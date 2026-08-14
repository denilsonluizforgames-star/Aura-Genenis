using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AURA.Core.Logging;
using AURA.Memory;

namespace AURA.AI
{
    /// <summary>
    /// Loop agêntico sobre o OpenRouterClient: envia a conversa com as
    /// ferramentas registradas, executa as chamadas de ferramenta solicitadas
    /// pelo modelo, anexa os resultados e repete até o modelo responder texto
    /// final (estilo opencode/agentes de terminal).
    /// Quando um MemoryStore é fornecido, cada turno user/assistant é persistido
    /// em ~/AURA/memory.json, garantindo continuidade de contexto entre sessões.
    /// </summary>
    public sealed class AgentSession
    {
        private const int MaxRounds = 20;

        private readonly OpenRouterClient _client;
        private readonly ILogger _logger;
        private readonly ToolRegistry _registry;
        private readonly List<AgentMessage> _messages = new();
        private readonly string? _systemPrompt;
        private readonly MemoryStore? _memory;

        /// <summary>
        /// Construtor preferido: ferramentas já organizadas em <see cref="ToolRegistry"/>.
        /// </summary>
        public AgentSession(OpenRouterClient client, ToolRegistry registry,
            string? systemPrompt = null, ILogger? logger = null, MemoryStore? memory = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _systemPrompt = systemPrompt;
            _logger = logger ?? new ConsoleLogger();
            _memory = memory;
        }

        /// <summary>
        /// Compatibilidade: monta um <see cref="ToolRegistry"/> a partir da lista.
        /// Preferir o construtor que recebe <see cref="ToolRegistry"/> diretamente.
        /// </summary>
        public AgentSession(OpenRouterClient client, IEnumerable<AgentTool> tools,
            string? systemPrompt = null, ILogger? logger = null, MemoryStore? memory = null)
            : this(client, new ToolRegistry(tools ?? Enumerable.Empty<AgentTool>()),
                systemPrompt, logger, memory)
        {
        }

        /// <summary>Emitido a cada ferramenta executada (para atualizar a UI).</summary>
        public event Action<AgentStep>? Step;

        public IReadOnlyList<AgentMessage> Messages => _messages;

        /// <summary>Registro de ferramentas desta sessão.</summary>
        public ToolRegistry Registry => _registry;

        public async Task<string> RunAsync(string userText,
            HttpClient? httpClient = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userText))
            {
                throw new ArgumentException("A instrução não pode ser vazia.", nameof(userText));
            }

            _messages.Add(new AgentMessage { Role = "user", Content = userText });
            _memory?.Append(MemoryEntry.Question(userText));

            int round = 0;
            while (round++ < MaxRounds)
            {
                AgentChatResponse response = await _client.ChatToolsAsync(
                    _messages,
                    _registry.Definitions().ToList(),
                    httpClient,
                    ct,
                    _systemPrompt).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(response.Error))
                {
                    throw new AgentLlmException(response.Error, response.ErrorKind);
                }

                if (response.ToolCalls is { Count: > 0 })
                {
                    _messages.Add(new AgentMessage
                    {
                        Role = "assistant",
                        Content = null,
                        ToolCalls = response.ToolCalls,
                        // Precisa seguir junto com esta mensagem assistant:
                        // é isto que faltava e causava o 400 "missing a
                        // thought_signature" nos modelos Gemini 3.x.
                        ReasoningDetails = response.ReasoningDetails
                    });

                    foreach (AgentToolCall call in response.ToolCalls)
                    {
                        ct.ThrowIfCancellationRequested();
                        AgentToolResult result = await ExecuteToolAsync(call, ct).ConfigureAwait(false);
                        _messages.Add(new AgentMessage
                        {
                            Role = "tool",
                            ToolCallId = call.Id,
                            Content = result.Text
                        });
                        Step?.Invoke(new AgentStep(call.Name, call.ArgumentsJson, result.Text, result.Success));
                        _logger.Info("agent: ferramenta='" + call.Name + "' " +
                            (result.Success ? "ok" : "erro"));
                    }

                    continue;
                }

                string final = response.Content ?? "(resposta vazia)";
                _messages.Add(new AgentMessage { Role = "assistant", Content = final });
                _memory?.Append(MemoryEntry.Answer(final));
                return final;
            }

            throw new InvalidOperationException(
                "O agente atingiu o limite de " + MaxRounds + " passos de ferramentas.");
        }

        private async Task<AgentToolResult> ExecuteToolAsync(AgentToolCall call, CancellationToken ct)
        {
            AgentTool? tool = _registry.Resolve(call.Name);
            if (tool == null)
            {
                return AgentToolResult.Error("ERRO: ferramenta desconhecida: " + call.Name);
            }

            try
            {
                return await tool.ExecuteStructuredAsync(call.ArgumentsJson, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error("agent: falha na ferramenta '" + call.Name + "': " + ex.Message);
                return AgentToolResult.Error("ERRO na ferramenta " + call.Name + ": " + ex.Message);
            }
        }
    }
}
