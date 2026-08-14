using System;

namespace AURA.AI.Providers
{
    /// <summary>Estado estruturado da validação de uma credencial.</summary>
    public enum ProviderHealthStatus
    {
        /// <summary>A credencial é válida e o provedor respondeu.</summary>
        Valid,

        /// <summary>A chave foi rejeitada (401/403).</summary>
        Unauthorized,

        /// <summary>Chave válida mas sem créditos/saldo (402/429 de cota).</summary>
        InsufficientCredits,

        /// <summary>Formato da resposta ou endpoint não bateu (provedor provavelmente errado).</summary>
        Invalid,

        /// <summary>Provedor inacessível (rede, timeout, 5xx).</summary>
        ProviderUnavailable,

        /// <summary>Não foi possível identificar o provedor.</summary>
        UnknownProvider,
    }

    /// <summary>Resultado da validação real da credencial contra o provedor.</summary>
    public sealed class ProviderHealthResult
    {
        public ProviderHealthStatus Status { get; set; }

        /// <summary>Provedor testado (quando identificado).</summary>
        public IAiProvider? Provider { get; set; }

        /// <summary>Código HTTP observado, quando houver.</summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>Mensagem curta e segura (NUNCA contém a chave).</summary>
        public string Message { get; set; } = string.Empty;
    }
}
