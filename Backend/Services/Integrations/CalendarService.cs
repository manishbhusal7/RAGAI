using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Backend.Services.Integrations
{
    /// <summary>
    /// Represents a calendar event with all its important details
    /// </summary>
    public class CalendarEvent
    {
        public string Id { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Organizer { get; set; } = string.Empty;
        public List<string> Attendees { get; set; } = new List<string>();
        public bool IsAllDay { get; set; }
        public string EventType { get; set; } = string.Empty; // "meeting", "holiday", "office-closure", "team-event"
        public string CalendarName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for fetching and managing calendar events from Microsoft Graph API.
    /// This service helps the AI assistant answer questions about meetings, holidays, and office schedules.
    /// </summary>
    public class CalendarService
    {
        // Constants to make the code more readable and maintainable
        private const int DEFAULT_DAYS_TO_FETCH = 30;           // How many days ahead to fetch events
        private const int MAX_EVENTS_PER_CALENDAR = 1000;       // Maximum events to fetch from each calendar
        private const int TEXT_CHUNK_SIZE = 800;                // Size of text chunks for AI processing
        private const string MICROSOFT_GRAPH_SCOPE = "https://graph.microsoft.com/.default";
        private const string DEFAULT_CALENDAR_IDENTIFIER = "me";

        // Service dependencies - these help us make HTTP calls, read configuration, and log information
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CalendarService> _logger;
        
        // Microsoft Graph API credentials and settings
        private readonly string _tenantId;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly List<string> _calendarIds;

        /// <summary>
        /// Creates a new CalendarService with the necessary dependencies and configuration
        /// </summary>
        public CalendarService(IConfiguration configuration, HttpClient httpClient, ILogger<CalendarService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
            
            // Read Microsoft Graph API credentials from configuration
            _tenantId = _configuration["MicrosoftGraph:TenantId"] ?? "";
            _clientId = _configuration["MicrosoftGraph:ClientId"] ?? "";
            _clientSecret = _configuration["MicrosoftGraph:ClientSecret"] ?? "";
            
            // Get the list of calendars we want to monitor (like company calendar, holidays calendar, etc.)
            _calendarIds = _configuration.GetSection("MicrosoftGraph:CalendarIds").Get<List<string>>() ?? new List<string>();
        }

        /// <summary>
        /// Fetches all calendar events from the configured calendars for the specified number of days ahead.
        /// This is the main method that gets all events the AI assistant needs to know about.
        /// </summary>
        /// <param name="daysAhead">Number of days in the future to fetch events for (default: 30 days)</param>
        /// <returns>List of all calendar events found</returns>
        public async Task<List<CalendarEvent>> FetchAllEventsAsync(int daysAhead = DEFAULT_DAYS_TO_FETCH)
        {
            var allCalendarEvents = new List<CalendarEvent>();
            
            try
            {
                // Step 1: Get permission to access Microsoft Graph API
                var accessToken = await GetMicrosoftGraphAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Could not get access token for Microsoft Graph API. Check your credentials in configuration.");
                    return allCalendarEvents;
                }

                // Step 2: Calculate the date range we want to fetch events for
                var dateRange = CalculateDateRange(daysAhead);

                // Step 3: Fetch events from each configured calendar
                if (_calendarIds.Any())
                {
                    // We have specific calendars configured (like company calendar, holidays calendar)
                    foreach (var calendarId in _calendarIds)
                    {
                        var eventsFromThisCalendar = await FetchEventsFromSpecificCalendarAsync(
                            accessToken, calendarId, dateRange.startDate, dateRange.endDate);
                        allCalendarEvents.AddRange(eventsFromThisCalendar);
                    }
                }
                else
                {
                    // No specific calendars configured, so use the default user calendar
                    var defaultCalendarEvents = await FetchEventsFromSpecificCalendarAsync(
                        accessToken, DEFAULT_CALENDAR_IDENTIFIER, dateRange.startDate, dateRange.endDate);
                    allCalendarEvents.AddRange(defaultCalendarEvents);
                }

                // Log success message for debugging and monitoring
                _logger.LogInformation("Successfully fetched {EventCount} calendar events from {CalendarCount} calendars", 
                    allCalendarEvents.Count, Math.Max(_calendarIds.Count, 1));

                return allCalendarEvents;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error occurred while fetching calendar events");
                return allCalendarEvents; // Return empty list instead of crashing
            }
        }

        /// <summary>
        /// Calculates the start and end dates for fetching calendar events
        /// </summary>
        /// <param name="daysAhead">Number of days to look ahead</param>
        /// <returns>Tuple with formatted start and end dates</returns>
        private (string startDate, string endDate) CalculateDateRange(int daysAhead)
        {
            var currentTime = DateTime.UtcNow;
            var futureTime = currentTime.AddDays(daysAhead);
            
            // Format dates in the way Microsoft Graph API expects them
            var startDate = currentTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var endDate = futureTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            
            return (startDate, endDate);
        }

        /// <summary>
        /// Gets an access token from Microsoft Graph API so we can read calendar data.
        /// This uses the "client credentials" flow which is for applications (not users).
        /// </summary>
        /// <returns>Access token string, or empty string if authentication failed</returns>
        private async Task<string> GetMicrosoftGraphAccessTokenAsync()
        {
            try
            {
                // Build the URL for Microsoft's token endpoint
                var tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";
                
                // Prepare the authentication request with our app credentials
                var authenticationRequest = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "client_credentials"),      // This type means "app authentication"
                    new("client_id", _clientId),                  // Our app's ID
                    new("client_secret", _clientSecret),          // Our app's secret key
                    new("scope", MICROSOFT_GRAPH_SCOPE)          // What permissions we're asking for
                };

                // Send the authentication request to Microsoft
                var requestContent = new FormUrlEncodedContent(authenticationRequest);
                var authenticationResponse = await _httpClient.PostAsync(tokenEndpoint, requestContent);
                
                if (authenticationResponse.IsSuccessStatusCode)
                {
                    // Parse the response to get our access token
                    var responseContent = await authenticationResponse.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    return tokenData.GetProperty("access_token").GetString() ?? "";
                }
                else
                {
                    var errorContent = await authenticationResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get Microsoft Graph access token. Status: {StatusCode}, Error: {ErrorDetails}", 
                        authenticationResponse.StatusCode, errorContent);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Exception occurred while getting Microsoft Graph access token");
            }
            
            return ""; // Return empty string to indicate failure
        }

        /// <summary>
        /// Fetches events from a specific calendar using Microsoft Graph API
        /// </summary>
        /// <param name="accessToken">Valid access token for Microsoft Graph</param>
        /// <param name="calendarId">ID of the calendar to fetch from (or "me" for default calendar)</param>
        /// <param name="startDate">Start date in ISO format</param>
        /// <param name="endDate">End date in ISO format</param>
        /// <returns>List of calendar events from this specific calendar</returns>
        private async Task<List<CalendarEvent>> FetchEventsFromSpecificCalendarAsync(
            string accessToken, string calendarId, string startDate, string endDate)
        {
            var eventsFromThisCalendar = new List<CalendarEvent>();
            
            try
            {
                // Add our access token to the HTTP request headers
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Build the Microsoft Graph API URL for this calendar
                var graphApiUrl = BuildGraphApiUrl(calendarId, startDate, endDate);

                // Make the API call to Microsoft Graph
                var apiResponse = await _httpClient.GetAsync(graphApiUrl);
                
                if (apiResponse.IsSuccessStatusCode)
                {
                    // Parse the response and convert to our CalendarEvent objects
                    var responseContent = await apiResponse.Content.ReadAsStringAsync();
                    var eventsData = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (eventsData.TryGetProperty("value", out var eventsArray))
                    {
                        foreach (var eventJsonData in eventsArray.EnumerateArray())
                        {
                            var parsedEvent = ParseEventFromJson(eventJsonData, calendarId);
                            if (parsedEvent != null)
                            {
                                eventsFromThisCalendar.Add(parsedEvent);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogError("Failed to fetch events from calendar '{CalendarId}'. Status code: {StatusCode}", 
                        calendarId, apiResponse.StatusCode);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error occurred while fetching events from calendar '{CalendarId}'", calendarId);
            }

            return eventsFromThisCalendar;
        }

        /// <summary>
        /// Builds the Microsoft Graph API URL for fetching calendar events
        /// </summary>
        private string BuildGraphApiUrl(string calendarId, string startDate, string endDate)
        {
            // Different URL structure depending on whether we're accessing the default calendar or a specific one
            var baseGraphEndpoint = calendarId == DEFAULT_CALENDAR_IDENTIFIER 
                ? "https://graph.microsoft.com/v1.0/me/calendar/events"
                : $"https://graph.microsoft.com/v1.0/users/{calendarId}/calendar/events";

            // Add query parameters to filter by date range and specify what data we want
            var urlWithParameters = $"{baseGraphEndpoint}" +
                $"?$filter=start/dateTime ge '{startDate}' and end/dateTime le '{endDate}'" +
                $"&$select=id,subject,body,start,end,location,organizer,attendees,isAllDay" +
                $"&$orderby=start/dateTime" +
                $"&$top={MAX_EVENTS_PER_CALENDAR}";

            return urlWithParameters;
        }

        /// <summary>
        /// Converts JSON data from Microsoft Graph API into our CalendarEvent object
        /// </summary>
        /// <param name="eventJsonData">JSON data for one event from Microsoft Graph</param>
        /// <param name="calendarId">ID of the calendar this event came from</param>
        /// <returns>CalendarEvent object, or null if parsing failed</returns>
        private CalendarEvent? ParseEventFromJson(JsonElement eventJsonData, string calendarId)
        {
            try
            {
                // Extract basic event information
                var eventId = eventJsonData.GetProperty("id").GetString() ?? "";
                var eventSubject = eventJsonData.GetProperty("subject").GetString() ?? "";
                
                // Parse start and end times
                var startElement = eventJsonData.GetProperty("start");
                var endElement = eventJsonData.GetProperty("end");
                
                var startTime = DateTime.Parse(startElement.GetProperty("dateTime").GetString() ?? "");
                var endTime = DateTime.Parse(endElement.GetProperty("dateTime").GetString() ?? "");
                
                // Check if this is an all-day event
                var isAllDayEvent = eventJsonData.TryGetProperty("isAllDay", out var allDayElement) 
                    && allDayElement.GetBoolean();
                
                // Extract location information if available
                var eventLocation = ExtractLocationFromJson(eventJsonData);
                
                // Extract organizer information if available
                var eventOrganizer = ExtractOrganizerFromJson(eventJsonData);
                
                // Extract attendees list if available
                var eventAttendees = ExtractAttendeesFromJson(eventJsonData);
                
                // Extract and clean up the event description
                var eventDescription = ExtractAndCleanDescription(eventJsonData);
                
                // Determine what type of event this is (meeting, holiday, etc.)
                var eventType = DetermineEventType(eventSubject, eventDescription, eventOrganizer, calendarId, eventAttendees);

                // Create and return the CalendarEvent object
                return new CalendarEvent
                {
                    Id = eventId,
                    Subject = eventSubject,
                    Description = eventDescription,
                    StartTime = startTime,
                    EndTime = endTime,
                    Location = eventLocation,
                    Organizer = eventOrganizer,
                    Attendees = eventAttendees,
                    IsAllDay = isAllDayEvent,
                    EventType = eventType,
                    CalendarName = calendarId
                };
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not parse calendar event from JSON data. Skipping this event.");
                return null; // Skip events that can't be parsed rather than crashing
            }
        }

        /// <summary>
        /// Extracts location information from the JSON event data
        /// </summary>
        private string ExtractLocationFromJson(JsonElement eventJsonData)
        {
            if (eventJsonData.TryGetProperty("location", out var locationElement) && 
                locationElement.TryGetProperty("displayName", out var displayNameElement))
            {
                return displayNameElement.GetString() ?? "";
            }
            return "";
        }

        /// <summary>
        /// Extracts organizer information from the JSON event data
        /// </summary>
        private string ExtractOrganizerFromJson(JsonElement eventJsonData)
        {
            if (eventJsonData.TryGetProperty("organizer", out var organizerElement) &&
                organizerElement.TryGetProperty("emailAddress", out var emailElement) &&
                emailElement.TryGetProperty("name", out var nameElement))
            {
                return nameElement.GetString() ?? "";
            }
            return "";
        }

        /// <summary>
        /// Extracts the list of attendees from the JSON event data
        /// </summary>
        private List<string> ExtractAttendeesFromJson(JsonElement eventJsonData)
        {
            var attendeesList = new List<string>();
            
            if (eventJsonData.TryGetProperty("attendees", out var attendeesElement))
            {
                foreach (var attendee in attendeesElement.EnumerateArray())
                {
                    if (attendee.TryGetProperty("emailAddress", out var emailElement) &&
                        emailElement.TryGetProperty("name", out var nameElement))
                    {
                        var attendeeName = nameElement.GetString();
                        if (!string.IsNullOrEmpty(attendeeName))
                        {
                            attendeesList.Add(attendeeName);
                        }
                    }
                }
            }
            
            return attendeesList;
        }

        /// <summary>
        /// Extracts and cleans up the event description from JSON data
        /// </summary>
        private string ExtractAndCleanDescription(JsonElement eventJsonData)
        {
            var description = "";
            
            if (eventJsonData.TryGetProperty("body", out var bodyElement) &&
                bodyElement.TryGetProperty("content", out var contentElement))
            {
                description = contentElement.GetString() ?? "";
                description = CleanDescription(description);
            }
            
            return description;
        }

        /// <summary>
        /// Determines what type of event this is based on the subject, description, and other properties.
        /// This helps the AI assistant categorize events for better responses.
        /// </summary>
        /// <param name="subject">The event title/subject</param>
        /// <param name="description">The event description/body</param>
        /// <param name="organizer">Who organized the event</param>
        /// <param name="calendarId">Which calendar this event came from</param>
        /// <param name="attendees">List of people attending the event</param>
        /// <returns>Event type: "holiday", "office-closure", "team-event", or "meeting"</returns>
        private string DetermineEventType(string subject, string description, string organizer, string calendarId, List<string> attendees)
        {
            // Convert to lowercase for easier text matching
            var eventText = $"{subject} {description}".ToLower();

            // Check if this is a holiday (highest priority)
            if (IsHolidayEvent(eventText))
            {
                return "holiday";
            }

            // Check if this is an office closure
            if (IsOfficeClosureEvent(eventText))
            {
                return "office-closure";
            }

            // Check if this is a team/company event
            if (IsTeamEvent(eventText, attendees.Count))
            {
                return "team-event";
            }

            // Default to regular meeting
            return "meeting";
        }

        /// <summary>
        /// Checks if the event text indicates this is a holiday
        /// </summary>
        private bool IsHolidayEvent(string eventText)
        {
            var holidayKeywords = new[]
            {
                "holiday", "company holiday", "bank holiday",
                "christmas", "thanksgiving", "new year",
                "memorial day", "labor day", "independence day",
                "martin luther king", "presidents day", "veterans day"
            };

            return holidayKeywords.Any(keyword => eventText.Contains(keyword));
        }

        /// <summary>
        /// Checks if the event text indicates this is an office closure
        /// </summary>
        private bool IsOfficeClosureEvent(string eventText)
        {
            var closureKeywords = new[]
            {
                "office closed", "office closure", "building closed",
                "facility closed", "no access", "office shutdown"
            };

            return closureKeywords.Any(keyword => eventText.Contains(keyword));
        }

        /// <summary>
        /// Checks if this appears to be a team or company-wide event
        /// </summary>
        private bool IsTeamEvent(string eventText, int attendeeCount)
        {
            var teamKeywords = new[]
            {
                "team", "all hands", "company", "nba",
                "town hall", "quarterly", "annual"
            };

            // Large meetings (more than 5 people) are often team events
            var hasTeamKeywords = teamKeywords.Any(keyword => eventText.Contains(keyword));
            var isLargeMeeting = eventText.Contains("meeting") && attendeeCount > 5;

            return hasTeamKeywords || isLargeMeeting;
        }

        /// <summary>
        /// Cleans up HTML content and formatting from event descriptions.
        /// This makes the text easier for the AI to process and understand.
        /// </summary>
        /// <param name="description">Raw description text that might contain HTML</param>
        /// <returns>Clean, readable text without HTML formatting</returns>
        private string CleanDescription(string description)
        {
            // Handle empty or null descriptions
            if (string.IsNullOrEmpty(description))
                return "";

            try
            {
                // Step 1: Remove HTML tags (like <div>, <p>, <br>, etc.)
                description = Regex.Replace(description, "<[^>]+>", "");
                
                // Step 2: Clean up whitespace (replace multiple spaces/newlines with single spaces)
                description = Regex.Replace(description, @"\s+", " ").Trim();
                
                // Step 3: Limit length to keep it manageable for AI processing
                const int MAX_DESCRIPTION_LENGTH = 1000;
                if (description.Length > MAX_DESCRIPTION_LENGTH)
                {
                    description = description.Substring(0, MAX_DESCRIPTION_LENGTH - 3) + "...";
                }

                return description;
            }
            catch (Exception)
            {
                // If cleaning fails for any reason, return the original text
                return description;
            }
        }

        /// <summary>
        /// Converts a calendar event into text chunks that the AI can easily understand and search through.
        /// This creates a formatted, human-readable description of the event with all important details.
        /// </summary>
        /// <param name="calendarEvent">The calendar event to convert to text</param>
        /// <returns>List of text chunks (usually just one per event) describing the event</returns>
        public List<string> ChunkCalendarEvent(CalendarEvent calendarEvent)
        {
            var textChunks = new List<string>();
            
            try
            {
                // Build a comprehensive, readable description of the event
                var eventDescription = BuildEventDescription(calendarEvent);

                // Check if the description is too long and needs to be split
                if (eventDescription.Length <= TEXT_CHUNK_SIZE)
                {
                    // Event fits in one chunk - use it as is
                    textChunks.Add(eventDescription);
                }
                else
                {
                    // Event is too long - split it into smaller chunks
                    var splitChunks = SplitLongEventIntoChunks(calendarEvent, eventDescription);
                    textChunks.AddRange(splitChunks);
                }

                return textChunks;
            }
            catch (Exception exception)
            {
                // If something goes wrong, create a simple fallback description
                var fallbackDescription = $"Event: {calendarEvent.Subject} on {calendarEvent.StartTime:dddd, MMMM dd, yyyy}";
                textChunks.Add(fallbackDescription);
                
                _logger.LogWarning(exception, "Error creating chunks for calendar event '{EventSubject}'. Using fallback description.", 
                    calendarEvent.Subject);
                
                return textChunks;
            }
        }

        /// <summary>
        /// Builds a complete, formatted description of a calendar event
        /// </summary>
        private string BuildEventDescription(CalendarEvent calendarEvent)
        {
            var description = new StringBuilder();

            // Start with the event title
            description.AppendLine($"Event: {calendarEvent.Subject}");
            
            // Add date information in a user-friendly format
            description.AppendLine($"Date: {calendarEvent.StartTime:dddd, MMMM dd, yyyy}");
            
            // Add time information
            if (calendarEvent.IsAllDay)
            {
                description.AppendLine("Time: All Day Event");
            }
            else
            {
                description.AppendLine($"Time: {calendarEvent.StartTime:h:mm tt} - {calendarEvent.EndTime:h:mm tt}");
            }
            
            // Add location if available
            if (!string.IsNullOrEmpty(calendarEvent.Location))
            {
                description.AppendLine($"Location: {calendarEvent.Location}");
            }
            
            // Add organizer if available
            if (!string.IsNullOrEmpty(calendarEvent.Organizer))
            {
                description.AppendLine($"Organizer: {calendarEvent.Organizer}");
            }
            
            // Add attendees if there are any
            if (calendarEvent.Attendees.Any())
            {
                var attendeeList = string.Join(", ", calendarEvent.Attendees.Take(10)); // Limit to first 10 attendees
                if (calendarEvent.Attendees.Count > 10)
                {
                    attendeeList += $" and {calendarEvent.Attendees.Count - 10} more";
                }
                description.AppendLine($"Attendees: {attendeeList}");
            }
            
            // Add event type to help the AI categorize
            description.AppendLine($"Type: {calendarEvent.EventType}");
            
            // Add description/details if available
            if (!string.IsNullOrEmpty(calendarEvent.Description))
            {
                description.AppendLine($"Description: {calendarEvent.Description}");
            }

            return description.ToString();
        }

        /// <summary>
        /// Splits a long event description into smaller chunks that fit within the size limit
        /// </summary>
        private List<string> SplitLongEventIntoChunks(CalendarEvent calendarEvent, string fullDescription)
        {
            var chunks = new List<string>();
            
            // Create a basic event header that will go in each chunk
            var eventHeader = $"Event: {calendarEvent.Subject}\n" +
                             $"Date: {calendarEvent.StartTime:dddd, MMMM dd, yyyy}\n" +
                             $"Type: {calendarEvent.EventType}\n";

            // Calculate how much space we have left for the description
            var remainingSpace = TEXT_CHUNK_SIZE - eventHeader.Length;
            
            if (remainingSpace > 100) // Make sure we have enough space for meaningful content
            {
                // Split the description into smaller pieces
                var words = calendarEvent.Description.Split(' ');
                var currentChunk = eventHeader;
                
                foreach (var word in words)
                {
                    if (currentChunk.Length + word.Length + 1 <= TEXT_CHUNK_SIZE)
                    {
                        currentChunk += word + " ";
                    }
                    else
                    {
                        // Current chunk is full, start a new one
                        chunks.Add(currentChunk.Trim());
                        currentChunk = eventHeader + word + " ";
                    }
                }
                
                // Don't forget the last chunk
                if (currentChunk.Length > eventHeader.Length)
                {
                    chunks.Add(currentChunk.Trim());
                }
            }
            else
            {
                // If header is too long, just use a truncated version
                chunks.Add(fullDescription.Substring(0, Math.Min(TEXT_CHUNK_SIZE, fullDescription.Length)));
            }

            return chunks;
        }
    }
} 