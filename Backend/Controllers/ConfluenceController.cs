using Microsoft.AspNetCore.Mvc;
using Backend.Services.Integrations;
using Backend.Services.Search;
using Azure.Storage.Blobs;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace RAG.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfluenceController : ControllerBase
    {
        private readonly ConfluenceService _confluenceService;
        private readonly IConfiguration _config;
        private readonly BlobContainerClient _blobClient;
        private readonly SearchClient _searchClient;

        public ConfluenceController(
            ConfluenceService confluenceService, 
            IConfiguration config,
            HttpClient httpClient)
        {
            _confluenceService = confluenceService;
            _config = config;
            
            // Initialize Azure services
            _blobClient = new BlobContainerClient(
                _config["Azure:BlobStorageConnectionString"], 
                _config["Azure:BlobContainer"]);
            
            var searchEndpoint = new Uri(_config["AzureSearch:Endpoint"]);
            var searchApiKey = _config["AzureSearch:ApiKey"];
            var indexName = _config["AzureSearch:IndexName"];
            
            _searchClient = new SearchClient(searchEndpoint, indexName, new Azure.AzureKeyCredential(searchApiKey));
        }

        [HttpGet("validate")]
        public async Task<IActionResult> ValidateConfluenceAuth()
        {
            var baseUrl = _config["Confluence:BaseUrl"];
            var username = _config["Confluence:Username"];
            var spaceKey = _config["Confluence:SpaceKey"];

            var result = await _confluenceService.ValidateCredentialsAsync();
            return Ok(new
            {
                baseUrl,
                username,
                spaceKey,
                authSuccess = result
            });
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncConfluenceDocuments()
        {
            try
            {
                // Fetch all documents from Confluence
                var documents = await _confluenceService.FetchAllDocumentsAsync();
                
                if (!documents.Any())
                {
                    return BadRequest("No documents found in Confluence or all documents are internal.");
                }

                var processedCount = 0;
                var errors = new List<string>();

                foreach (var document in documents)
                {
                    try
                    {
                        // Chunk the content for better search
                        var chunks = _confluenceService.ChunkContent(document.Content);
                        
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var chunk = chunks[i];
                            var chunkId = $"confluence_{document.Id}_{i}";
                            
                            // Upload chunk to blob storage
                            var blobName = $"confluence/{document.Id}/{chunkId}.txt";
                            var blobClient = _blobClient.GetBlobClient(blobName);
                            
                            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(chunk));
                            await blobClient.UploadAsync(stream, overwrite: true);

                            // Index in Azure Search (using existing schema)
                            var searchDocument = new SearchDocument
                            {
                                ["id"] = chunkId,
                                ["content"] = chunk,
                                ["source"] = "confluence",
                                ["filename"] = document.Title
                            };

                            var batch = IndexDocumentsBatch.Create<SearchDocument>(IndexDocumentsAction.Upload(searchDocument));
                            await _searchClient.IndexDocumentsAsync(batch);
                        }

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error processing document {document.Title}: {ex.Message}");
                    }
                }

                // Update last sync time
                await UpdateLastSyncTimeAsync();

                return Ok(new 
                { 
                    message = $"Successfully processed {processedCount} documents",
                    processedCount,
                    totalDocuments = documents.Count,
                    lastSync = DateTime.UtcNow,
                    errors = errors.Any() ? errors : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Error syncing Confluence documents: {ex.Message}" });
            }
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetSyncStatus()
        {
            try
            {
                // Count documents in Azure Search (Confluence only)
                var searchOptions = new SearchOptions
                {
                    Filter = "source eq 'confluence'",
                    IncludeTotalCount = true,
                    Size = 0
                };

                var searchResults = await _searchClient.SearchAsync<SearchDocument>("*", options: searchOptions);
                var confluenceDocuments = searchResults.Value.TotalCount ?? 0;

                // Count all documents in Azure Search
                var allSearchOptions = new SearchOptions
                {
                    IncludeTotalCount = true,
                    Size = 0
                };
                var allSearchResults = await _searchClient.SearchAsync<SearchDocument>("*", options: allSearchOptions);
                var totalDocuments = allSearchResults.Value.TotalCount ?? 0;

                // Count blobs in confluence folder
                var confluenceBlobs = _blobClient.GetBlobsAsync(prefix: "confluence/");
                var blobCount = 0;
                await foreach (var blob in confluenceBlobs)
                {
                    blobCount++;
                }

                // Try to get last sync time from a status blob
                var lastSyncTime = await GetLastSyncTimeAsync();
                var autoSyncEnabled = _config.GetValue("Confluence:EnableAutoSync", true);
                var syncInterval = _config.GetValue("Confluence:SyncIntervalHours", 6);

                return Ok(new
                {
                    confluenceDocuments,
                    totalDocuments,
                    totalBlobs = blobCount,
                    lastSync = lastSyncTime,
                    autoSyncEnabled,
                    syncIntervalHours = syncInterval,
                    nextAutoSync = autoSyncEnabled && lastSyncTime.HasValue ? 
                        lastSyncTime.Value.AddHours(syncInterval) : (DateTime?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Error getting sync status: {ex.Message}" });
            }
        }

        [HttpGet("spaces")]
        public async Task<IActionResult> GetAvailableSpaces()
        {
            try
            {
                var baseUrl = _config["Confluence:BaseUrl"];
                var username = _config["Confluence:Username"];
                var password = _config["Confluence:Password"];
                
                var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                
                var url = $"{baseUrl}/rest/api/space";
                var response = await httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                    
                    var spaces = new List<object>();
                    if (data.TryGetProperty("results", out var results))
                    {
                        var spaceArray = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(results.GetRawText());
                        if (spaceArray != null)
                        {
                            foreach (var space in spaceArray)
                            {
                                spaces.Add(new
                                {
                                    key = space.GetProperty("key").GetString(),
                                    name = space.GetProperty("name").GetString(),
                                    type = space.GetProperty("type").GetString()
                                });
                            }
                        }
                    }
                    
                    return Ok(new { 
                        currentSpace = _config["Confluence:SpaceKey"],
                        availableSpaces = spaces,
                        totalSpaces = spaces.Count
                    });
                }
                else
                {
                    return BadRequest(new { error = $"Failed to fetch spaces: {response.StatusCode}" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("documents")]
        public async Task<IActionResult> GetConfluenceDocuments()
        {
            try
            {
                var documents = await _confluenceService.FetchAllDocumentsAsync();
                
                var documentTitles = documents.Select(doc => new
                {
                    id = doc.Id,
                    title = doc.Title,
                    url = doc.TinyLink,
                    createdDate = doc.CreatedDate.ToString("yyyy-MM-dd"),
                    lastModified = doc.LastModified.ToString("yyyy-MM-dd")
                }).ToList();
                
                return Ok(new
                {
                    spaceKey = _config["Confluence:SpaceKey"],
                    totalDocuments = documentTitles.Count,
                    documents = documentTitles
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<DateTime?> GetLastSyncTimeAsync()
        {
            try
            {
                var statusBlob = _blobClient.GetBlobClient("confluence/_sync_status.json");
                if (await statusBlob.ExistsAsync())
                {
                    var content = await statusBlob.DownloadContentAsync();
                    var statusText = content.Value.Content.ToString();
                    var status = System.Text.Json.JsonSerializer.Deserialize<SyncStatus>(statusText);
                    return status?.LastSync;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading sync status: {ex.Message}");
            }
            return null;
        }

        private async Task UpdateLastSyncTimeAsync()
        {
            try
            {
                var status = new SyncStatus { LastSync = DateTime.UtcNow };
                var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
                var statusBlob = _blobClient.GetBlobClient("confluence/_sync_status.json");
                
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(statusJson));
                await statusBlob.UploadAsync(stream, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating sync status: {ex.Message}");
            }
        }

        public class SyncStatus
        {
            public DateTime LastSync { get; set; }
        }
    }
} 