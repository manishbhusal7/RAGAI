using OpenAI.Chat;

namespace Backend.Services.AI
{
    /// <summary>
    /// Service responsible for creating and managing chat completion configurations.
    /// This centralizes all AI model settings and parameters for consistent behavior.
    /// </summary>
    public class ChatConfiguration
    {
        // Constants for AI model behavior - all in one place for easy tuning
        private const float AI_TEMPERATURE = 0.1f;              // Lower temperature for more focused, analytical responses
        private const int MAX_RESPONSE_TOKENS = 3000;           // Increased for complex data analysis responses
        private const float AI_TOP_P = 0.9f;                    // Slightly more focused response diversity
        private const float FREQUENCY_PENALTY = 0.1f;          // Small penalty to reduce repetition in analysis
        private const float PRESENCE_PENALTY = 0.1f;           // Encourage covering different aspects of data

        /// <summary>
        /// Creates standard chat completion options for general use
        /// </summary>
        /// <returns>Configured ChatCompletionOptions with standard settings</returns>
        public ChatCompletionOptions CreateStandardOptions()
        {
            return new ChatCompletionOptions
            {
                Temperature = AI_TEMPERATURE,
                MaxOutputTokenCount = MAX_RESPONSE_TOKENS,
                TopP = AI_TOP_P,
                FrequencyPenalty = FREQUENCY_PENALTY,
                PresencePenalty = PRESENCE_PENALTY
            };
        }

        /// <summary>
        /// Creates chat completion options optimized for analytical tasks
        /// </summary>
        /// <returns>ChatCompletionOptions optimized for data analysis and detailed responses</returns>
        public ChatCompletionOptions CreateAnalyticalOptions()
        {
            return new ChatCompletionOptions
            {
                Temperature = 0.2f,                              // Even lower temperature for more precise analysis
                MaxOutputTokenCount = 4000,                     // More tokens for detailed analysis
                TopP = 0.85f,                                   // More focused responses
                FrequencyPenalty = 0.15f,                       // Higher penalty to avoid repetition
                PresencePenalty = 0.2f                          // Encourage comprehensive coverage
            };
        }

        /// <summary>
        /// Creates chat completion options optimized for creative or general tasks
        /// </summary>
        /// <returns>ChatCompletionOptions with higher creativity settings</returns>
        public ChatCompletionOptions CreateCreativeOptions()
        {
            return new ChatCompletionOptions
            {
                Temperature = 0.7f,                              // Higher temperature for more creative responses
                MaxOutputTokenCount = MAX_RESPONSE_TOKENS,
                TopP = 0.95f,                                   // More diverse response options
                FrequencyPenalty = 0.05f,                       // Lower penalty to allow more variation
                PresencePenalty = 0.05f
            };
        }

        /// <summary>
        /// Creates chat completion options based on the source type
        /// </summary>
        /// <param name="sourceType">Type of source (calendar, uploaded, confluence)</param>
        /// <returns>Optimized ChatCompletionOptions for the specific source type</returns>
        public ChatCompletionOptions CreateOptionsForSourceType(string sourceType)
        {
            return sourceType.ToLower() switch
            {
                "uploaded" => CreateAnalyticalOptions(),        // User documents need detailed analysis
                "calendar" => CreateStandardOptions(),          // Calendar queries are straightforward
                "confluence" => CreateStandardOptions(),        // Company docs need consistent responses
                _ => CreateStandardOptions()
            };
        }

        /// <summary>
        /// Gets the current AI model name being used
        /// </summary>
        /// <returns>The AI model identifier</returns>
        public string GetModelName()
        {
            return "gpt-4o";
        }

        /// <summary>
        /// Gets configuration details for logging or debugging
        /// </summary>
        /// <returns>Dictionary with current configuration values</returns>
        public Dictionary<string, object> GetConfigurationDetails()
        {
            return new Dictionary<string, object>
            {
                { "Model", GetModelName() },
                { "Temperature", AI_TEMPERATURE },
                { "MaxTokens", MAX_RESPONSE_TOKENS },
                { "TopP", AI_TOP_P },
                { "FrequencyPenalty", FREQUENCY_PENALTY },
                { "PresencePenalty", PRESENCE_PENALTY }
            };
        }
    }
} 