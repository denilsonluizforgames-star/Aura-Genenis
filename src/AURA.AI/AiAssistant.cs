using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AURA.Core.Logging;
using AURA.Memory;

namespace AURA.AI
{
    /// <summary>
    /// F3 assistant service: asks the LLM a question, returns the answer, and
    /// persists the conversation turn in MemoryStore so context survives across
    /// restarts (mirror of the mobile app's AURA.AI / MemoryService).
    /// </summary>
    public sealed class AiAssistant
    {
        private readonly OpenRouterClient _client;
        private readonly MemoryStore _memory;
        private readonly ILogger _logger;

        public AiAssistant(OpenRouterClient client, MemoryStore memory, ILogger? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _logger = logger ?? new ConsoleLogger();
        }

        public async Task<string> AskAsync(string question,
            HttpClient? httpClient = null, CancellationToken ct = default)
        {
            _memory.Append(MemoryEntry.Question(question));

            string answer = await _client.ChatAsync(question, httpClient, ct).ConfigureAwait(false);
            _memory.Append(MemoryEntry.Answer(answer));

            _logger.Info("AI: pergunta registrada e respondida.");
            return answer;
        }
    }
}
