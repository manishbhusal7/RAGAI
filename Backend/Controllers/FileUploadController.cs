using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Services.Search;

namespace RAG.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly AzureSearchService _searchService;

        public FileUploadController(IConfiguration config, AzureSearchService searchService)
        {
            _config = config;
            _searchService = searchService;
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            try
            {
                // Upload to blob storage
            var blobClient = new BlobContainerClient(_config["Azure:BlobStorageConnectionString"], _config["Azure:BlobContainer"]);
            await blobClient.CreateIfNotExistsAsync();

            var blob = blobClient.GetBlobClient(file.FileName);
            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: true);

                // Process with Azure Search
                try
                {
                    await _searchService.ProcessDocumentAsync(file.FileName, blob.Uri.ToString());
                }
                catch (Exception searchEx)
                {
                    // Log the error but don't fail the upload
                    Console.WriteLine($"Azure Search processing failed: {searchEx.Message}");
                }

                return Ok(new {
                    fileName = file.FileName,
                    message = "File uploaded successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Upload failed: {ex.Message}" });
            }
        }

        [HttpDelete("{fileName}")]
        public async Task<IActionResult> Delete(string fileName)
        {
            try
            {
                // Delete from blob storage
                var blobClient = new BlobContainerClient(_config["Azure:BlobStorageConnectionString"], _config["Azure:BlobContainer"]);
                var blob = blobClient.GetBlobClient(fileName);
                await blob.DeleteIfExistsAsync();

                // Remove from Azure Search
                await _searchService.DeleteDocumentAsync(fileName);

                return Ok(new { message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Delete failed: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFiles()
        {
            var blobClient = new BlobContainerClient(_config["Azure:BlobStorageConnectionString"], _config["Azure:BlobContainer"]);
            var files = new List<object>();

            await foreach (var blobItem in blobClient.GetBlobsAsync())
            {
                // Exclude Confluence documents and system files - only show user-uploaded files
                if (blobItem.Name.StartsWith("confluence/") ||
                    blobItem.Name.StartsWith("_sync_status") ||
                    blobItem.Name.Contains("_metadata"))
                {
                    continue; // Skip Confluence and system files
                }

                files.Add(new
                {
                    fileId = blobItem.Name, // Use file name as ID
                    fileName = blobItem.Name,
                    fileSize = blobItem.Properties.ContentLength ?? 0,
                    fileType = blobItem.Properties.ContentType ?? "application/octet-stream",
                    createdAt = blobItem.Properties.CreatedOn?.UtcDateTime ?? DateTime.UtcNow,
                    fileState = "COMPLETED" // Or use your enum if you want
                });
            }

            return Ok(files);
        }

        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupOrphanedDocuments()
        {
            try
            {
                await _searchService.CleanupOrphanedDocumentsAsync();
                return Ok(new { message = "Cleanup completed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Cleanup failed: {ex.Message}" });
            }
        }
    }
}
