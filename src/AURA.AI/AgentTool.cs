using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AURA.AI
{
    /// <summary>Parâmetro de um esquema JSON-Schema simples de ferramenta.</summary>
    public sealed class AgentToolParameter
    {
        public string Type { get; set; } = "string";

        public string Description { get; set; } = string.Empty;
    }

    /// <summary>Definição de ferramenta enviada ao modelo no campo "tools".</summary>
    public sealed class AgentToolDefinition
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public Dictionary<string, AgentToolParameter> Parameters { get; } = new();

        public List<string> Required { get; } = new();
    }

    /// <summary>
    /// Uma ferramenta que o agente pode invocar. Recebe os argumentos como JSON
    /// e devolve um texto que será enviado de volta ao modelo.
    /// </summary>
    public abstract class AgentTool
    {
        public abstract AgentToolDefinition Definition { get; }

        public abstract Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default);

        /// <summary>
        /// Versão estruturada de <see cref="ExecuteAsync"/> (Fase B). A implementação
        /// padrão delega para <see cref="ExecuteAsync"/> e classifica o texto pela
        /// convenção "ERRO:"; ferramentas que conhecem a semântica do próprio resultado
        /// (ex.: exit code de processo) devem fazer override para classificar com precisão.
        /// O <see cref="AgentToolResult.Text"/> é idêntico ao que <see cref="ExecuteAsync"/>
        /// devolveria — nada no protocolo de mensagens muda.
        /// </summary>
        public virtual async Task<AgentToolResult> ExecuteStructuredAsync(
            string argumentsJson, CancellationToken ct = default)
        {
            string text = await ExecuteAsync(argumentsJson, ct).ConfigureAwait(false);
            return AgentToolResult.FromText(text);
        }

        /// <summary>Lê um parâmetro string de um JSON de argumentos (ou null).</summary>
        protected static string? ReadString(JsonElement args, string name)
        {
            if (args.ValueKind == JsonValueKind.Object &&
                args.TryGetProperty(name, out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }
    }
}
