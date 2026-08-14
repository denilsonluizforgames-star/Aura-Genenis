using System.Threading;
using System.Threading.Tasks;
using AURA.AI;

namespace AURA.AI.Providers
{
    /// <summary>
    /// Resolve a qual provedor de IA pertence uma API key e valida a
    /// credencial. Desacoplado da UI: recebe um ProviderCredential e devolve
    /// resultados estruturados. A detecção determinística por formato da chave
    /// nunca faz rede; o teste de endpoints só ocorre com AllowProbe=true.
    /// </summary>
    public interface IApiKeyProviderResolver
    {
        /// <summary>
        /// Identifica o provedor pela chave, sem rede. Usa o formato/prefixo
        /// da chave primeiro; se ambíguo, usa o provedor preferido como
        /// contexto (nunca envia a chave a terceiros aqui).
        /// </summary>
        ProviderDetectionResult Detect(ProviderCredential credential);

        /// <summary>
        /// Valida a credencial contra o provedor identificado (ou, com
        /// AllowProbe, contra os provedores compatíveis em ordem de confiança).
        /// Nunca registra a chave em logs.
        /// </summary>
        Task<ProviderHealthResult> ValidateAsync(
            ProviderCredential credential,
            System.Net.Http.HttpClient? http = null,
            CancellationToken ct = default);

        /// <summary>
        /// Detecta e, se preciso e autorizado, testa endpoints para configurar
        /// automaticamente provedor/base/modelo.
        /// </summary>
        Task<ProviderDetectionResult> ResolveAsync(
            ProviderCredential credential,
            System.Net.Http.HttpClient? http = null,
            CancellationToken ct = default);

        /// <summary>
        /// Aplica o resultado da detecção/validação num OpenRouterClient:
        /// base URL, header de autenticação, formato e modelo padrão.
        /// </summary>
        void ApplyToClient(OpenRouterClient client, ProviderDetectionResult result);
    }
}
