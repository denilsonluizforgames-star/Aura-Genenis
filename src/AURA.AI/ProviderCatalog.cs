using System.Collections.Generic;
using AURA.AI.Providers;

namespace AURA.AI
{
    public sealed class ProviderModel
    {
        public string Id { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public bool IsFree { get; init; }

        public override string ToString() =>
            IsFree ? $"{Label} (grátis)" : Label;
    }

    public sealed class ProviderInfo : IAiProvider
    {
        public string Name { get; init; } = string.Empty;
        public string BaseUrl { get; init; } = string.Empty;
        public string ModelsUrl { get; init; } = string.Empty;
        public bool NeedsKey { get; init; } = true;
        public string KeyHint { get; init; } = string.Empty;
        public string DefaultModelId { get; init; } = string.Empty;
        public string AuthHeaderName { get; init; } = "Authorization";
        public string AuthScheme { get; init; } = "Bearer ";
        public AiApiFormat ApiFormat { get; init; } = AiApiFormat.OpenAICompletions;
        public string AnthropicVersion { get; init; } = "2023-06-01";
        public IReadOnlyList<string> KeyPrefixes { get; init; } = new List<string>();
        public List<ProviderModel> Models { get; init; } = new();
    }

    public static class ProviderCatalog
    {
        private static readonly List<ProviderInfo> ProvidersList = Build();

        public static List<ProviderInfo> Providers => ProvidersList;

