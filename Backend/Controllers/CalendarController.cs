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
    public class CalendarController : ControllerBase
    {
        private readonly CalendarService _calendarService;
        private readonly IConfiguration _config;
        private readonly BlobContainerClient _blobClient;
        private readonly SearchClient _searchClient;

        public CalendarController(
            CalendarService calendarService,
            IConfiguration config,
            HttpClient httpClient)
        {
            _calendarService = calendarService;
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

        [HttpPost("sync")]
        public async Task<IActionResult> SyncCalendarEvents()
        {
            try
            {
                var events = await _calendarService.FetchAllEventsAsync(30);

                if (!events.Any())
                {
                    return Ok(new { message = "No calendar events found", eventsCount = 0 });
                }

                var processedCount = 0;
                var errors = new List<string>();

                foreach (var calendarEvent in events)
                {
                    try
                    {
                        var chunks = _calendarService.ChunkCalendarEvent(calendarEvent);

                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var chunk = chunks[i];
                            var chunkId = $"calendar_{calendarEvent.Id}_{i}";

                            // Upload chunk to blob storage
                            var blobName = $"calendar/{calendarEvent.StartTime:yyyy-MM}/{chunkId}.txt";
                            var blobClientForEvent = _blobClient.GetBlobClient(blobName);

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
                            await _searchClient.IndexDocumentsAsync(batch);
                        }

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error processing event {calendarEvent.Subject}: {ex.Message}");
                    }
                }

                // Update last sync time
                await UpdateLastSyncTimeAsync();

                return Ok(new
                {
                    message = $"Successfully processed {processedCount} calendar events",
                    processedCount,
                    totalEvents = events.Count,
                    lastSync = DateTime.UtcNow,
                    errors = errors.Any() ? errors : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Error syncing calendar events: {ex.Message}" });
            }
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetSyncStatus()
        {
            try
            {
                // Count calendar events in Azure Search
                var searchOptions = new SearchOptions
                {
                    Filter = "source eq 'calendar'",
                    IncludeTotalCount = true,
                    Size = 0
                };

                var searchResults = await _searchClient.SearchAsync<SearchDocument>("*", options: searchOptions);
                var calendarEvents = searchResults.Value.TotalCount ?? 0;

                // Count all documents in Azure Search
                var allSearchOptions = new SearchOptions
                {
                    IncludeTotalCount = true,
                    Size = 0
                };
                var allSearchResults = await _searchClient.SearchAsync<SearchDocument>("*", options: allSearchOptions);
                var totalDocuments = allSearchResults.Value.TotalCount ?? 0;

                // Count blobs in calendar folder
                var calendarBlobs = _blobClient.GetBlobsAsync(prefix: "calendar/");
                var blobCount = 0;
                await foreach (var blob in calendarBlobs)
                {
                    blobCount++;
                }

                // Try to get last sync time from a status blob
                var lastSyncTime = await GetLastSyncTimeAsync();
                var autoSyncEnabled = _config.GetValue("MicrosoftGraph:EnableAutoSync", true);
                var syncInterval = _config.GetValue("MicrosoftGraph:SyncIntervalHours", 2);

                return Ok(new
                {
                    calendarEvents,
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

        [HttpGet("events")]
        public async Task<IActionResult> GetUpcomingEvents([FromQuery] int days = 7)
        {
            try
            {
                var events = await _calendarService.FetchAllEventsAsync(days);

                var eventSummaries = events.Select(evt => new
                {
                    id = evt.Id,
                    subject = evt.Subject,
                    startTime = evt.StartTime.ToString("yyyy-MM-dd HH:mm"),
                    endTime = evt.EndTime.ToString("yyyy-MM-dd HH:mm"),
                    location = evt.Location,
                    organizer = evt.Organizer,
                    eventType = evt.EventType,
                    isAllDay = evt.IsAllDay,
                    calendarName = evt.CalendarName
                }).OrderBy(e => e.startTime).ToList();

                return Ok(new
                {
                    totalEvents = eventSummaries.Count,
                    daysAhead = days,
                    events = eventSummaries
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetTodayEvents()
        {
            try
            {
                var allEvents = await _calendarService.FetchAllEventsAsync(1);
                var today = DateTime.Today;

                var todayEvents = allEvents
                    .Where(e => e.StartTime.Date == today || e.EndTime.Date == today)
                    .Select(evt => new
                    {
                        subject = evt.Subject,
                        startTime = evt.StartTime.ToString("HH:mm"),
                        endTime = evt.EndTime.ToString("HH:mm"),
                        location = evt.Location,
                        eventType = evt.EventType,
                        isAllDay = evt.IsAllDay
                    })
                    .OrderBy(e => e.startTime)
                    .ToList();

                return Ok(new
                {
                    date = today.ToString("yyyy-MM-dd"),
                    eventsCount = todayEvents.Count,
                    events = todayEvents
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("holidays")]
        public async Task<IActionResult> GetHolidays([FromQuery] int days = 30)
        {
            try
            {
                var allEvents = await _calendarService.FetchAllEventsAsync(days);

                var holidays = allEvents
                    .Where(e => e.EventType == "holiday" || e.EventType == "office-closure")
                    .Select(evt => new
                    {
                        subject = evt.Subject,
                        date = evt.StartTime.ToString("yyyy-MM-dd"),
                        type = evt.EventType,
                        isAllDay = evt.IsAllDay,
                        description = evt.Description
                    })
                    .OrderBy(e => e.date)
                    .ToList();

                return Ok(new
                {
                    holidaysCount = holidays.Count,
                    daysAhead = days,
                    holidays = holidays
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
                var statusBlob = _blobClient.GetBlobClient("calendar/_sync_status.json");
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
                Console.WriteLine($"Error reading calendar sync status: {ex.Message}");
            }
            return null;
        }

        private async Task UpdateLastSyncTimeAsync()
        {
            try
            {
                var status = new SyncStatus { LastSync = DateTime.UtcNow };
                var statusJson = System.Text.Json.JsonSerializer.Serialize(status);
                var statusBlob = _blobClient.GetBlobClient("calendar/_sync_status.json");

                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(statusJson));
                await statusBlob.UploadAsync(stream, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating calendar sync status: {ex.Message}");
            }
        }

        public class SyncStatus
        {
            public DateTime LastSync { get; set; }
        }
    }
}