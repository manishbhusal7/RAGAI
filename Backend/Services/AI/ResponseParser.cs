using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace Backend.Services.AI
{
    /// <summary>
    /// Service responsible for parsing and validating AI responses.
    /// This handles response extraction, error handling, and fallback messaging.
    /// </summary>
    public class ResponseParser
    {
        /// <summary>
        /// Extracts text content from an AI chat completion response
        /// </summary>
        /// <param name="aiResponse">The response from Azure OpenAI</param>
        /// <returns>Extracted text content or null if extraction fails</returns>
        public string? ExtractResponseText(System.ClientModel.ClientResult<ChatCompletion> aiResponse)
        {
            try
            {
                if (aiResponse?.Value?.Content?.Count > 0)
                {
                    var responseContent = aiResponse.Value.Content[0];
                    if (responseContent?.Text != null)
                    {
                        return responseContent.Text;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting response text: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a user-friendly fallback message when AI operations fail
        /// </summary>
        /// <param name="operationDescription">Description of what operation failed</param>
        /// <returns>Professional error message for users</returns>
        public string CreateFallbackErrorMessage(string operationDescription)
        {
            return $"I apologize, but I encountered an issue while {operationDescription}. " +
                   "Please try rephrasing your question or try again in a moment. " +
                   "If the problem persists, please contact your system administrator.";
        }

        /// <summary>
        /// Creates a configuration error message when Azure OpenAI setup is incomplete
        /// </summary>
        /// <param name="missingComponent">What configuration is missing</param>
        /// <returns>Technical error message for configuration issues</returns>
        public string CreateConfigurationErrorMessage(string missingComponent)
        {
            return $"Error: {missingComponent} is not configured. Please check your settings and ensure all Azure OpenAI credentials are properly set up.";
        }

        /// <summary>
        /// Validates if an AI response contains meaningful content
        /// </summary>
        /// <param name="responseText">The response text to validate</param>
        /// <returns>True if response is valid and meaningful</returns>
        public bool IsValidResponse(string? responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return false;

            // Check for minimum content length
            if (responseText.Trim().Length < 10)
                return false;

            // Check for common error indicators
            var errorIndicators = new[]
            {
                "I apologize",
                "I'm sorry",
                "I cannot",
                "Unable to process",
                "Error occurred",
                "Something went wrong"
            };

            // If response starts with error indicators, it might not be a good response
            // But don't completely reject it - these could be legitimate responses
            return true;
        }

        /// <summary>
        /// Truncates text to a specified maximum length while preserving word boundaries
        /// </summary>
        /// <param name="text">Text to truncate</param>
        /// <param name="maxLength">Maximum allowed length</param>
        /// <returns>Truncated text with ellipsis if needed</returns>
        public string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            // Find the last complete word within the limit
            int lastSpaceIndex = text.LastIndexOf(' ', maxLength - 3); // Reserve space for "..."

            if (lastSpaceIndex > 0)
            {
                return text.Substring(0, lastSpaceIndex) + "...";
            }
            else
            {
                // If no space found, just truncate at the limit
                return text.Substring(0, maxLength - 3) + "...";
            }
        }

        /// <summary>
        /// Sanitizes and cleans response text for safe display
        /// </summary>
        /// <param name="responseText">Raw response text</param>
        /// <returns>Cleaned and sanitized text</returns>
        public string SanitizeResponse(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return string.Empty;

            // Basic cleanup operations
            var cleaned = responseText
                .Trim()                                    // Remove leading/trailing whitespace
                .Replace("\r\n", "\n")                     // Normalize line endings
                .Replace("\r", "\n")                       // Handle old Mac line endings
                .Replace("\n\n\n", "\n\n");              // Reduce excessive blank lines

            return cleaned;
        }

        /// <summary>
        /// Handles and formats exceptions into user-friendly messages
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="context">Context about what was happening when the error occurred</param>
        /// <returns>User-friendly error message</returns>
        public string HandleException(Exception exception, string context)
        {
            // Log the full exception for debugging
            Console.WriteLine($"Exception in {context}: {exception}");

            // Return user-friendly message based on exception type
            return exception switch
            {
                TimeoutException => $"The request timed out while {context}. Please try again.",
                UnauthorizedAccessException => "Authentication failed. Please check your credentials.",
                HttpRequestException httpEx when httpEx.Message.Contains("404") =>
                    "The AI service is currently unavailable. Please try again later.",
                HttpRequestException httpEx when httpEx.Message.Contains("429") =>
                    "The AI service is busy. Please wait a moment and try again.",
                HttpRequestException =>
                    "Network error occurred. Please check your connection and try again.",
                ArgumentException =>
                    "Invalid request format. Please rephrase your question and try again.",
                _ => CreateFallbackErrorMessage(context)
            };
        }

        /// <summary>
        /// Creates a standard success response wrapper
        /// </summary>
        /// <param name="content">The successful response content</param>
        /// <returns>Formatted success response</returns>
        public string CreateSuccessResponse(string content)
        {
            return SanitizeResponse(content);
        }

        /// <summary>
        /// Creates a standard "no content found" message
        /// </summary>
        /// <param name="searchContext">What was being searched</param>
        /// <returns>Helpful message when no relevant content is found</returns>
        public string CreateNoContentFoundMessage(string searchContext)
        {
            return $"I wasn't able to find specific information about your question in the available {searchContext}. " +
                   "However, I can try to help with general information. Could you provide more details or rephrase your question?";
        }
    }
}