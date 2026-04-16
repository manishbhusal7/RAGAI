using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Backend.Services.Integrations;

namespace Backend.Services.BackgroundServices
{
    public class CalendarSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CalendarSyncBackgroundService> _logger;
        private readonly IConfiguration _config;
        private readonly TimeSpan _syncInterval;

        public CalendarSyncBackgroundService(
            IServiceProvider serviceProvider, 
            ILogger<CalendarSyncBackgroundService> logger,
            IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;
            
            // Default to sync every 2 hours, configurable via appsettings
            var intervalHours = _config.GetValue("MicrosoftGraph:SyncIntervalHours", 2);
            _syncInterval = TimeSpan.FromHours(intervalHours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var autoSyncEnabled = _config.GetValue("MicrosoftGraph:EnableAutoSync", true);
            
            if (!autoSyncEnabled)
            {
                _logger.LogInformation("Calendar auto-sync is disabled in configuration");
                return;
            }

            _logger.LogInformation("Calendar Background Sync Service started. Sync interval: {Interval}", _syncInterval);

            // Initial delay of 45 seconds to let the application start up (after Confluence)
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting automatic calendar sync...");
                    await SyncCalendarEventsAsync();
                    _logger.LogInformation("Automatic calendar sync completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during automatic calendar sync");
                }

                // Wait for the next sync interval
                await Task.Delay(_syncInterval, stoppingToken);
            }
        }

        private async Task SyncCalendarEventsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var calendarService = scope.ServiceProvider.GetRequiredService<CalendarService>();
            
            var blobClient = new BlobContainerClient(
                _config["Azure:BlobStorageConnectionString"], 
                _config["Azure:BlobContainer"]);
            
            var searchEndpoint = new Uri(_config["AzureSearch:Endpoint"]);
            var searchApiKey = _config["AzureSearch:ApiKey"];
            var indexName = _config["AzureSearch:IndexName"];
            var searchClient = new SearchClient(searchEndpoint, indexName, new Azure.AzureKeyCredential(searchApiKey));

            try
            {
                // First, clean up old calendar events (older than 7 days)
                await CleanupOldCalendarEventsAsync(searchClient);

                // Fetch all events from calendars
                var events = await calendarService.FetchAllEventsAsync(30); // Next 30 days
                
                if (!events.Any())
                {
                    _logger.LogWarning("No calendar events found");
                    return;
                }

                var processedCount = 0;
                var errors = new List<string>();

                foreach (var calendarEvent in events)
                {
                    try
                    {
                        // Chunk the event content for better search
                        var chunks = calendarService.ChunkCalendarEvent(calendarEvent);
                        
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var chunk = chunks[i];
                            var chunkId = $"calendar_{calendarEvent.Id}_{i}";
                            
                            // Upload chunk to blob storage
                            var blobName = $"calendar/{calendarEvent.StartTime:yyyy-MM}/{chunkId}.txt";
                            var blobClientForEvent = blobClient.GetBlobClient(blobName);
                            
                            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(chunk));
                            await blobClientForEvent.UploadAsync(stream, overwrite: true);

                            // Index in Azure Search
                            var searchDocument = new SearchDocument
                            {
                                ["id"] = chunkId,
                                ["content"] = chunk,
                                ["source"] = "calendar",
                                ["filename"] = $"{calendarEvent.Subject} - {calendarEvent.StartTime:MMM dd, yyyy}"
                            };

                            var batch = IndexDocumentsBatch.Create<SearchDocument>(IndexDocumentsAction.Upload(searchDocument));
                            await searchClient.IndexDocumentsAsync(batch);
                        }

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        var error = $"Error processing calendar event {calendarEvent.Subject}: {ex.Message}";
                        errors.Add(error);
                        _logger.LogError(ex, "Error processing calendar event: {Subject}", calendarEvent.Subject);
                    }
                }

                // Update last sync time
                await UpdateLastSyncTimeAsync(blobClient);

                _logger.LogInformation("Calendar sync completed: {ProcessedCount}/{TotalCount} events processed", 
                    processedCount, events.Count);
                
                if (errors.Any())
                {
                    _logger.LogWarning("Calendar sync had {ErrorCount} errors", errors.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calendar sync");
                throw;
            }
        }

        private async Task CleanupOldCalendarEventsAsync(SearchClient searchClient)
        {
            try
            {
                // Find and delete calendar events older than 7 days
                var cutoffDate = DateTime.UtcNow.AddDays(-7);
                var cutoffDateString = cutoffDate.ToString("yyyy-MM-dd");

                var searchOptions = new SearchOptions
                {
                    Filter = $"source eq 'calendar'",
                    Size = 1000
                };

                var searchResults = await searchClient.SearchAsync<SearchDocument>("*", options: searchOptions);
                var documentsToDelete = new List<string>();

                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    if (result.Document.TryGetValue("content", out var contentObj) && contentObj is string content)
                    {
                        // Extract date from content to determine if it's old
                        if (IsOldCalendarEvent(content, cutoffDate))
                        {
                            if (result.Document.TryGetValue("id", out var idObj) && idObj is string id)
                            {
                                documentsToDelete.Add(id);
                            }
                        }
                    }
                }

                // Delete old documents in batches
                if (documentsToDelete.Any())
                {
                    const int batchSize = 100;
                    for (int i = 0; i < documentsToDelete.Count; i += batchSize)
                    {
                        var batch = documentsToDelete.Skip(i).Take(batchSize);
                        var deleteBatch = IndexDocumentsBatch.Create<SearchDocument>();
                        
                        foreach (var docId in batch)
                        {
                            deleteBatch.Actions.Add(IndexDocumentsAction.Delete("id", docId));
                        }

                        await searchClient.IndexDocumentsAsync(deleteBatch);
                    }

                    _logger.LogInformation("Cleaned up {Count} old calendar events", documentsToDelete.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old calendar events");
            }
        }

        private bool IsOldCalendarEvent(string content, DateTime cutoffDate)
        {
            // Simple check - look for date patterns in the content
            // This could be made more sophisticated
            try
            {
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("Date:"))
                    {
                        var dateString = line.Replace("Date:", "").Trim();
                        if (DateTime.TryParse(dateString, out var eventDate))
                        {
                            return eventDate < cutoffDate;
                        }
                    }
                }
            }
            catch
            {
                // If we can't parse the date, assume it's not old
            }
            
            return false;
        }

        private async Task UpdateLastSyncTimeAsync(BlobContainerClient blobClient)
        {
            try
            {
                var status = new SyncStatus { LastSync = DateTime.UtcNow };
                var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
                var statusBlob = blobClient.GetBlobClient("calendar/_sync_status.json");
                
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(statusJson));
                await statusBlob.UploadAsync(stream, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating calendar sync status");
            }
        }

        public class SyncStatus
        {
            public DateTime LastSync { get; set; }
        }
    }
} 