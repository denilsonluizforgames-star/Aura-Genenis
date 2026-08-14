using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AURA.Core.Logging;
using AURA.Memory;

namespace AURA.AI
{
    /// <summary>
    /// F3-F5: ÃÂgent on the go — handles queries for mobile, desktop, and web.
    /// <br/>Pipeline: Client App → (AiAssistant) → OpenRouterClient → OpenRouter API.
    /// <br/>Persists conversation history in MemoryStore for cross-session continuity.
    /// <br/>Used by the CLI (<c>aura ask</c>), the Android app (<c>MainActivity</c>), and the upcoming web dashboard.
    /// </summary>
    public static class AiAssistantService
    {
        public static readonly OpenRouterOptions DefaultOptions = new OpenRouterOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("AURA_OPENROUTER_KEY") ?? string.Empty,
            BaseUrl = "https://openrouter.ai/api/v1/chat/completions",
            Model = "qwen/qwen-plus"
        };

        /// <summary>Thread-safe entry point for asking the AI anything.</summary>
        public static async Task<string> AskAsync(string question, MemoryStore? memory = null, ILogger? logger = null, OpenRouterOptions? options = null, HttpClient? http = null)
        {
            if (string.IsNullOrWhiteSpace(question))
                throw new ArgumentException("A pergunta não pode ser vazia.", nameof(question));

            ILogger log = logger ?? new ConsoleLogger();
            OpenRouterOptions opt = options ?? DefaultOptions;
            if (string.IsNullOrWhiteSpace(opt.ApiKey))
                throw new InvalidOperationException("API key não configurada. Defina a variável de ambiente AURA_OPENROUTER_KEY.");

            if (memory != null)
            {
                memory.Append(MemoryEntry.Question(question));
            }

            HttpClient client = http ?? new HttpClient();
            var payload = new
            {
                model = opt.Model,
                messages = new[] { new { role = "user", content = question } },
                @const = new { include_reasoning = false }
            };

            string json = JsonSerializer.Serialize(payload);
            var req = new HttpRequestMessage(HttpMethod.Post, opt.BaseUrl);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opt.ApiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage resp = await client.SendAsync(req).ConfigureAwait(false);
            string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                log.Error("LLM request failed: " + resp.StatusCode + " " + body);
                throw new HttpRequestException("LLM failed: " + resp.StatusCode);
            }

            using var doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            string answer = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            if (memory != null)
            {
                memory.Append(MemoryEntry.Answer(answer));
                log.Info("AI: pergunta e resposta armazenadas.");
            }

            return answer;
        }
    }
}
