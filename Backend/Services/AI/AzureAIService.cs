using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using RAG.API.Controllers;

namespace Backend.Services.AI
{
    /// <summary>
    /// Main service that handles all AI interactions with Azure OpenAI.
    /// This service orchestrates the interaction between prompts, configuration, and response parsing.
    /// Think of this as the "conductor" that coordinates all AI operations.
    /// </summary>
    public class AzureAIService
    {
        // Azure OpenAI connection details
        private readonly string? _azureOpenAIEndpoint;
        private readonly string? _azureOpenAIKey;

        // Helper services for clean separation of concerns
        private readonly PromptBuilder _promptBuilder;
        private readonly ChatConfiguration _chatConfiguration;
        private readonly ResponseParser _responseParser;

        /// <summary>
        /// Creates a new AzureAIService with the necessary Azure OpenAI configuration and helper services
        /// </summary>
        /// <param name="configuration">Application configuration containing Azure OpenAI settings</param>
        public AzureAIService(IConfiguration configuration)
        {
            // Read Azure OpenAI connection details from configuration
            _azureOpenAIEndpoint = configuration["Azure:OpenAIEndpoint"];
            _azureOpenAIKey = configuration["Azure:OpenAIKey"];

            // Initialize helper services
            _promptBuilder = new PromptBuilder();
            _chatConfiguration = new ChatConfiguration();
            _responseParser = new ResponseParser();
        }

        /// <summary>
        /// Asks the AI a question with document context to help generate a better answer.
        /// This is the main method used when the AI has relevant documents to reference.
        /// </summary>
        /// <param name="documentContext">Relevant text from documents that might help answer the question</param>
        /// <param name="userQuestion">The question the user is asking</param>
        /// <returns>AI-generated response based on the context and question</returns>
        public async Task<string> AskQuestionAsync(string documentContext, string userQuestion)
        {
            // Validate configuration first
            var configurationCheck = ValidateAzureOpenAIConfiguration();
            if (!configurationCheck.isValid)
            {
                return _responseParser.CreateConfigurationErrorMessage(configurationCheck.errorMessage);
            }

            try
            {
                // Create Azure OpenAI client
                var azureOpenAIClient = CreateAzureOpenAIClient();
                var chatClient = azureOpenAIClient.GetChatClient(_chatConfiguration.GetModelName());

                // Build conversation messages
                var conversationMessages = _promptBuilder.CreateBasicConversationMessages(documentContext, userQuestion);

                // Configure chat options
                var responseOptions = _chatConfiguration.CreateStandardOptions();

                // Get AI response
                var aiResponse = await chatClient.CompleteChatAsync(conversationMessages, responseOptions);

                // Extract and validate response
                var responseText = _responseParser.ExtractResponseText(aiResponse);

                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    return _responseParser.CreateSuccessResponse(responseText);
                }

                return "Sorry, I couldn't generate a response. Please try again.";
            }
            catch (Exception exception)
            {
                return _responseParser.HandleException(exception, "processing your question");
            }
        }

        /// <summary>
        /// Asks the AI a question with specific context and source information.
        /// This version provides more detailed prompts based on where the information came from.
        /// </summary>
        /// <param name="documentContext">Relevant text from documents</param>
        /// <param name="userQuestion">The user's question</param>
        /// <param name="sourceType">Type of source (e.g., "calendar", "uploaded", "confluence")</param>
        /// <param name="hasUserDocuments">Whether the user has uploaded their own documents</param>
        /// <param name="conversationHistory">Previous messages in the conversation for context</param>
        /// <returns>AI-generated response tailored to the specific source type</returns>
        public async Task<string> AskQuestionWithContextAsync(string documentContext, string userQuestion,
            string sourceType, bool hasUserDocuments, List<ChatHistoryMessage>? conversationHistory = null)
        {
            // Validate configuration first
            var configurationCheck = ValidateAzureOpenAIConfiguration();
            if (!configurationCheck.isValid)
            {
                return _responseParser.CreateConfigurationErrorMessage(configurationCheck.errorMessage);
            }

            try
            {
                // Create Azure OpenAI client
                var azureOpenAIClient = CreateAzureOpenAIClient();
                var chatClient = azureOpenAIClient.GetChatClient(_chatConfiguration.GetModelName());

                // Build conversation messages with full context
                var conversationMessages = _promptBuilder.CreateConversationWithHistory(
                    documentContext, userQuestion, sourceType, hasUserDocuments, conversationHistory);

                // Configure chat options based on source type
                var responseOptions = _chatConfiguration.CreateOptionsForSourceType(sourceType);

                // Get AI response
                var aiResponse = await chatClient.CompleteChatAsync(conversationMessages, responseOptions);

                // Extract and validate response
                var responseText = _responseParser.ExtractResponseText(aiResponse);

                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    return _responseParser.CreateSuccessResponse(responseText);
                }

                return _responseParser.CreateNoContentFoundMessage(sourceType);
            }
            catch (Exception exception)
            {
                return _responseParser.HandleException(exception, $"processing your {sourceType} question");
            }
        }

        /// <summary>
        /// Validates that Azure OpenAI is properly configured
        /// </summary>
        /// <returns>Validation result with success status and error message if applicable</returns>
        private (bool isValid, string errorMessage) ValidateAzureOpenAIConfiguration()
        {
            if (string.IsNullOrEmpty(_azureOpenAIEndpoint))
            {
                return (false, "Azure OpenAI endpoint");
            }

            if (string.IsNullOrEmpty(_azureOpenAIKey))
            {
                return (false, "Azure OpenAI API key");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Creates and configures an Azure OpenAI client
        /// </summary>
        /// <returns>Configured AzureOpenAIClient</returns>
        private AzureOpenAIClient CreateAzureOpenAIClient()
        {
            var azureCredential = new AzureKeyCredential(_azureOpenAIKey!);
            return new AzureOpenAIClient(new Uri(_azureOpenAIEndpoint!), azureCredential);
        }
    }
}
