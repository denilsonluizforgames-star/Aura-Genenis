namespace AURA.AI.Providers
{
    /// <summary>Formato de API do provedor.</summary>
    public enum AiApiFormat
    {
        /// <summary>POST /chat/completions com payload da OpenAI (OpenRouter, Groq, Cerebras, xAI, etc.).</summary>
        OpenAICompletions,

        /// <summary>POST /v1/messages com payload da Anthropic (Claude).</summary>
        AnthropicMessages,
    }
}
