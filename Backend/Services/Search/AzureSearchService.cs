using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;

namespace Backend.Services.Search
{
    /// <summary>
    /// Main service for searching through documents using Azure Cognitive Search.
    /// This service orchestrates document processing, search operations, and content management.
    /// Think of this as the "search engine coordinator" that manages all search functionality.
    /// </summary>
    public class AzureSearchService
    {
        // Azure Search clients for different operations
        private readonly SearchClient _searchClient;             // For searching documents
        private readonly SearchIndexClient _indexClient;         // For managing the search index
        private readonly IConfiguration _configuration;          // Application configuration

        // Helper services for clean separation of concerns
        private readonly DocumentProcessor _documentProcessor;
        private readonly ContentChunker _contentChunker;
        private readonly SearchQueryBuilder _searchQueryBuilder;

        /// <summary>
        /// Creates a new AzureSearchService with the necessary Azure Search configuration and helper services
        /// </summary>
        /// <param name="configuration">Application configuration containing Azure Search settings</param>
        public AzureSearchService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Read Azure Search connection details from configuration
            var endpoint = configuration["AzureSearch:Endpoint"] ??
                throw new ArgumentNullException("AzureSearch:Endpoint", "Azure Search endpoint is not configured");
            var indexName = configuration["AzureSearch:IndexName"] ??
                throw new ArgumentNullException("AzureSearch:IndexName", "Azure Search index name is not configured");
            var apiKey = configuration["AzureSearch:ApiKey"] ??
                throw new ArgumentNullException("AzureSearch:ApiKey", "Azure Search API key is not configured");

            // Create clients for searching and managing the search index
            var searchEndpoint = new Uri(endpoint);
            var searchCredential = new AzureKeyCredential(apiKey);
            _searchClient = new SearchClient(searchEndpoint, indexName, searchCredential);
            _indexClient = new SearchIndexClient(searchEndpoint, searchCredential);

            // Initialize helper services
            _documentProcessor = new DocumentProcessor(configuration);
            _contentChunker = new ContentChunker();
            _searchQueryBuilder = new SearchQueryBuilder();
        }

