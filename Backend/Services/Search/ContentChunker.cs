using System.Text;

namespace Backend.Services.Search
{
    /// <summary>
    /// Service responsible for intelligently chunking content for better search and AI processing.
    /// This handles different content types and optimizes chunk sizes for AI consumption.
    /// </summary>
    public class ContentChunker
    {
        // Constants for chunking behavior - simplified and reliable
        private const int TEXT_CHUNK_SIZE = 1000;                // Size of text chunks for better AI processing
        private const int EXCEL_ROWS_PER_CHUNK = 20;             // Balanced chunk size for Excel
        private const int MIN_CONTENT_LENGTH_FOR_CHUNKING = 100; // Minimum content length before we split into chunks

        /// <summary>
        /// Intelligently chunks content based on its type and structure
        /// </summary>
        /// <param name="content">The content to chunk</param>
        /// <returns>List of content chunks</returns>
        public List<string> ChunkContent(string content)
        {
            var chunks = new List<string>();

            if (string.IsNullOrWhiteSpace(content))
                return chunks;

            // Enhanced Excel content chunking for better analysis
            if (content.Contains("=== EXCEL DOCUMENT ANALYSIS ==="))
            {
                return ChunkExcelContent(content);
            }
            else
            {
                return ChunkRegularContent(content);
            }
        }

        /// <summary>
        /// Simplified chunking for Excel content that preserves data integrity
        /// </summary>
        /// <param name="content">Excel content to chunk</param>
        /// <returns>List of strategically chunked Excel content</returns>
        private List<string> ChunkExcelContent(string content)
        {
            var chunks = new List<string>();

            // Create strategic chunks that preserve analytical context
            var sections = content.Split(new[] { "=== SHEET:" }, StringSplitOptions.RemoveEmptyEntries);

            // First chunk: Document overview (always include)
            if (sections.Length > 0)
            {
                var overviewSection = sections[0];
                chunks.Add(overviewSection.Trim());
            }

            // Process each sheet with simple, reliable chunking
            for (int i = 1; i < sections.Length; i++)
            {
                var sheetContent = "=== SHEET:" + sections[i];
                var lines = sheetContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                var sheetMetadata = new StringBuilder();
                var detailedDataLines = new List<string>();
                bool inDetailedData = false;

                // Separate metadata from detailed data
                foreach (var line in lines)
                {
                    if (line.Contains("DETAILED DATA:"))
                    {
                        inDetailedData = true;
                        detailedDataLines.Add(line);
                    }
                    else if (!inDetailedData)
                    {
                        sheetMetadata.AppendLine(line);
                    }
                    else
                    {
                        detailedDataLines.Add(line);
                    }
                }

                // Always include sheet metadata as a separate chunk for context
                if (sheetMetadata.Length > 0)
                {
                    chunks.Add(sheetMetadata.ToString().Trim());
                }

                // Simple row-based chunking - no complex entity grouping
                if (detailedDataLines.Any())
                {
                    CreateSimpleDataChunks(chunks, sheetMetadata.ToString(), detailedDataLines);
                }
            }

            return chunks;
        }

        /// <summary>
        /// Creates simple, reliable data chunks without complex grouping logic
        /// </summary>
        /// <param name="chunks">List to add chunks to</param>
        /// <param name="sheetMetadata">Sheet metadata for context</param>
        /// <param name="detailedDataLines">Data lines to chunk</param>
        private void CreateSimpleDataChunks(List<string> chunks, string sheetMetadata, List<string> detailedDataLines)
        {
            var essentialMetadata = ExtractEssentialMetadata(sheetMetadata);
            var dataRows = detailedDataLines.Skip(1).ToList(); // Skip "DETAILED DATA:" header

            if (!dataRows.Any()) return;

            // Simple strategy: Regular row-based chunks ensuring ALL rows are processed
            for (int startIndex = 0; startIndex < dataRows.Count; startIndex += EXCEL_ROWS_PER_CHUNK)
            {
                var chunkRows = dataRows.Skip(startIndex).Take(EXCEL_ROWS_PER_CHUNK).ToList();
                if (chunkRows.Any())
                {
                    var contextualChunk = new StringBuilder();
                    contextualChunk.AppendLine(essentialMetadata);
                    contextualChunk.AppendLine("DETAILED DATA:");

                    // Add range information for better context
                    var endIndex = Math.Min(startIndex + EXCEL_ROWS_PER_CHUNK, dataRows.Count);
                    contextualChunk.AppendLine($"Rows {startIndex + 1}-{endIndex} of {dataRows.Count}:");

                    foreach (var row in chunkRows)
                    {
                        contextualChunk.AppendLine(row);
                    }

                    chunks.Add(contextualChunk.ToString().Trim());
                }
            }
        }

