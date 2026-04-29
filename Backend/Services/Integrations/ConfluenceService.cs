using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Backend.Services.Integrations
{
    public class ConfluenceDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string TinyLink { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class ConfluenceService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly string _baseUrl;
        private readonly string _username;
        private readonly string _password;
        private readonly string[] _spaceKeys;

        public ConfluenceService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;

            _baseUrl = (_config["Confluence:BaseUrl"] ?? "").Trim();
            _username = (_config["Confluence:Username"] ?? "").Trim();
            _password = (_config["Confluence:Password"] ?? "").Trim(); // Must be Atlassian API token for cloud
            var spaceKeyConfig = (_config["Confluence:SpaceKey"] ?? "").Trim();

            // Support multiple space keys separated by commas
            _spaceKeys = spaceKeyConfig.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim())
                                     .Where(s => !string.IsNullOrEmpty(s))
                                     .ToArray();

            // Optional inline override for diagnostics (set via configuration)
            // Set Confluence:ForceInline=true and provide Confluence:InlineBaseUrl, InlineUsername, InlinePassword, InlineSpaceKey
            if (bool.TryParse(_config["Confluence:ForceInline"], out var forceInline) && forceInline)
            {
                var inlineBaseUrl = (_config["Confluence:InlineBaseUrl"] ?? _baseUrl).Trim();
                var inlineUsername = (_config["Confluence:InlineUsername"] ?? _username).Trim();
                var inlinePassword = (_config["Confluence:InlinePassword"] ?? _password).Trim();
                var inlineSpaceKeyConfig = (_config["Confluence:InlineSpaceKey"] ?? spaceKeyConfig).Trim();

                _baseUrl = inlineBaseUrl;
                _username = inlineUsername;
                _password = inlinePassword;
                _spaceKeys = inlineSpaceKeyConfig.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                               .Select(s => s.Trim())
                                               .Where(s => !string.IsNullOrEmpty(s))
                                               .ToArray();

                Console.WriteLine("WARNING: Using inline Confluence credentials override (for diagnostics)");
            }

            // Basic validation to surface misconfiguration early
            if (string.IsNullOrWhiteSpace(_baseUrl))
            {
                Console.WriteLine("Confluence config error: BaseUrl is not set (Confluence:BaseUrl)");
            }
            if (string.IsNullOrWhiteSpace(_username))
            {
                Console.WriteLine("Confluence config error: Username is not set (Confluence:Username)");
            }
            if (string.IsNullOrWhiteSpace(_password))
            {
                Console.WriteLine("Confluence config error: API token is not set (Confluence:Password)");
            }
            if (_spaceKeys.Length == 0)
            {
                Console.WriteLine("Confluence config error: SpaceKey is not set (Confluence:SpaceKey)");
            }

            // Log effective config (mask secret)
            Console.WriteLine($"Confluence config → BaseUrl={_baseUrl}, Username={_username}, SpaceKeys=[{string.Join(", ", _spaceKeys)}], TokenSet={!string.IsNullOrWhiteSpace(_password)}");

            // Set up basic authentication (email + API token)
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<bool> ValidateCredentialsAsync()
        {
            try
            {
                var url = $"{_baseUrl}/rest/api/user/current";
                Console.WriteLine($"Validating Confluence credentials via: {url}");
                var resp = await _httpClient.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    Console.WriteLine("Confluence auth validation: SUCCESS");
                    return true;
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    Console.WriteLine($"Confluence auth validation FAILED ({resp.StatusCode}): {body}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Confluence auth validation error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<ConfluenceDocument>> FetchAllDocumentsAsync()
        {
            var documents = new List<ConfluenceDocument>();

            try
            {
                // Guard: if any required config is missing, short-circuit
                if (string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password) || _spaceKeys.Length == 0)
                {
                    Console.WriteLine("Confluence fetch aborted due to missing configuration. Check previous log lines.");
                    return documents;
                }

                // Fetch all pages from all spaces
                var allPages = new List<ConfluenceDocument>();

                foreach (var spaceKey in _spaceKeys)
                {
                    Console.WriteLine($"Fetching pages from space: {spaceKey}");
                    var pagesFromSpace = await FetchAllPagesFromSpaceAsync(spaceKey);
                    allPages.AddRange(pagesFromSpace);
                    Console.WriteLine($"Total pages fetched from {spaceKey}: {pagesFromSpace.Count}");
                }

                Console.WriteLine($"Total pages fetched from all spaces: {allPages.Count}");

                foreach (var page in allPages)
                {
                    // Check if page is internal only
                    var isInternal = await CheckInternalLabelAsync(page.Id);
                    if (isInternal)
                    {
                        Console.WriteLine($"Skipping internal page: {page.Title}");
                        continue;
                    }

                    // Fetch page content
                    var content = await FetchPageContentAsync(page.Id);
                    if (!string.IsNullOrEmpty(content))
                    {
                        page.Content = CleanHtmlContent(content);
                        documents.Add(page);
                    }
                }

                Console.WriteLine($"Final documents after filtering: {documents.Count}");
                return documents;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching documents: {ex.Message}");
                return documents;
            }
        }

        private async Task<List<ConfluenceDocument>> FetchAllPagesFromSpaceAsync(string spaceKey)
        {
            var pages = new List<ConfluenceDocument>();
            var start = 0;
            var limit = 100;

            while (true)
            {
                var url = $"{_baseUrl}/rest/api/content?spaceKey={spaceKey}&type=page&limit={limit}&start={start}&expand=body.storage";

                try
                {
                    Console.WriteLine($"Fetching Confluence pages: {url}");
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var data = JsonSerializer.Deserialize<JsonElement>(json);

                        if (data.TryGetProperty("results", out var results))
                        {
                            var pageArray = JsonSerializer.Deserialize<JsonElement[]>(results.GetRawText());
                            if (pageArray != null)
                            {
                                foreach (var pageElement in pageArray)
                                {
                                    var page = ParsePageFromJson(pageElement);
                                    if (page != null)
                                    {
                                        pages.Add(page);
                                    }
                                }
                            }
                        }

                        // Pagination: advance if a next link is present
                        if (data.TryGetProperty("_links", out var links) &&
                            links.TryGetProperty("next", out var next))
                        {
                            start += limit;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Confluence API error ({response.StatusCode}) while listing pages from space {spaceKey}: {body}");
                        // Break to avoid tight loop on persistent errors
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in FetchAllPagesFromSpaceAsync for space {spaceKey}: {ex.Message}");
                    break;
                }
            }

            return pages;
        }

        private ConfluenceDocument? ParsePageFromJson(JsonElement pageElement)
        {
            try
            {
                var page = new ConfluenceDocument
                {
                    Id = pageElement.GetProperty("id").GetString() ?? "",
                    Type = pageElement.GetProperty("type").GetString() ?? "",
                    Status = pageElement.GetProperty("status").GetString() ?? "",
                    Title = pageElement.GetProperty("title").GetString() ?? ""
                };

                // Parse links
                if (pageElement.TryGetProperty("_links", out var links) &&
                    links.TryGetProperty("tinyui", out var tinyui))
                {
                    page.TinyLink = tinyui.GetString() ?? "";
                }

                // Parse dates
                if (pageElement.TryGetProperty("created", out var created))
                {
                    page.CreatedDate = DateTime.Parse(created.GetString() ?? DateTime.Now.ToString());
                }

                if (pageElement.TryGetProperty("lastmodified", out var modified))
                {
                    page.LastModified = DateTime.Parse(modified.GetString() ?? DateTime.Now.ToString());
                }

                return page;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing page: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> CheckInternalLabelAsync(string pageId)
        {
            try
            {
                var url = $"{_baseUrl}/rest/api/content/{pageId}/label";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(json);

                    if (data.TryGetProperty("results", out var results))
                    {
                        var labels = JsonSerializer.Deserialize<JsonElement[]>(results.GetRawText());
                        if (labels != null)
                        {
                            foreach (var label in labels)
                            {
                                if (label.TryGetProperty("name", out var name) &&
                                    name.GetString() == "internal_only")
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Confluence API error ({response.StatusCode}) checking labels for page {pageId}: {body}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking internal label for {pageId}: {ex.Message}");
            }

            return false;
        }

        private async Task<string?> FetchPageContentAsync(string pageId)
        {
            try
            {
                var url = $"{_baseUrl}/rest/api/content/{pageId}?expand=body.storage";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(json);
                    if (data.TryGetProperty("body", out var body) &&
                        body.TryGetProperty("storage", out var storage) &&
                        storage.TryGetProperty("value", out var value))
                    {
                        return value.GetString();
                    }
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Confluence API error ({response.StatusCode}) fetching content for page {pageId}: {body}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching page content for {pageId}: {ex.Message}");
            }

            return null;
        }

        private string CleanHtmlContent(string htmlContent)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                // Remove script and style elements
                var scriptNodes = doc.DocumentNode.SelectNodes("//script");
                if (scriptNodes != null)
                {
                    foreach (var node in scriptNodes)
                    {
                        node.Remove();
                    }
                }

                var styleNodes = doc.DocumentNode.SelectNodes("//style");
                if (styleNodes != null)
                {
                    foreach (var node in styleNodes)
                    {
                        node.Remove();
                    }
                }

                // Get text content
                var text = doc.DocumentNode.InnerText;

                // Clean up whitespace
                text = Regex.Replace(text, @"\s+", " ");
                text = text.Trim();

                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning HTML content: {ex.Message}");
                return htmlContent;
            }
        }

        public List<string> ChunkContent(string content, int maxChunkSize = 1000, int overlap = 200)
        {
            var chunks = new List<string>();

            if (string.IsNullOrEmpty(content))
                return chunks;

            var sentences = Regex.Split(content, @"(?<=[.!?])\s+");
            var currentChunk = new StringBuilder();

            foreach (var sentence in sentences)
            {
                if (currentChunk.Length + sentence.Length > maxChunkSize && currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();

                    // Add overlap from previous chunk
                    var lastChunk = chunks.Last();
                    if (lastChunk.Length > overlap)
                    {
                        var overlapText = lastChunk.Substring(lastChunk.Length - overlap);
                        currentChunk.Append(overlapText + " ");
                    }
                }

                currentChunk.Append(sentence + " ");
            }

            // Add the last chunk
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks;
        }
    }
}