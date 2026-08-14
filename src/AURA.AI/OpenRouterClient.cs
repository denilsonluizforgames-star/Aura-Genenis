using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AURA.AI.Providers;
using AURA.Core.Logging;
using AURA.Memory;

namespace AURA.AI
{
    /// <summary>
    /// Configurações do provedor LLM. O mobile (AURA.AI) expõe o mesmo
    /// provedor via MemoryService; aqui o cliente HTTP direto. Defaults seguem
    /// o config do aichat (OpenRouter, qwen/qwen-plus).
    /// </summary>
    public sealed class OpenRouterOptions
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
        public string Model { get; set; } = "qwen/qwen-plus";
        public int MaxTokens { get; set; } = 1500;
        public int TimeoutSeconds { get; set; } = 90;
        public string? AppReference { get; set; }

        /// <summary>Header de autenticação (padrão OpenAI: "Authorization").</summary>
        public string AuthHeaderName { get; set; } = "Authorization";

        /// <summary>Prefixo do esquema (padrão: "Bearer ").</summary>
        public string AuthScheme { get; set; } = "Bearer ";

        /// <summary>Formato de API do provedor.</summary>
        public AiApiFormat ApiFormat { get; set; } = AiApiFormat.OpenAICompletions;

        /// <summary>Header anthropic-version quando ApiFormat é AnthropicMessages.</summary>
        public string AnthropicVersion { get; set; } = "2023-06-01";
    }

    /// <summary>
    /// Cliente mínimo para OpenRouter chat completions. Construa a requisição
    /// (testável sem rede) com BuildRequest; execute com ChatAsync.
    /// </summary>
    public sealed class OpenRouterClient
    {
        private readonly ILogger _logger;

        public OpenRouterOptions Options { get; }

        public OpenRouterClient(OpenRouterOptions options, ILogger? logger = null)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? new ConsoleLogger();
        }

        public HttpRequestMessage BuildRequest(string question, string? systemPrompt = null)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                throw new ArgumentException("A pergunta não pode ser vazia.", nameof(question));
            }

            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            messages.Add(new { role = "user", content = question });

            var payload = new
            {
                model = Options.Model,
                max_tokens = Options.MaxTokens,
                messages
            };

            string json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, Options.BaseUrl);
            AddAuth(request);
            if (Options.AppReference != null)
            {
                request.Headers.TryAddWithoutValidation("X-Title", "AURA");
                request.Headers.TryAddWithoutValidation("X-URL", Options.AppReference);
            }

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }

        public async Task<string> ChatAsync(string question,
            HttpClient? httpClient = null, CancellationToken ct = default, string? systemPrompt = null)
        {
            EnsureValidApiKey();

            HttpClient client = httpClient ?? ResolveClient();
            HttpRequestMessage request = Options.ApiFormat == AiApiFormat.AnthropicMessages
                ? BuildAnthropicRequest(question, systemPrompt)
                : BuildRequest(question, systemPrompt);

            HttpResponseMessage response = await client.SendAsync(request, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string detail = string.IsNullOrWhiteSpace(body) ? "(sem corpo)" : body;
                if (detail.Length > 500)
                {
                    detail = detail.Substring(0, 500);
                }

                _logger.Error("LLM: " + response.StatusCode + " " + detail);
                throw new HttpRequestException(
                    string.Format("Falha na chamada LLM ({0} {1}): {2}",
                        (int)response.StatusCode, response.StatusCode, detail));
            }

            return Options.ApiFormat == AiApiFormat.AnthropicMessages
                ? ParseAnthropicText(body)
                : ParseOpenAIText(body);
        }

        /// <summary>
        /// Rodada única de chat com suporte a ferramentas (function calling).
        /// Devolve o texto final ou as chamadas de ferramenta solicitadas pelo
        /// modelo; o AgentSession executa as chamadas e faz o loop.
        /// </summary>
        public async Task<AgentChatResponse> ChatToolsAsync(
            List<AgentMessage> messages,
            List<AgentToolDefinition>? tools = null,
            HttpClient? httpClient = null,
            CancellationToken ct = default,
            string? systemPrompt = null)
        {
            EnsureValidApiKey();

            var payload = new JsonObject
            {
                ["model"] = Options.Model,
                ["max_tokens"] = Options.MaxTokens
            };

            var arr = new JsonArray();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                arr.Add(new JsonObject { ["role"] = "system", ["content"] = systemPrompt });
            }

            if (messages != null)
            {
                foreach (AgentMessage m in messages)
                {
                    var mo = new JsonObject { ["role"] = m.Role };
                    if (m.Content != null)
                    {
                        mo["content"] = m.Content;
                    }

                    if (m.ToolCallId != null)
                    {
                        mo["tool_call_id"] = m.ToolCallId;
                    }

                    if (m.ToolCalls is { Count: > 0 })
                    {
                        var calls = new JsonArray();
                        foreach (AgentToolCall tc in m.ToolCalls)
                        {
                            calls.Add(new JsonObject
                            {
                                ["id"] = tc.Id,
                                ["type"] = "function",
                                ["function"] = new JsonObject
                                {
                                    ["name"] = tc.Name,
                                    ["arguments"] = tc.ArgumentsJson
                                }
                            });
                        }

                        mo["tool_calls"] = calls;
                    }

                    // Reenvia os blocos de reasoning tal como recebidos. Gemini
                    // "thinking" exige isso na mensagem assistant que contém os
                    // tool_calls correspondentes, senão o provedor rejeita com
                    // 400 "missing a thought_signature". DeepClone é obrigatório:
                    // um JsonNode não pode pertencer a duas árvores JSON ao mesmo
                    // tempo (m.ReasoningDetails pode ser reutilizado em rounds futuros).
                    if (m.ReasoningDetails is { Count: > 0 })
                    {
                        mo["reasoning_details"] = m.ReasoningDetails.DeepClone();
                    }

                    arr.Add(mo);
                }
            }

            payload["messages"] = arr;

            if (tools is { Count: > 0 })
            {
                var toolsArray = new JsonArray();
                foreach (AgentToolDefinition t in tools)
                {
                    var props = new JsonObject();
                    foreach (KeyValuePair<string, AgentToolParameter> p in t.Parameters)
                    {
                        props[p.Key] = new JsonObject
                        {
                            ["type"] = p.Value.Type,
                            ["description"] = p.Value.Description
                        };
                    }

                    var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
                    if (t.Required.Count > 0)
                    {
                        var required = new JsonArray();
                        foreach (string r in t.Required)
                        {
                            required.Add(r);
                        }

                        schema["required"] = required;
                    }

                    toolsArray.Add(new JsonObject
                    {
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = t.Name,
                            ["description"] = t.Description,
                            ["parameters"] = schema
                        }
                    });
                }

                payload["tools"] = toolsArray;
            }

            string json = JsonSerializer.Serialize(payload);
            HttpClient client = httpClient ?? ResolveClient();
            var request = new HttpRequestMessage(HttpMethod.Post, Options.BaseUrl);
            AddAuth(request);
            if (Options.AppReference != null)
            {
                request.Headers.TryAddWithoutValidation("X-Title", "AURA");
                request.Headers.TryAddWithoutValidation("X-URL", Options.AppReference);
            }

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            string body;
            try
            {
                response = await client.SendAsync(request, ct).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // O token do chamador não foi cancelado: foi o timeout do
                // próprio HttpClient (Options.TimeoutSeconds) que estourou.
                _logger.Error("LLM: timeout após " + Options.TimeoutSeconds + "s");
                return new AgentChatResponse
                {
                    Error = "Timeout ao chamar o LLM após " + Options.TimeoutSeconds + "s.",
                    ErrorKind = AgentErrorKind.Timeout
                };
            }
            catch (HttpRequestException hex)
            {
                _logger.Error("LLM: falha de rede: " + hex.Message);
                return new AgentChatResponse
                {
                    Error = "Falha de rede ao chamar o LLM: " + hex.Message,
                    ErrorKind = AgentErrorKind.Network
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                string detail = string.IsNullOrWhiteSpace(body) ? "(sem corpo)" : body;
                if (detail.Length > 500)
                {
                    detail = detail.Substring(0, 500);
                }

                _logger.Error("LLM: " + response.StatusCode + " " + detail);
                return new AgentChatResponse
                {
                    Error = string.Format("Falha na chamada LLM ({0} {1}): {2}",
                        (int)response.StatusCode, response.StatusCode, detail),
                    ErrorKind = ClassifyError(response.StatusCode)
                };
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("choices", out JsonElement choices) &&
                    choices.GetArrayLength() > 0)
                {
                    JsonElement message = choices[0];
                    if (message.TryGetProperty("message", out JsonElement msg))
                    {
                        string? content = ReadContentString(msg);
                        var calls = new List<AgentToolCall>();
                        if (msg.TryGetProperty("tool_calls", out JsonElement toolCalls))
                        {
                            foreach (JsonElement call in toolCalls.EnumerateArray())
                            {
                                string id = GetProp(call, "id") ?? string.Empty;
                                string name = string.Empty;
                                string argumentsJson = "{}";
                                if (call.TryGetProperty("function", out JsonElement fn))
                                {
                                    name = GetProp(fn, "name") ?? string.Empty;
                                    argumentsJson = GetProp(fn, "arguments") ?? "{}";
                                }

                                calls.Add(new AgentToolCall
                                {
                                    Id = id,
                                    Name = name,
                                    ArgumentsJson = argumentsJson
                                });
                            }
                        }

                        // Captura os blocos de reasoning tal como vieram, sem
                        // reconstruir campo a campo — a sequência exata importa
                        // para modelos Gemini (ver comentário em AgentMessage).
                        JsonArray? reasoningDetails = null;
                        if (msg.TryGetProperty("reasoning_details", out JsonElement rd) &&
                            rd.ValueKind == JsonValueKind.Array)
                        {
                            reasoningDetails = JsonNode.Parse(rd.GetRawText()) as JsonArray;
                        }

                        return new AgentChatResponse
                        {
                            Content = content,
                            ToolCalls = calls.Count > 0 ? calls : null,
                            ReasoningDetails = reasoningDetails
                        };
                    }
                }

                return new AgentChatResponse { Content = body };
            }
            catch (JsonException jex)
            {
                _logger.Error("LLM: resposta inválida: " + jex.Message);
                return new AgentChatResponse { Error = "Resposta inválida do modelo: " + jex.Message };
            }
        }

        private static AgentErrorKind ClassifyError(HttpStatusCode status)
        {
            int code = (int)status;
            return status switch
            {
                HttpStatusCode.BadRequest => AgentErrorKind.InvalidRequest,
                HttpStatusCode.Unauthorized => AgentErrorKind.InvalidApiKey,
                HttpStatusCode.PaymentRequired => AgentErrorKind.PaymentRequired,
                HttpStatusCode.TooManyRequests => AgentErrorKind.RateLimited,
                _ when code >= 500 && code < 600 => AgentErrorKind.ProviderError,
                _ => AgentErrorKind.Unknown
            };
        }

        private void EnsureValidApiKey()
        {
            if (string.IsNullOrWhiteSpace(Options.ApiKey))
            {
                throw new InvalidOperationException(
                    "ApiKey do provedor LLM não configurada. Defina OpenRouterOptions.ApiKey.");
            }

            if (Options.ApiKey.Length > 200 ||
                Options.ApiKey.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
            {
                throw new InvalidOperationException(
                    "Chave de API inválida (parece conter conteúdo de log). " +
                    "Toque em 'Restaurar padrão' na aba Correções e digite a chave manualmente na aba Assistente.");
            }
        }

        private void AddAuth(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation(
                Options.AuthHeaderName, Options.AuthScheme + Options.ApiKey);
            if (Options.ApiFormat == AiApiFormat.AnthropicMessages)
            {
                request.Headers.TryAddWithoutValidation("anthropic-version", Options.AnthropicVersion);
            }
        }

        private HttpRequestMessage BuildAnthropicRequest(string question, string? systemPrompt)
        {
            var payload = new JsonObject
            {
                ["model"] = Options.Model,
                ["max_tokens"] = Options.MaxTokens
            };

            var messages = new JsonArray();
            var user = new JsonObject { ["role"] = "user", ["content"] = question };
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                payload["system"] = systemPrompt;
            }

            messages.Add(user);
            payload["messages"] = messages;

            var request = new HttpRequestMessage(HttpMethod.Post, Options.BaseUrl);
            AddAuth(request);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return request;
        }

        private static string ParseOpenAIText(string body)
        {
            using var document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("choices", out JsonElement choices) &&
                choices.GetArrayLength() > 0)
            {
                JsonElement first = choices[0];
                if (first.TryGetProperty("message", out JsonElement message) &&
                    message.TryGetProperty("content", out JsonElement content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString() ?? string.Empty;
                }
            }

            return body;
        }

        private static string ParseAnthropicText(string body)
        {
            using var document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("content", out JsonElement content) &&
                content.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (JsonElement block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out JsonElement type) &&
                        type.GetString() == "text" &&
                        block.TryGetProperty("text", out JsonElement text))
                    {
                        sb.Append(text.GetString());
                    }
                }

                if (sb.Length > 0)
                {
                    return sb.ToString();
                }
            }

            return body;
        }

        private HttpClient ResolveClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Options.TimeoutSeconds > 0 ? Options.TimeoutSeconds : 90)
            };
        }

        private static string? GetProp(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static string? ReadContentString(JsonElement message)
        {
            if (!message.TryGetProperty("content", out JsonElement content))
            {
                return null;
            }

            if (content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }

            if (content.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (JsonElement part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out JsonElement text) &&
                        text.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(text.GetString());
                    }
                }

                return sb.Length > 0 ? sb.ToString() : null;
            }

            return null;
        }
    }
}
