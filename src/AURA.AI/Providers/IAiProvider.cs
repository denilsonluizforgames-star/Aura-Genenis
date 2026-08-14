namespace AURA.AI.Providers
{
    /// <summary>
    /// Abstração de um provedor de IA. Cada implementação descreve como a
    /// AURA deve autenticar, qual formato de API usar e quais características
    /// da API key permitem identificá-lo de forma determinística.
    /// </summary>
    public interface IAiProvider
    {
        /// <summary>Nome de exibição (deve casar com ProviderCatalog).</summary>
        string Name { get; }

        /// <summary>URL base do endpoint de chat.</summary>
        string BaseUrl { get; }

        /// <summary>URL usada para listar/validar credencial (GET models).</summary>
        string ModelsUrl { get; }

        /// <summary>Se exige chave de API.</summary>
        bool NeedsKey { get; }

        /// <summary>Modelo padrão sugerido após detecção.</summary>
        string DefaultModelId { get; }

        /// <summary>Nome do header de autenticação (ex.: "Authorization", "x-api-key").</summary>
        string AuthHeaderName { get; }

        /// <summary>Prefixo do esquema de autenticação (ex.: "Bearer ").</summary>
        string AuthScheme { get; }

        /// <summary>Formato de API usado pelo provedor.</summary>
        AiApiFormat ApiFormat { get; }

        /// <summary>Versão do header Anthropic quando o formato é AnthropicMessages.</summary>
        string AnthropicVersion { get; }

        /// <summary>
        /// Prefixos confiáveis da chave. Uma chave que começa com um destes
        /// prefixos aponta com alta confiança para este provedor.
        /// </summary>
        IReadOnlyList<string> KeyPrefixes { get; }
    }
}
