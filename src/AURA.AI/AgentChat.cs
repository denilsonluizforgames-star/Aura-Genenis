using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AURA.AI
{
    /// <summary>Erro de chamada ao provedor LLM, com a causa já classificada
    /// (ver <see cref="AgentErrorKind"/>) para a UI decidir a mensagem certa.</summary>
    public sealed class AgentLlmException : Exception
    {
        public AgentErrorKind ErrorKind { get; }

        public AgentLlmException(string message, AgentErrorKind errorKind)
            : base(message)
        {
            ErrorKind = errorKind;
        }
    }

    /// <summary>
    /// Uma mensagem da conversa do agente, no protocolo OpenAI-compatível
    /// (roles: system | user | assistant | tool). Em tool_calls o conteúdo é
    /// null; o resultado da ferramenta volta com ToolCallId apontando o call.
    /// </summary>
    public sealed class AgentMessage
    {
        public string Role { get; set; } = "user";

        public string? Content { get; set; }

        public string? ToolCallId { get; set; }

        public List<AgentToolCall>? ToolCalls { get; set; }

        /// <summary>
        /// Blocos de "reasoning" opacos (formato "google-gemini-v1" etc.)
        /// devolvidos pelo OpenRouter junto de uma mensagem assistant com
        /// tool_calls. Modelos Gemini "thinking" (gemini-3.x) EXIGEM que
        /// esse array seja reenviado, sem alteração, na próxima requisição
        /// que contém os tool_calls correspondentes — senão o provedor
        /// responde 400 "missing a thought_signature". Guardado como
        /// JsonArray bruto (sem reserializar campo a campo) porque a ordem
        /// e o conteúdo exato dos blocos precisam ser preservados
        /// byte-a-byte. Ver: https://openrouter.ai/docs/guides/best-practices/reasoning-tokens
        /// </summary>
        public JsonArray? ReasoningDetails { get; set; }
    }

    /// <summary>Uma chamada de ferramenta solicitada pelo modelo.</summary>
    public sealed class AgentToolCall
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Argumentos em JSON (string) como retornado pelo modelo.</summary>
        public string ArgumentsJson { get; set; } = "{}";
    }

    /// <summary>Resposta de uma rodada de chat com suporte a ferramentas.</summary>
    public sealed class AgentChatResponse
    {
        /// <summary>Texto final (quando a resposta não usa ferramentas).</summary>
        public string? Content { get; set; }

        /// <summary>Chamadas de ferramenta solicitadas (quando houver).</summary>
        public List<AgentToolCall>? ToolCalls { get; set; }

        /// <summary>Blocos de reasoning brutos devolvidos junto com esta mensagem
        /// assistant (ver <see cref="AgentMessage.ReasoningDetails"/>). Precisa ser
        /// reanexado à mensagem assistant correspondente no histórico.</summary>
        public JsonArray? ReasoningDetails { get; set; }

        public string? Error { get; set; }

        /// <summary>Categoria do erro HTTP/rede, para a UI decidir a mensagem certa
        /// (chave inválida vs. sem crédito vs. rate limit vs. timeout etc.).</summary>
        public AgentErrorKind ErrorKind { get; set; } = AgentErrorKind.None;
    }

    /// <summary>Categoriza a causa de uma falha na chamada ao provedor LLM.</summary>
    public enum AgentErrorKind
    {
        None = 0,
        InvalidRequest,     // 400 - protocolo/request inválido
        InvalidApiKey,      // 401 - API key ausente/inválida
        PaymentRequired,    // 402 - billing/payment required
        RateLimited,        // 429 - quota/rate limit
        ProviderError,      // 5xx - erro do provedor
        Timeout,            // timeout de rede/HttpClient
        Network,            // outra falha de rede/conexão
        Unknown
    }

    /// <summary>Evento emitido pelo AgentSession a cada ferramenta executada (para a UI).</summary>
    public sealed class AgentStep
    {
        public AgentStep(string toolName, string arguments, string result)
            : this(toolName, arguments, result, true)
        {
        }

        public AgentStep(string toolName, string arguments, string result, bool success)
        {
            ToolName = toolName;
            Arguments = arguments;
            Result = result;
            Success = success;
        }

        public string ToolName { get; }

        public string Arguments { get; }

        public string Result { get; }

        /// <summary>Classificação de sucesso/erro (Fase B) — true para o construtor legado.</summary>
        public bool Success { get; }
    }
}
