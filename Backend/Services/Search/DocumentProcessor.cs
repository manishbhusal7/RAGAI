using Azure.Storage.Blobs;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Presentation;

namespace Backend.Services.Search
{
    /// <summary>
    /// Service responsible for extracting text content from various document formats.
    /// This handles the complexity of different file types and document parsing.
    /// </summary>
    public class DocumentProcessor
    {
        private readonly IConfiguration _configuration;
        private readonly ExcelProcessor _excelProcessor;

        public DocumentProcessor(IConfiguration configuration)
        {
            _configuration = configuration;
            _excelProcessor = new ExcelProcessor();
        }

        /// <summary>
        /// Extracts text content from a document based on its file type
        /// </summary>
        /// <param name="fileName">Name of the file to process</param>
        /// <param name="blobUrl">URL of the blob where the file is stored</param>
        /// <returns>Extracted text content</returns>
        public async Task<string> ExtractTextFromDocument(string fileName, string blobUrl)
        {
            try
            {
                var blobClient = new BlobContainerClient(_configuration["Azure:BlobStorageConnectionString"], _configuration["Azure:BlobContainer"]);
                var blob = blobClient.GetBlobClient(fileName);
                
                if (!await blob.ExistsAsync())
                {
                    Console.WriteLine($"Blob {fileName} not found");
                    return string.Empty;
                }

                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                
                return extension switch
                {
                    ".txt" or ".md" or ".json" or ".xml" or ".csv" => await ExtractPlainTextAsync(blob),
                    ".pdf" => await ExtractPdfTextAsync(blob),
                    ".docx" => await ExtractWordTextAsync(blob),
                    ".xlsx" or ".xls" => await ExtractExcelTextAsync(blob),
                    ".pptx" => await ExtractPowerPointTextAsync(blob),
                    _ => await ExtractPlainTextAsync(blob) // Fallback: try as plain text
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting text from {fileName}: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Extracts plain text from text-based files
        /// </summary>
        private async Task<string> ExtractPlainTextAsync(BlobClient blob)
        {
            var response = await blob.DownloadAsync();
            using var stream = response.Value.Content;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Extracts text from PDF documents
        /// </summary>
        private async Task<string> ExtractPdfTextAsync(BlobClient blob)
        {
            using var pdfStream = await blob.OpenReadAsync();
            using var pdf = PdfDocument.Open(pdfStream);
            
            var sb = new System.Text.StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Extracts text from Word documents
        /// </summary>
        private async Task<string> ExtractWordTextAsync(BlobClient blob)
        {
            using var docStream = await blob.OpenReadAsync();
            using var wordDoc = WordprocessingDocument.Open(docStream, false);
            var body = wordDoc.MainDocumentPart.Document.Body;
            if (body == null)
            {
                return string.Empty;
            }
            return body.InnerText;
        }

        /// <summary>
        /// Extracts text from Excel spreadsheets using the specialized Excel processor
        /// </summary>
        private async Task<string> ExtractExcelTextAsync(BlobClient blob)
        {
            using var excelStream = await blob.OpenReadAsync();
            using var doc = SpreadsheetDocument.Open(excelStream, false);
            return _excelProcessor.ExtractExcelContent(doc);
        }

        /// <summary>
        /// Extracts text from PowerPoint presentations
        /// </summary>
        private async Task<string> ExtractPowerPointTextAsync(BlobClient blob)
        {
            using var pptStream = await blob.OpenReadAsync();
            using var ppt = PresentationDocument.Open(pptStream, false);
            
            var sb = new System.Text.StringBuilder();
            var slides = ppt.PresentationPart.SlideParts;
            
            foreach (var slide in slides)
            {
                var texts = slide.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                foreach (var text in texts)
                {
                    sb.AppendLine(text.Text);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the supported file extensions for document processing
        /// </summary>
        /// <returns>Array of supported file extensions</returns>
        public string[] GetSupportedExtensions()
        {
            return new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md", ".json", ".xml", ".csv" };
        }

        /// <summary>
        /// Checks if a file extension is supported for processing
        /// </summary>
        /// <param name="fileName">Name of the file to check</param>
        /// <returns>True if the file type is supported</returns>
        public bool IsFileTypeSupported(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return GetSupportedExtensions().Contains(extension);
        }
    }
} 