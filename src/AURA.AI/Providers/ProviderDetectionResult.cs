using System;
using System.Collections.Generic;

namespace AURA.AI.Providers
{
    /// <summary>Método usado para chegar à detecção do provedor.</summary>
    public enum ProviderDetectionSource
    {
        /// <summary>Nenhuma informação concluinte.</summary>
        None,

        /// <summary>Identificado pelo formato/prefixo da chave (determinístico, sem rede).</summary>
        KeyFormat,

        /// <summary>Usou o provedor preferido/contexto como palpite inicial.</summary>
        Context,

        /// <summary>Confirmado testando endpoints compatíveis (com autorização explícita).</summary>
        Probe,
    }

    /// <summary>
    /// Resultado da identificação do provedor a partir de uma API key.
    /// </summary>
    public sealed class ProviderDetectionResult
    {
        /// <summary>Provedor identificado (nulo quando inconclusivo).</summary>
        public IAiProvider? Provider { get; set; }

        /// <summary>Como a detecção chegou ao resultado.</summary>
        public ProviderDetectionSource Source { get; set; }

        /// <summary>Lista de provedores candidatos quando a detecção é ambígua.</summary>
        public IReadOnlyList<IAiProvider> Candidates { get; set; } = Array.Empty<IAiProvider>();

        /// <summary>Descreve por que o provedor foi (ou não) identificado.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Se a detecção foi conclusiva a ponto de configurar automaticamente.</summary>
        public bool IsConclusive =>
            Provider != null && Source != ProviderDetectionSource.None;
    }
}