        /// <summary>
        /// Searches for relevant text chunks based on a user's query.
        /// This is the main search method that finds information to help answer questions.
        /// </summary>
        /// <param name="userQuery">The search query from the user</param>
        /// <returns>List of relevant text chunks that might contain the answer</returns>
        public async Task<List<string>> SearchRelevantChunksAsync(string userQuery)
        {
            try
            {
                // Validate the query first
                var queryValidation = _searchQueryBuilder.ValidateQuery(userQuery);
                if (!queryValidation.isValid)
                {
                    Console.WriteLine($"Invalid query: {queryValidation.errorMessage}");
                    return new List<string>();
                }

                // Configure the search to get the most relevant results
                var searchOptions = _searchQueryBuilder.CreateStandardSearchOptions();

                // Perform the search using Azure Search
                var searchResponse = await _searchClient.SearchAsync<SearchDocument>(userQuery, searchOptions).ConfigureAwait(false);
                var searchResults = searchResponse.Value.GetResults();

                var relevantTextChunks = new List<string>();

                // Extract text content from each search result
                foreach (var searchResult in searchResults)
                {
                    var textContent = ExtractTextContentFromSearchResult(searchResult);
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        relevantTextChunks.Add(textContent);
                    }
                }

                return relevantTextChunks;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Azure Search error: {exception.Message}");
                // Return empty list if search fails instead of crashing
                return new List<string>();
            }
        }

        /// <summary>
        /// Searches for relevant chunks and includes information about the source type.
        /// This version is smarter about where to search based on the user's query and uploaded documents.
        /// </summary>
        /// <param name="userQuery">The user's search query</param>
        /// <returns>Search results with source information</returns>
        public async Task<SearchResultWithSource> SearchRelevantChunksWithSourceAsync(string userQuery)
        {
            try
            {
                // Validate the query first
                var queryValidation = _searchQueryBuilder.ValidateQuery(userQuery);
                if (!queryValidation.isValid)
                {
                    Console.WriteLine($"Invalid query: {queryValidation.errorMessage}");
                    return new SearchResultWithSource
                    {
                        Chunks = new List<string>(),
                        Source = "none",
                        HasUserDocuments = false
                    };
                }

                // Check if the user has uploaded their own documents
                bool userHasUploadedDocuments = await HasUserUploadedDocumentsAsync();

                // Get the optimal search strategy
                var searchStrategy = _searchQueryBuilder.GetSearchStrategy(userQuery, userHasUploadedDocuments);

                // Try primary source first
                var primaryResults = await SearchInSpecificSourceAsync(userQuery, searchStrategy.PrimarySource);

                if (primaryResults.Any())
                {
                    return new SearchResultWithSource
                    {
                        Chunks = primaryResults,
                        Source = searchStrategy.PrimarySource,
                        HasUserDocuments = userHasUploadedDocuments
                    };
                }

                // Try fallback sources if primary didn't yield results
                foreach (var fallbackSource in searchStrategy.FallbackSources)
                {
                    var fallbackResults = await SearchInSpecificSourceAsync(userQuery, fallbackSource);
                    if (fallbackResults.Any())
                    {
                        return new SearchResultWithSource
                        {
                            Chunks = fallbackResults,
                            Source = fallbackSource,
                            HasUserDocuments = userHasUploadedDocuments
                        };
                    }
                }

                // If no results found in any source
                return new SearchResultWithSource
                {
                    Chunks = new List<string>(),
                    Source = searchStrategy.PrimarySource,
                    HasUserDocuments = userHasUploadedDocuments
                };
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Azure Search with source error: {exception.Message}");
                return new SearchResultWithSource
                {
                    Chunks = new List<string>(),
                    Source = "none",
                    HasUserDocuments = false
                };
            }
        }

        /// <summary>
        /// Searches for documents from a specific source type (like calendar, uploaded files, or Confluence)
        /// </summary>
        /// <param name="userQuery">The user's search query</param>
        /// <param name="sourceType">Which source to search in (calendar, uploaded, confluence)</param>
        /// <returns>List of relevant text chunks from that specific source</returns>
        public async Task<List<string>> SearchInSpecificSourceAsync(string userQuery, string sourceType)
        {
            try
            {
                // Optimize the query for the specific source type
                var optimizedQuery = _searchQueryBuilder.OptimizeQuery(userQuery, sourceType);

                // Configure search with optimization for aggregate queries
                var searchOptions = _searchQueryBuilder.CreateFilteredSearchOptions(sourceType, userQuery);

                var searchResponse = await _searchClient.SearchAsync<SearchDocument>(optimizedQuery, searchOptions).ConfigureAwait(false);
                var searchResults = searchResponse.Value.GetResults();

                var relevantChunks = new List<string>();
                foreach (var searchResult in searchResults)
                {
                    var textContent = ExtractTextContentFromSearchResult(searchResult);
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        relevantChunks.Add(textContent);
                    }
                }

                return relevantChunks;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error searching in source '{sourceType}': {exception.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Searches for content in a specific source with date-aware filtering
        /// This ensures date-based queries only return content from documents matching the requested date
        /// </summary>
        /// <param name="userQuery">The user's search query</param>
        /// <param name="sourceType">Which source to search in (calendar, uploaded, confluence)</param>
        /// <returns>List of relevant text chunks from that specific source, filtered by date if applicable</returns>
        public async Task<List<string>> SearchInSpecificSourceWithDateFilterAsync(string userQuery, string sourceType)
        {
            try
            {
                // Optimize the query for the specific source type
                var optimizedQuery = _searchQueryBuilder.OptimizeQuery(userQuery, sourceType);

                // Configure search with date-aware filtering for better accuracy
                var searchOptions = _searchQueryBuilder.CreateDateAwareFilteredSearchOptions(sourceType, userQuery);

                var searchResponse = await _searchClient.SearchAsync<SearchDocument>(optimizedQuery, searchOptions).ConfigureAwait(false);
                var searchResults = searchResponse.Value.GetResults();

                var relevantChunks = new List<string>();
                foreach (var searchResult in searchResults)
                {
                    var textContent = ExtractTextContentFromSearchResult(searchResult);
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        relevantChunks.Add(textContent);
                    }
                }

                return relevantChunks;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error searching in source '{sourceType}' with date filter: {exception.Message}");
                return new List<string>();
            }
        }

        public async Task<bool> HasUserUploadedDocumentsAsync()
        {
            try
            {
                // First check blob storage for actual files (authoritative source)
                var blobClient = new BlobContainerClient(_configuration["Azure:BlobStorageConnectionString"], _configuration["Azure:BlobContainer"]);
                var hasActualFiles = false;

                await foreach (var blobItem in blobClient.GetBlobsAsync())
                {
                    // Only count user-uploaded files, not system files
                    if (!blobItem.Name.StartsWith("confluence/") &&
                        !blobItem.Name.StartsWith("_sync_status") &&
                        !blobItem.Name.Contains("_metadata"))
                    {
                        hasActualFiles = true;
                        break;
                    }
                }

                // If no actual files in blob storage, return false regardless of search index
                if (!hasActualFiles)
                {
                    Console.WriteLine("No user files found in blob storage");
                    return false;
                }

                // If files exist in blob storage, verify they're properly indexed
                var options = new SearchOptions
                {
                    IncludeTotalCount = true,
                    Size = 0,
                    Filter = "source eq 'uploaded'"
                };

                var response = await _searchClient.SearchAsync<SearchDocument>("*", options).ConfigureAwait(false);
                var indexedCount = response.Value.TotalCount ?? 0;

                Console.WriteLine($"Found {(hasActualFiles ? "files in blob storage" : "no files in blob storage")} and {indexedCount} indexed chunks");
                return hasActualFiles && indexedCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking user documents: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the total number of documents in the search index
        /// </summary>
        /// <returns>Total document count</returns>
        public async Task<long> GetTotalDocumentCountAsync()
        {
            var options = _searchQueryBuilder.CreateCountingSearchOptions();
            var response = await _searchClient.SearchAsync<SearchDocument>("*", options).ConfigureAwait(false);
            return response.Value.TotalCount ?? 0;
        }

        /// <summary>
        /// Gets document counts by source type
        /// </summary>
        /// <returns>Dictionary with source types and their document counts</returns>
        public async Task<Dictionary<string, long>> GetDocumentCountBySourceAsync()
        {
            var totalCount = await GetTotalDocumentCountAsync();
            return new Dictionary<string, long>
            {
                { "documents", totalCount }
            };
        }

        /// <summary>
        /// Processes and indexes a document for search
        /// </summary>
        /// <param name="fileName">Name of the file to process</param>
        /// <param name="blobUrl">URL where the file is stored</param>
        public async Task ProcessDocumentAsync(string fileName, string blobUrl)
        {
            try
            {
                // Extract text content from the document using the document processor
                string documentContent = await _documentProcessor.ExtractTextFromDocument(fileName, blobUrl);

                if (string.IsNullOrWhiteSpace(documentContent))
                {
                    // Fallback to metadata if text extraction fails
                    documentContent = $"Document: {fileName} - Uploaded from {blobUrl}";
                }

                // Chunk the content for better search using the content chunker
                var chunks = _contentChunker.ChunkContent(documentContent);

                // Index each chunk separately
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var chunkId = Guid.NewGuid().ToString();

                    var document = new SearchDocument
                    {
                        ["id"] = chunkId,
                        ["content"] = chunk,
                        ["source"] = "uploaded",
                        ["filename"] = fileName
                    };

                    var batch = IndexDocumentsBatch.Create<SearchDocument>(IndexDocumentsAction.Upload(document));
                    await _searchClient.IndexDocumentsAsync(batch);
                }

                Console.WriteLine($"Processed document {fileName} into {chunks.Count} chunks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing document {fileName}: {ex.Message}");
                // Create a fallback entry
                var document = new SearchDocument
                {
                    ["id"] = Guid.NewGuid().ToString(),
                    ["content"] = $"Document: {fileName} - Uploaded from {blobUrl} (Processing failed: {ex.Message})",
                    ["source"] = "uploaded",
                    ["filename"] = fileName
                };

                var batch = IndexDocumentsBatch.Create<SearchDocument>(IndexDocumentsAction.Upload(document));
                await _searchClient.IndexDocumentsAsync(batch);
            }
        }

                public async Task DeleteDocumentAsync(string fileName)
        {
            try
            {
                // Search for documents with the given filename
                var options = new SearchOptions
                {
                    Filter = $"filename eq '{fileName}'"
                };

                var response = await _searchClient.SearchAsync<SearchDocument>("*", options);
                var results = response.Value.GetResults();

                if (results.Any())
                {
                    var deleteActions = results
                        .Select(r => CreateDeleteAction(r))
                        .Where(action => action != null)
                        .ToList();

                    if (deleteActions.Any())
                    {
                        var batch = IndexDocumentsBatch.Create(deleteActions.ToArray());
                        await _searchClient.IndexDocumentsAsync(batch);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting document {fileName}: {ex.Message}");
                // Don't throw - just log the error
            }
        }

        private IndexDocumentsAction<SearchDocument>? CreateDeleteAction(SearchResult<SearchDocument> result)
        {
            if (result.Document.TryGetValue("id", out var id) && id != null)
            {
                return IndexDocumentsAction.Delete(new SearchDocument { ["id"] = id.ToString() });
            }

            if (result.Document.TryGetValue("documentId", out var documentId) && documentId != null)
            {
                return IndexDocumentsAction.Delete(new SearchDocument { ["id"] = documentId.ToString() });
            }

            return null;
        }

        /// <summary>
        /// Cleans up orphaned uploaded documents from search index that no longer exist in blob storage
        /// </summary>
        public async Task CleanupOrphanedDocumentsAsync()
        {
            try
            {
                // Get all uploaded documents from search index
                var searchOptions = new SearchOptions
                {
                    Filter = "source eq 'uploaded'",
                    Size = 100
                };

                var searchResponse = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions);
                var indexDocuments = searchResponse.Value.GetResults().ToList();

                if (!indexDocuments.Any()) return;

                // Get list of actual files in blob storage
                var blobClient = new BlobContainerClient(_configuration["Azure:BlobStorageConnectionString"], _configuration["Azure:BlobContainer"]);
                var actualFiles = new HashSet<string>();

                await foreach (var blobItem in blobClient.GetBlobsAsync())
                {
                    if (!blobItem.Name.StartsWith("confluence/") && !blobItem.Name.StartsWith("_sync_status"))
                    {
                        actualFiles.Add(blobItem.Name);
                    }
                }

                // Find orphaned documents (in search index but not in blob storage)
                var deleteActions = new List<IndexDocumentsAction<SearchDocument>>();

                foreach (var doc in indexDocuments)
                {
                    if (doc.Document.TryGetValue("filename", out var filename) && filename != null)
                    {
                        if (!actualFiles.Contains(filename.ToString()))
                        {
                            var deleteAction = CreateDeleteAction(doc);
                            if (deleteAction != null)
                            {
                                deleteActions.Add(deleteAction);
                            }
                        }
                    }
                }

                // Delete orphaned documents
                if (deleteActions.Any())
                {
                    var batch = IndexDocumentsBatch.Create(deleteActions.ToArray());
                    await _searchClient.IndexDocumentsAsync(batch);
                    Console.WriteLine($"Cleaned up {deleteActions.Count} orphaned document chunks");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up orphaned documents: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up orphaned uploaded documents from the search index
        /// This removes any "uploaded" documents that no longer exist in blob storage
        /// </summary>
        public async Task CleanupOrphanedUploadedDocumentsAsync()
        {
            try
            {
                Console.WriteLine("Starting cleanup of orphaned uploaded documents...");

                // Get all uploaded documents from search index
                var searchOptions = _searchQueryBuilder.CreateFilteredSearchOptions("uploaded", 1000);
                var response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions);
                var results = response.Value.GetResults();

                if (!results.Any())
                {
                    Console.WriteLine("No uploaded documents found in search index");
                    return;
                }

                // Get list of actual files in blob storage
                var blobClient = new BlobContainerClient(_configuration["Azure:BlobStorageConnectionString"], _configuration["Azure:BlobContainer"]);
                var existingFiles = new HashSet<string>();

                await foreach (var blobItem in blobClient.GetBlobsAsync())
                {
                    if (!blobItem.Name.StartsWith("confluence/") &&
                        !blobItem.Name.StartsWith("_sync_status") &&
                        !blobItem.Name.Contains("_metadata"))
                    {
                        existingFiles.Add(blobItem.Name);
                    }
                }

                Console.WriteLine($"Found {existingFiles.Count} files in blob storage and {results.Count()} uploaded document chunks in search index");

                // Find orphaned documents (in search index but not in blob storage)
                var orphanedActions = new List<IndexDocumentsAction<SearchDocument>>();

                foreach (var result in results)
                {
                    if (result.Document.TryGetValue("filename", out var filenameObj) && filenameObj != null)
                    {
                        var filename = filenameObj.ToString();
                        if (!string.IsNullOrEmpty(filename) && !existingFiles.Contains(filename))
                        {
                            // This document chunk is orphaned - file no longer exists
                            if (result.Document.TryGetValue("id", out var idObj) && idObj != null)
                            {
                                orphanedActions.Add(IndexDocumentsAction.Delete(new SearchDocument { ["id"] = idObj.ToString() }));
                            }
                        }
                    }
                }

                if (orphanedActions.Any())
                {
                    Console.WriteLine($"Found {orphanedActions.Count} orphaned document chunks to delete");
                    var batch = IndexDocumentsBatch.Create<SearchDocument>(orphanedActions.ToArray());
                    await _searchClient.IndexDocumentsAsync(batch);
                    Console.WriteLine("Successfully deleted orphaned document chunks");
                }
                else
                {
                    Console.WriteLine("No orphaned document chunks found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during orphaned document cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts text content from a search result, trying different possible field names
        /// </summary>
        /// <param name="searchResult">A single search result from Azure Search</param>
        /// <returns>The text content, or empty string if none found</returns>
        private string ExtractTextContentFromSearchResult(SearchResult<SearchDocument> searchResult)
        {
            // Try to get content from various possible field names in the search index
            // Different document types might store content in different fields

            if (searchResult.Document.TryGetValue("content", out var content) && content != null)
            {
                return content.ToString();
            }
            else if (searchResult.Document.TryGetValue("text", out var text) && text != null)
            {
                return text.ToString();
            }
            else if (searchResult.Document.TryGetValue("body", out var body) && body != null)
            {
                return body.ToString();
            }
            else
            {
                // If no specific content field found, convert the entire document to string as fallback
                return searchResult.Document.ToString();
            }
        }
    }

    // Keep the existing result classes for backward compatibility

    /// <summary>
    /// Represents a search result from Azure Search with all the important information
    /// </summary>
    public class SearchResult
    {
        public string Content { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public double? Score { get; set; }
    }

    /// <summary>
    /// Contains search results along with information about the source and user documents
    /// </summary>
    public class SearchResultWithSource
    {
        public List<string> Chunks { get; set; } = new List<string>();
        public string Source { get; set; } = string.Empty;
        public bool HasUserDocuments { get; set; }
    }
}