        /// <summary>
        /// Standard chunking for non-Excel content
        /// </summary>
        /// <param name="content">Regular content to chunk</param>
        /// <returns>List of content chunks</returns>
        private List<string> ChunkRegularContent(string content)
        {
            var chunks = new List<string>();

            // Try to split by paragraphs first
            var paragraphs = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var paragraph in paragraphs)
            {
                var trimmedParagraph = paragraph.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedParagraph))
                {
                    // If paragraph is too long, split it further
                    if (trimmedParagraph.Length > TEXT_CHUNK_SIZE)
                    {
                        chunks.AddRange(SplitLargeText(trimmedParagraph));
                    }
                    else
                    {
                        chunks.Add(trimmedParagraph);
                    }
                }
            }

            // If no paragraphs found, split by single newlines
            if (chunks.Count == 0)
            {
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        if (trimmedLine.Length > TEXT_CHUNK_SIZE)
                        {
                            chunks.AddRange(SplitLargeText(trimmedLine));
                        }
                        else
                        {
                            chunks.Add(trimmedLine);
                        }
                    }
                }
            }

            // If still no chunks, use the entire content but split if too large
            if (chunks.Count == 0)
            {
                if (content.Length > TEXT_CHUNK_SIZE)
                {
                    chunks.AddRange(SplitLargeText(content));
                }
                else
                {
                    chunks.Add(content);
                }
            }

            return chunks;
        }

        /// <summary>
        /// Splits large text into smaller chunks while trying to preserve word boundaries
        /// </summary>
        /// <param name="text">Large text to split</param>
        /// <returns>List of smaller text chunks</returns>
        private List<string> SplitLargeText(string text)
        {
            var chunks = new List<string>();
            var words = text.Split(' ');
            var currentChunk = new StringBuilder();

            foreach (var word in words)
            {
                if (currentChunk.Length + word.Length + 1 > TEXT_CHUNK_SIZE)
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }
                }

                currentChunk.Append(word + " ");
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks;
        }

        /// <summary>
        /// Extracts essential metadata from Excel sheet metadata for context preservation
        /// </summary>
        /// <param name="metadata">Full metadata text</param>
        /// <returns>Essential metadata for context</returns>
        private string ExtractEssentialMetadata(string metadata)
        {
            var lines = metadata.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var essential = new StringBuilder();

            foreach (var line in lines)
            {
                // Keep essential structural information for context
                if (line.Contains("=== SHEET:") ||
                    line.Contains("COLUMNS (") ||
                    line.Contains("TOTAL ROWS:") ||
                    line.StartsWith("  ") && line.Contains("(") || // Column definitions
                    line.Contains("DATA SUMMARY:") ||
                    (line.StartsWith("  ") && line.Contains("unique values"))) // Summary stats
                {
                    essential.AppendLine(line);
                }
            }

            return essential.ToString();
        }

        /// <summary>
        /// Gets the optimal chunk size for different content types
        /// </summary>
        /// <param name="contentType">Type of content being chunked</param>
        /// <returns>Optimal chunk size for the content type</returns>
        public int GetOptimalChunkSize(string contentType)
        {
            return contentType.ToLower() switch
            {
                "excel" => 1600,        // Balanced size for Excel chunks
                "pdf" => 1500,          // Medium chunks for PDFs
                "word" => 1200,         // Medium chunks for Word docs
                "text" => TEXT_CHUNK_SIZE, // Standard size for plain text
                _ => TEXT_CHUNK_SIZE
            };
        }

        /// <summary>
        /// Validates if content should be chunked based on its size and type
        /// </summary>
        /// <param name="content">Content to evaluate</param>
        /// <param name="contentType">Type of content</param>
        /// <returns>True if content should be chunked</returns>
        public bool ShouldChunkContent(string content, string contentType = "text")
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var optimalSize = GetOptimalChunkSize(contentType);
            return content.Length > optimalSize;
        }
    }
}