using System;

namespace AURA.Core.Events
{
    /// <summary>
    /// Publicado quando uma célula muda de estado (criada/iniciada/parada/
    /// pausada/retomada/excluída).
    /// </summary>
    public sealed class CellStateChangedEvent : IEvent
    {
        public string CellId { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Publicado quando um assistente (aichat/termux-ai/opencode) responde a
    /// uma pergunta via <c>aura ask</c>.
    /// </summary>
    public sealed class AssistantRespondedEvent : IEvent
    {
        public string Assistant { get; set; }

        public string Question { get; set; }

        public string Answer { get; set; }

        public string CellId { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Publicado quando um executor de ferramenta (shell/git/python/node)
    /// termina a execução.
    /// </summary>
    public sealed class ExecutorCompletedEvent : IEvent
    {
        public string Executor { get; set; }

        public string Command { get; set; }

        public bool Success { get; set; }

        public TimeSpan Duration { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Publicado quando um módulo opcional é aplicado ou removido, para a UI
    /// reordenar as abas/funções visíveis.
    /// </summary>
    public sealed class ModuleStateChangedEvent : IEvent
    {
        public string ModuleId { get; set; }

        public bool Applied { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}
