using System;

namespace AURA.AI
{
    /// <summary>
    /// Resultado estruturado de uma ferramenta (Fase B). Mantém o <see cref="Text"/>
    /// exatamente como a string que o <see cref="AgentSession"/> envia de volta ao
    /// modelo no protocolo de mensagens — nada do comportamento existente muda — e
    /// acrescenta a classificação de sucesso/erro para log, memória episódica e
    /// autocrítica (P1.3/P1.4). Não substitui <see cref="ExecutionResult"/> nem cria
    /// um segundo sistema de erro de execução de processo.
    /// </summary>
    public sealed class AgentToolResult
    {
        private AgentToolResult(bool success, string text)
        {
            Success = success;
            Text = text ?? string.Empty;
        }

        /// <summary>True quando a ferramenta concluiu com sucesso.</summary>
        public bool Success { get; }

        /// <summary>Texto no formato legado (ex.: "exit=0\n...") enviado ao LLM.</summary>
        public string Text { get; }

        public static AgentToolResult Ok(string text) => new(true, text);

        public static AgentToolResult Error(string text) => new(false, text);

        /// <summary>
        /// Classificação por convenção: strings que começam com "ERRO" são falhas.
        /// Usada pela implementação padrão de <see cref="AgentTool.ExecuteStructuredAsync"/>.
        /// </summary>
        public static AgentToolResult FromText(string text) =>
            text != null && text.StartsWith("ERRO", StringComparison.Ordinal)
                ? Error(text)
                : Ok(text ?? string.Empty);
    }
}