        private static List<ProviderInfo> Build()
        {
            return new List<ProviderInfo>
            {
                new ProviderInfo
                {
                    Name = "OpenRouter",
                    BaseUrl = "https://openrouter.ai/api/v1/chat/completions",
                    ModelsUrl = "https://openrouter.ai/api/v1/models",
                    NeedsKey = true,
                    KeyHint = "sk-or-...",
                    DefaultModelId = "openrouter/free",
                    KeyPrefixes = new List<string> { "sk-or-" },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "openrouter/free", Label = "Auto (qualquer grátis)", Category = "Grátis", IsFree = true },
                        new() { Id = "openai/gpt-oss-120b", Label = "GPT-OSS 120B", Category = "Flagship", IsFree = false },
                        new() { Id = "openai/gpt-oss-20b:free", Label = "GPT-OSS 20B", Category = "Grátis", IsFree = true },
                        new() { Id = "nvidia/nemotron-3-ultra-550b-a55b:free", Label = "Nemotron 3 Ultra", Category = "Grátis", IsFree = true },
                        new() { Id = "nvidia/nemotron-3-super-120b-a12b:free", Label = "Nemotron 3 Super", Category = "Grátis", IsFree = true },
                        new() { Id = "nvidia/nemotron-3-nano-30b-a3b:free", Label = "Nemotron Nano 30B", Category = "Grátis", IsFree = true },
                        new() { Id = "nvidia/nemotron-nano-9b-v2:free", Label = "Nemotron Nano 9B v2", Category = "Grátis", IsFree = true },
                        new() { Id = "google/gemma-4-31b-it:free", Label = "Gemma 4 31B", Category = "Grátis", IsFree = true },
                        new() { Id = "google/gemma-4-26b-a4b-it:free", Label = "Gemma 4 26B", Category = "Grátis", IsFree = true },
                        new() { Id = "poolside/laguna-s-2.1:free", Label = "Laguna S 2.1", Category = "Grátis", IsFree = true },
                        new() { Id = "poolside/laguna-xs-2.1:free", Label = "Laguna XS 2.1", Category = "Grátis", IsFree = true },
                        new() { Id = "inclusionai/ling-3.0-flash:free", Label = "Ling 3.0 Flash", Category = "Grátis", IsFree = true },
                        new() { Id = "cohere/north-mini-code:free", Label = "North Mini Code", Category = "Grátis", IsFree = true },
                    }
                },
                new ProviderInfo
                {
                    Name = "OpenAI",
                    BaseUrl = "https://api.openai.com/v1/chat/completions",
                    ModelsUrl = "https://api.openai.com/v1/models",
                    NeedsKey = true,
                    KeyHint = "sk-proj-...",
                    DefaultModelId = "gpt-4o-mini",
                    KeyPrefixes = new List<string> { "sk-proj-", "sk-" },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "gpt-4o", Label = "GPT-4o", Category = "Flagship", IsFree = false },
                        new() { Id = "gpt-4o-mini", Label = "GPT-4o mini", Category = "Razoável", IsFree = false },
                        new() { Id = "o3", Label = "o3 (raciocínio)", Category = "Flagship", IsFree = false },
                    }
                },
                new ProviderInfo
                {
                    Name = "Anthropic",
                    BaseUrl = "https://api.anthropic.com/v1/messages",
                    ModelsUrl = "https://api.anthropic.com/v1/models",
                    NeedsKey = true,
                    KeyHint = "sk-ant-...",
                    DefaultModelId = "claude-3-5-sonnet-latest",
                    AuthHeaderName = "x-api-key",
                    AuthScheme = "",
                    ApiFormat = AiApiFormat.AnthropicMessages,
                    KeyPrefixes = new List<string> { "sk-ant-" },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "claude-3-7-sonnet-latest", Label = "Claude 3.7 Sonnet", Category = "Flagship", IsFree = false },
                        new() { Id = "claude-3-5-sonnet-latest", Label = "Claude 3.5 Sonnet", Category = "Razoável", IsFree = false },
                        new() { Id = "claude-3-5-haiku-latest", Label = "Claude 3.5 Haiku", Category = "Rápido", IsFree = false },
                    }
                },
                new ProviderInfo
                {
                    Name = "Google Gemini",
                    BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                    ModelsUrl = "https://generativelanguage.googleapis.com/v1beta/openai/models",
                    NeedsKey = true,
                    KeyHint = "AIza... ou AQ....",
                    DefaultModelId = "gemini-3.6-flash",
                    KeyPrefixes = new List<string> { "AIza", "AQ." },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "gemini-3.6-flash", Label = "Gemini 3.6 Flash", Category = "Grátis", IsFree = true },
                        new() { Id = "gemini-3-flash-preview", Label = "Gemini 3 Flash", Category = "Grátis", IsFree = true },
                        new() { Id = "gemini-3.1-pro-preview", Label = "Gemini 3.1 Pro", Category = "Pago", IsFree = false },
                    }
                },
                new ProviderInfo
                {
                    Name = "Groq (grátis)",
                    BaseUrl = "https://api.groq.com/openai/v1/chat/completions",
                    ModelsUrl = "https://api.groq.com/openai/v1/models",
                    NeedsKey = true,
                    KeyHint = "gsk_...",
                    DefaultModelId = "openai/gpt-oss-20b",
                    KeyPrefixes = new List<string> { "gsk_" },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "openai/gpt-oss-120b", Label = "GPT-OSS 120B", Category = "Grátis", IsFree = true },
                        new() { Id = "openai/gpt-oss-20b", Label = "GPT-OSS 20B", Category = "Grátis", IsFree = true },
                        new() { Id = "qwen/qwen3.6-27b", Label = "Qwen 3.6 27B", Category = "Grátis", IsFree = true },
                    }
                },
                new ProviderInfo
                {
                    Name = "Cerebras (grátis)",
                    BaseUrl = "https://api.cerebras.ai/v1/chat/completions",
                    ModelsUrl = "https://api.cerebras.ai/v1/models",
                    NeedsKey = true,
                    KeyHint = "csk-...",
                    DefaultModelId = "gpt-oss-120b",
                    KeyPrefixes = new List<string> { "csk-" },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "gpt-oss-120b", Label = "GPT-OSS 120B", Category = "Grátis", IsFree = true },
                        new() { Id = "gemma-4-31b", Label = "Gemma 4 31B", Category = "Grátis", IsFree = true },
                    }
                },
                new ProviderInfo
                {
                    Name = "xAI (Grok)",
                    BaseUrl = "https://api.x.ai/v1/chat/completions",
                    ModelsUrl = "https://api.x.ai/v1/models",
                    NeedsKey = true,
                    KeyHint = "xai-...",
                    DefaultModelId = "grok-3-mini",
                    KeyPrefixes = new List<string> { "xai-" },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "grok-3", Label = "Grok 3", Category = "Flagship", IsFree = false },
                        new() { Id = "grok-3-mini", Label = "Grok 3 mini", Category = "Rápido", IsFree = false },
                    }
                },
                new ProviderInfo
                {
                    Name = "DeepSeek",
                    BaseUrl = "https://api.deepseek.com/v1/chat/completions",
                    ModelsUrl = "https://api.deepseek.com/v1/models",
                    NeedsKey = true,
                    KeyHint = "sk-...",
                    DefaultModelId = "deepseek-chat",
                    KeyPrefixes = new List<string> { "sk-" },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "deepseek-chat", Label = "DeepSeek Chat", Category = "Razoável", IsFree = false },
                        new() { Id = "deepseek-reasoner", Label = "DeepSeek Reasoner", Category = "Flagship", IsFree = false },
                    }
                },
                new ProviderInfo
                {
                    Name = "Mistral",
                    BaseUrl = "https://api.mistral.ai/v1/chat/completions",
                    ModelsUrl = "https://api.mistral.ai/v1/models",
                    NeedsKey = true,
                    KeyHint = "chave Mistral",
                    DefaultModelId = "mistral-large-latest",
                    KeyPrefixes = new List<string> { },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "mistral-large-latest", Label = "Mistral Large", Category = "Flagship", IsFree = false },
                        new() { Id = "mistral-small-latest", Label = "Mistral Small", Category = "Rápido", IsFree = false },
                    }
                },
                new ProviderInfo
                {
                    Name = "Together AI",
                    BaseUrl = "https://api.together.xyz/v1/chat/completions",
                    ModelsUrl = "https://api.together.xyz/v1/models",
                    NeedsKey = true,
                    KeyHint = "tgp_...",
                    DefaultModelId = "meta-llama/Llama-4-Scout-17B-16E-Instruct",
                    KeyPrefixes = new List<string> { "tgp_" },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "meta-llama/Llama-4-Scout-17B-16E-Instruct", Label = "Llama 4 Scout", Category = "Razoável", IsFree = false },
                    }
                },
                new ProviderInfo
                {
                    Name = "Ollama (local)",
                    BaseUrl = "http://localhost:11434/v1/chat/completions",
                    ModelsUrl = "http://localhost:11434/v1/models",
                    NeedsKey = false,
                    KeyHint = "deixe vazio",
                    DefaultModelId = "llama3.2",
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "llama3.2", Label = "Llama 3.2", Category = "Local", IsFree = true },
                        new() { Id = "qwen2.5", Label = "Qwen 2.5", Category = "Local", IsFree = true },
                        new() { Id = "mistral", Label = "Mistral", Category = "Local", IsFree = true },
                    }
                },
                new ProviderInfo
                {
                    Name = "Custom (OpenAI-compatível)",
                    BaseUrl = "https://SEU_HOST/v1/chat/completions",
                    ModelsUrl = "https://SEU_HOST/v1/models",
                    NeedsKey = false,
                    KeyHint = "opcional",
                    DefaultModelId = "modelo",
                    KeyPrefixes = new List<string> { },
                    Models = new List<ProviderModel>
                    {
                        new() { Id = "modelo", Label = "Modelo do seu endpoint", Category = "Custom", IsFree = true },
                    }
                },
            };
        }

        public static ProviderInfo? Find(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ProvidersList[0];
            }

            foreach (var p in ProvidersList)
            {
                if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return ProvidersList[0];
        }

        /// <summary>
        /// Provedores que exigem chave e aceitam o formato OpenAI de chat
        /// (candidatos válidos para teste de credencial).
        /// </summary>
        public static IEnumerable<ProviderInfo> KeyedProbeCandidates()
        {
            foreach (var p in ProvidersList)
            {
                if (p.NeedsKey)
                {
                    yield return p;
                }
            }
        }
    }
}
