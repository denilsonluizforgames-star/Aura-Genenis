namespace AURA.Modules
{
    /// <summary>
    /// Estado real de implementação de um módulo do catálogo. Distingue o que
    /// existe de verdade do que é só planejado, para o catálogo não enganar.
    /// </summary>
    public enum ModuleStatus
    {
        /// <summary>Código existe e está em uso (ex.: AI, Memory, Plugins).</summary>
        Implementado,

        /// <summary>Código parcial/em construção.</summary>
        EmDesenvolvimento,

        /// <summary>Só especificado/planejado (sem implementação real).</summary>
        Planejado
    }
}
