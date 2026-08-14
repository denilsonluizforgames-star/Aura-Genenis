using System;

namespace AURA.AI.Providers
{
    /// <summary>
    /// Credencial fornecida pelo usuário, sem contexto de UI. Nunca deve ser
    /// serializada, logada ou exposta em exceções.
    /// </summary>
    public sealed class ProviderCredential
    {
        public ProviderCredential(string apiKey, bool allowProbe = false)
        {
            ApiKey = apiKey ?? string.Empty;
            AllowProbe = allowProbe;
        }

        /// <summary>Chave de API em texto puro (memória apenas).</summary>
        public string ApiKey { get; }

        /// <summary>
        /// Autorização explícita do usuário para testar endpoints de
        /// provedores compatíveis a fim de descobrir o provedor. Sem isto,
        /// a AURA nunca envia a chave para um serviço externo.
        /// </summary>
        public bool AllowProbe { get; }

        /// <summary>Provedor preferido como contexto (ex.: o selecionado na UI).</summary>
        public string? PreferredProviderName { get; init; }

        /// <summary>Timeout opcional para chamadas de validação.</summary>
        public TimeSpan? Timeout { get; init; }
    }
}
