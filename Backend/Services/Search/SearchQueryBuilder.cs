using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace Backend.Services.Search
{
    /// <summary>
    /// Service responsible for building and configuring search queries for Azure Search.
    /// This handles source type detection, query optimization, and search options configuration.
    /// </summary>
    public class SearchQueryBuilder
    {
        // Constants for search behavior
        private const int DEFAULT_SEARCH_RESULTS_COUNT = 10;     // How many search results to return by default
        private const int AGGREGATE_SEARCH_RESULTS_COUNT = 50;   // Higher count for aggregate/comparative queries
        private const int DATE_AWARE_SEARCH_RESULTS_COUNT = 25;  // Higher count for date-specific queries to get complete meeting notes

        /// <summary>
        /// Creates standard search options for general queries
        /// </summary>
        /// <param name="resultsCount">Number of results to return</param>
        /// <returns>Configured SearchOptions</returns>
        public SearchOptions CreateStandardSearchOptions(int resultsCount = DEFAULT_SEARCH_RESULTS_COUNT)
        {
            return new SearchOptions()
            {
                IncludeTotalCount = true,
                Size = resultsCount
            };
        }

        /// <summary>
        /// Creates search options optimized for the query type (aggregate vs standard)
        /// </summary>
        /// <param name="query">User query to analyze</param>
        /// <param name="resultsCount">Optional override for result count</param>
        /// <returns>Optimized SearchOptions</returns>
        public SearchOptions CreateOptimizedSearchOptions(string query, int? resultsCount = null)
        {
            var isAggregateQuery = IsAggregateQuery(query);
            var defaultCount = isAggregateQuery ? AGGREGATE_SEARCH_RESULTS_COUNT : DEFAULT_SEARCH_RESULTS_COUNT;
            var finalCount = resultsCount ?? defaultCount;

            return new SearchOptions()
            {
                IncludeTotalCount = true,
                Size = finalCount
            };
        }

        /// <summary>
        /// Creates search options filtered by source type with query optimization
        /// </summary>
        /// <param name="sourceType">Type of source to filter by</param>
        /// <param name="query">User query for optimization</param>
        /// <param name="resultsCount">Optional override for result count</param>
        /// <returns>Configured SearchOptions with source filter and optimization</returns>
        public SearchOptions CreateFilteredSearchOptions(string sourceType, string query, int? resultsCount = null)
        {
            var isAggregateQuery = IsAggregateQuery(query);
            var defaultCount = isAggregateQuery ? AGGREGATE_SEARCH_RESULTS_COUNT : DEFAULT_SEARCH_RESULTS_COUNT;
            var finalCount = resultsCount ?? defaultCount;

            return new SearchOptions()
            {
                IncludeTotalCount = true,
                Size = finalCount,
                Filter = $"source eq '{sourceType}'"
            };
        }

        /// <summary>
        /// Creates search options filtered by source type and date patterns in document titles
        /// This helps ensure date-based queries only return content from documents matching the requested date
        /// </summary>
        /// <param name="sourceType">Type of source to filter by</param>
        /// <param name="query">User query for optimization and date extraction</param>
        /// <param name="resultsCount">Optional override for result count</param>
        /// <returns>Configured SearchOptions with source and date filters</returns>
        public SearchOptions CreateDateAwareFilteredSearchOptions(string sourceType, string query, int? resultsCount = null)
        {
            var isAggregateQuery = IsAggregateQuery(query);
            var defaultCount = isAggregateQuery ? AGGREGATE_SEARCH_RESULTS_COUNT : DATE_AWARE_SEARCH_RESULTS_COUNT;
            var finalCount = resultsCount ?? defaultCount;

            var filter = $"source eq '{sourceType}'";
            
            // Extract date patterns from query and add title-based filtering
            var dateFilter = ExtractDateFilterFromQuery(query);
            if (!string.IsNullOrEmpty(dateFilter))
            {
                filter += $" and ({dateFilter})";
            }

            return new SearchOptions()
            {
                IncludeTotalCount = true,
                Size = finalCount,
                Filter = filter
            };
        }

        /// <summary>
        /// Creates search options filtered by source type (legacy method for backward compatibility)
        /// </summary>
        /// <param name="sourceType">Type of source to filter by</param>
        /// <param name="resultsCount">Number of results to return</param>
        /// <returns>Configured SearchOptions with source filter</returns>
        public SearchOptions CreateFilteredSearchOptions(string sourceType, int resultsCount = DEFAULT_SEARCH_RESULTS_COUNT)
        {
            return new SearchOptions()
            {
                IncludeTotalCount = true,
                Size = resultsCount,
                Filter = $"source eq '{sourceType}'"
            };
        }

        /// <summary>
        /// Creates search options for counting documents by source
        /// </summary>
        /// <param name="sourceType">Source type to count, or null for all documents</param>
        /// <returns>SearchOptions configured for counting</returns>
        public SearchOptions CreateCountingSearchOptions(string? sourceType = null)
        {
            var options = new SearchOptions()
            {
                IncludeTotalCount = true,
                Size = 0  // We only want the count, not the actual documents
            };

            if (!string.IsNullOrEmpty(sourceType))
            {
                options.Filter = $"source eq '{sourceType}'";
            }

            return options;
        }

        /// <summary>
        /// Detects if a query requires aggregate/comparative analysis
        /// </summary>
        /// <param name="query">User query to analyze</param>
        /// <returns>True if the query is asking for comparisons or aggregations</returns>
        public bool IsAggregateQuery(string query)
        {
            var queryLower = query.ToLower();
            var aggregateKeywords = new[]
            {
                // Superlatives and comparatives
                "highest", "lowest", "most", "least", "fewest", "best", "worst",
                "largest", "smallest", "biggest", "greatest", "maximum", "minimum",
                "top", "bottom", "first", "last", "longest", "shortest",
                
                // Comparison terms
                "compare", "comparison", "versus", "vs", "between", "difference",
                "more than", "less than", "greater than", "better than", "worse than",
                
                // Ranking and ordering
                "rank", "ranking", "order", "sort", "list all", "show all",
                "which one", "which person", "which company", "which team",
                
                // Counting and totaling
                "how many", "count", "total", "sum", "number of", "amount of",
                "all the", "every", "each", "who all", "what all",
                
                // Statistical terms
                "average", "median", "common", "frequent", "popular",
                "typical", "standard", "normal", "unusual", "rare"
            };

            return aggregateKeywords.Any(keyword => queryLower.Contains(keyword));
        }

        /// <summary>
        /// Determines if a query is calendar-related based on keywords
        /// </summary>
        /// <param name="query">User query to analyze</param>
        /// <returns>True if the query appears to be calendar-related</returns>
        public bool IsCalendarRelatedQuery(string query)
        {
            var queryLower = query.ToLower();
            var calendarKeywords = new[]
            {
                "today", "tomorrow", "this week", "next week",
                "calendar", "event", "meeting", "schedule",
                "appointment", "when is", "what time",
                "office closed", "holiday", "vacation",
                "what's happening", "events today",
                "meetings today", "what do we have"
            };

            return calendarKeywords.Any(keyword => queryLower.Contains(keyword));
        }

        /// <summary>
        /// Determines if a query could potentially be calendar-related
        /// </summary>
        /// <param name="query">User query to analyze</param>
        /// <returns>True if the query might be calendar-related</returns>
        public bool CouldBeCalendarRelated(string query)
        {
            var queryLower = query.ToLower();
            var possibleCalendarKeywords = new[]
            {
                "when", "what", "time", "date", "day",
                "week", "month", "schedule", "plan",
                "available", "free", "busy", "open"
            };

            return possibleCalendarKeywords.Any(keyword => queryLower.Contains(keyword));
        }

        /// <summary>
        /// Analyzes a query to determine the best source type to search
        /// </summary>
        /// <param name="query">User query to analyze</param>
        /// <param name="hasUserDocuments">Whether user has uploaded documents</param>
        /// <returns>Recommended source type for the query</returns>
        public string DetermineOptimalSourceType(string query, bool hasUserDocuments)
        {
            // Calendar queries get highest priority
            if (IsCalendarRelatedQuery(query))
            {
                return "calendar";
            }

            // If user has documents, prioritize those for most queries
            if (hasUserDocuments)
            {
                return "uploaded";
            }

            // Default to confluence for company information
            return "confluence";
        }

        /// <summary>
        /// Gets search strategy recommendations based on query analysis
        /// </summary>
        /// <param name="query">User query to analyze</param>
        /// <param name="hasUserDocuments">Whether user has uploaded documents</param>
        /// <returns>Search strategy with primary and fallback sources</returns>
        public SearchStrategy GetSearchStrategy(string query, bool hasUserDocuments)
        {
            var strategy = new SearchStrategy
            {
                IsAggregateQuery = IsAggregateQuery(query)
            };

            if (IsCalendarRelatedQuery(query))
            {
                strategy.PrimarySource = "calendar";
                strategy.FallbackSources = new List<string>();
                strategy.IsSpecialized = true;
            }
            else if (hasUserDocuments)
            {
                strategy.PrimarySource = "uploaded";
                strategy.FallbackSources = new List<string> { "confluence" };
                strategy.IsSpecialized = false;
            }
            else
            {
                strategy.PrimarySource = "confluence";
                strategy.FallbackSources = CouldBeCalendarRelated(query) 
                    ? new List<string> { "calendar" } 
                    : new List<string>();
                strategy.IsSpecialized = false;
            }

            return strategy;
        }

        /// <summary>
        /// Optimizes a search query for better results based on query type
        /// </summary>
        /// <param name="originalQuery">Original user query</param>
        /// <param name="sourceType">Target source type</param>
        /// <returns>Optimized query string</returns>
        public string OptimizeQuery(string originalQuery, string sourceType)
        {
            var optimizedQuery = originalQuery.Trim();
            
            // For aggregate queries, enhance with related terms to find entity-focused chunks
            if (IsAggregateQuery(originalQuery))
            {
                var queryLower = originalQuery.ToLower();
                
                // Add terms to help find entity summaries and counts
                if (sourceType == "uploaded" && (queryLower.Contains("affiliation") || queryLower.Contains("association")))
                {
                    optimizedQuery += " entity summary records count";
                }
                else if (queryLower.Contains("highest") || queryLower.Contains("most") || queryLower.Contains("largest"))
                {
                    optimizedQuery += " maximum total count";
                }
                else if (queryLower.Contains("lowest") || queryLower.Contains("least") || queryLower.Contains("smallest"))
                {
                    optimizedQuery += " minimum total count";
                }
            }
            
            return optimizedQuery;
        }

        /// <summary>
        /// Validates if a search query is appropriate
        /// </summary>
        /// <param name="query">Query to validate</param>
        /// <returns>Validation result with success status and error message</returns>
        public (bool isValid, string errorMessage) ValidateQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return (false, "Query cannot be empty");
            }

            if (query.Trim().Length < 2)
            {
                return (false, "Query is too short. Please provide at least 2 characters.");
            }

            if (query.Length > 1000)
            {
                return (false, "Query is too long. Please limit to 1000 characters.");
            }

            // Check for potentially problematic characters or patterns
            var problematicPatterns = new[] { "SELECT", "DROP", "DELETE", "UPDATE", "INSERT" };
            if (problematicPatterns.Any(pattern => query.ToUpper().Contains(pattern)))
            {
                return (false, "Query contains invalid patterns");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Extracts date patterns from query and creates content-based filters
        /// This looks for date patterns like "August 7th, 2025", "20250807", etc. and creates
        /// filters to search for those patterns within the document content
        /// </summary>
        /// <param name="query">User query to analyze for date patterns</param>
        /// <returns>OData filter string for document content, or empty if no dates found</returns>
        private string ExtractDateFilterFromQuery(string query)
        {
            var filters = new List<string>();
            var queryLower = query.ToLowerInvariant();

            // Pattern 1: "August 7th, 2025" or "August 7, 2025"
            var monthDayYearRegex = new System.Text.RegularExpressions.Regex(
                @"(january|february|march|april|may|june|july|august|september|october|november|december)\s+(\d{1,2})(?:st|nd|rd|th)?,?\s+(\d{4})",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            var monthDayYearMatch = monthDayYearRegex.Match(query);
            if (monthDayYearMatch.Success)
            {
                var month = monthDayYearMatch.Groups[1].Value.ToLowerInvariant();
                var day = int.Parse(monthDayYearMatch.Groups[2].Value);
                var year = monthDayYearMatch.Groups[3].Value;
                
                var monthNum = GetMonthNumber(month);
                
                // Search for multiple date formats in content
                var dateFormats = new[]
                {
                    $"{year}{monthNum:D2}{day:D2}", // 20250807
                    $"{monthNum:D2}/{day:D2}/{year}", // 08/07/2025
                    $"{day:D2}/{monthNum:D2}/{year}", // 07/08/2025 (alternative format)
                    $"{month} {day}, {year}", // august 7, 2025
                    $"{month} {day} {year}" // august 7 2025
                };
                
                var contentFilters = dateFormats.Select(format => 
                    $"search.ismatch('{format}', 'content')").ToArray();
                
                if (contentFilters.Length > 0)
                {
                    filters.Add($"({string.Join(" or ", contentFilters)})");
                }
            }

            // Pattern 2: "YYYYMMDD" format like "20250807"
            var yyyymmddRegex = new System.Text.RegularExpressions.Regex(@"\b(\d{8})\b");
            var yyyymmddMatch = yyyymmddRegex.Match(query);
            if (yyyymmddMatch.Success)
            {
                var dateStr = yyyymmddMatch.Groups[1].Value;
                filters.Add($"search.ismatch('{dateStr}', 'content')");
            }

            return filters.Count > 0 ? string.Join(" and ", filters) : string.Empty;
        }

        /// <summary>
        /// Converts month name to numeric value
        /// </summary>
        /// <param name="monthName">Month name in lowercase</param>
        /// <returns>Month number (1-12)</returns>
        private int GetMonthNumber(string monthName)
        {
            return monthName.ToLowerInvariant() switch
            {
                "january" => 1,
                "february" => 2,
                "march" => 3,
                "april" => 4,
                "may" => 5,
                "june" => 6,
                "july" => 7,
                "august" => 8,
                "september" => 9,
                "october" => 10,
                "november" => 11,
                "december" => 12,
                _ => 1
            };
        }
    }

    /// <summary>
    /// Represents a search strategy with primary and fallback sources
    /// </summary>
    public class SearchStrategy
    {
        public string PrimarySource { get; set; } = "confluence";
        public List<string> FallbackSources { get; set; } = new List<string>();
        public bool IsSpecialized { get; set; } = false;
        public bool IsAggregateQuery { get; set; } = false;
    }
} 