using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Backend.Services.Integrations;

namespace Backend.Services.BackgroundServices
{
    public class ConfluenceSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConfluenceSyncBackgroundService> _logger;
        private readonly IConfiguration _config;
        private readonly TimeSpan _syncInterval;

        public ConfluenceSyncBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ConfluenceSyncBackgroundService> logger,
            IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;

            // Default to sync every 6 hours, configurable via appsettings
            var intervalHours = _config.GetValue("Confluence:SyncIntervalHours", 6);
            _syncInterval = TimeSpan.FromHours(intervalHours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var autoSyncEnabled = _config.GetValue("Confluence:EnableAutoSync", true);

            if (!autoSyncEnabled)
            {
                _logger.LogInformation("Confluence auto-sync is disabled in configuration");
                return;
            }

            _logger.LogInformation("Confluence Background Sync Service started. Sync interval: {Interval}", _syncInterval);

            // Initial delay of 30 seconds to let the application start up
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting automatic Confluence sync...");
                    await SyncConfluenceDocumentsAsync();
                    _logger.LogInformation("Automatic Confluence sync completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during automatic Confluence sync");
                }

                // Wait for the next sync interval
                await Task.Delay(_syncInterval, stoppingToken);
            }
        }

        private async Task SyncConfluenceDocumentsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var confluenceService = scope.ServiceProvider.GetRequiredService<ConfluenceService>();

            var blobClient = new BlobContainerClient(
                _config["Azure:BlobStorageConnectionString"],
                _config["Azure:BlobContainer"]);

            var searchEndpoint = new Uri(_config["AzureSearch:Endpoint"]);
            var searchApiKey = _config["AzureSearch:ApiKey"];
            var indexName = _config["AzureSearch:IndexName"];
            var searchClient = new SearchClient(searchEndpoint, indexName, new Azure.AzureKeyCredential(searchApiKey));

            try
            {
                // Validate auth first
                var authOk = await confluenceService.ValidateCredentialsAsync();
                if (!authOk)
                {
                    _logger.LogWarning("Confluence authentication failed. Aborting sync.");
                    return;
                }

                // Fetch all documents from Confluence
                var documents = await confluenceService.FetchAllDocumentsAsync();

                if (!documents.Any())
                {
                    _logger.LogWarning("No documents found in Confluence or all documents are internal");
                    return;
                }

                var processedCount = 0;
                var errors = new List<string>();

                foreach (var document in documents)
                {
                    try
                    {
                        // Chunk the content for better search
                        var chunks = confluenceService.ChunkContent(document.Content);

                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var chunk = chunks[i];
                            var chunkId = $"confluence_{document.Id}_{i}";

                            // Upload chunk to blob storage
                            var blobName = $"confluence/{document.Id}/{chunkId}.txt";
                            var blobClientForDoc = blobClient.GetBlobClient(blobName);

                            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(chunk));
                            await blobClientForDoc.UploadAsync(stream, overwrite: true);

                            // Index in Azure Search
                            var searchDocument = new SearchDocument
                            {
                                ["id"] = chunkId,
                                ["content"] = chunk,
                                ["source"] = "confluence",
                                ["filename"] = document.Title
                            };

                            var batch = IndexDocumentsBatch.Create<SearchDocument>(IndexDocumentsAction.Upload(searchDocument));
                            await searchClient.IndexDocumentsAsync(batch);
                        }

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        var error = $"Error processing document {document.Title}: {ex.Message}";
                        errors.Add(error);
                        _logger.LogError(ex, "Error processing Confluence document: {Title}", document.Title);
                    }
                }

                // Update last sync time
                await UpdateLastSyncTimeAsync(blobClient);

                _logger.LogInformation("Confluence sync completed: {ProcessedCount}/{TotalCount} documents processed",
                    processedCount, documents.Count);

                if (errors.Any())
                {
                    _logger.LogWarning("Confluence sync had {ErrorCount} errors", errors.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Confluence sync");
                throw;
            }
        }

        private async Task UpdateLastSyncTimeAsync(BlobContainerClient blobClient)
        {
            try
            {
                var status = new SyncStatus { LastSync = DateTime.UtcNow };
                var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
                var statusBlob = blobClient.GetBlobClient("confluence/_sync_status.json");

                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(statusJson));
                await statusBlob.UploadAsync(stream, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync status");
            }
        }

        public class SyncStatus
        {
            public DateTime LastSync { get; set; }
        }
    }
}