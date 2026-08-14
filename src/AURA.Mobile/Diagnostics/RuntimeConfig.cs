using AURA.AI;

namespace AURA.Mobile.Diagnostics
{
    /// <summary>
    /// Configuração aplicável em tempo de execução (sem recompilar o APK).
    /// Toda alteração feita aqui persiste em Preferences e reflete
    /// imediatamente no OpenRouterClient.
    /// </summary>
    public static class RuntimeConfig
    {
        public static int MaxTokens
        {
            get => Preferences.Default.Get("ai_max_tokens", 1500);
            set => Preferences.Default.Set("ai_max_tokens", value);
        }

        public static int TimeoutSeconds
        {
            get => Preferences.Default.Get("ai_timeout_seconds", 90);
            set => Preferences.Default.Set("ai_timeout_seconds", value);
        }

        public static int LogLinesForAnalysis
        {
            get => Preferences.Default.Get("ai_log_lines", 120);
            set => Preferences.Default.Set("ai_log_lines", value);
        }

        public static string Provider
        {
            get => Preferences.Default.Get("ai_provider", string.Empty);
            set => Preferences.Default.Set("ai_provider", value);
        }

        public static string Model
        {
            get => Preferences.Default.Get("ai_model", string.Empty);
            set => Preferences.Default.Set("ai_model", value);
        }

        public static string ApiKey
        {
            get => Preferences.Default.Get("ai_api_key", string.Empty);
            set => Preferences.Default.Set("ai_api_key", value);
        }

        public static void Apply(OpenRouterClient client)
        {
            ProviderInfo provider = ProviderCatalog.Find(Provider);
            string model = Model;

            // O modelo salvo só vale se pertencer ao provedor resolvido; senão,
            // cai para o primeiro do provedor (evita mandar ID de outra API, ex. Groq na OpenRouter).
            bool modelBelongsToProvider = false;
            if (!string.IsNullOrWhiteSpace(model))
            {
                foreach (ProviderModel m in provider.Models)
                {
                    if (string.Equals(m.Id, model, System.StringComparison.OrdinalIgnoreCase))
                    {
                        modelBelongsToProvider = true;
                        break;
                    }
                }
            }

            if (!modelBelongsToProvider && provider.Models.Count > 0)
            {
                model = provider.Models[0].Id;
            }

            client.Options.BaseUrl = provider.BaseUrl;
            client.Options.Model = model;
            client.Options.MaxTokens = MaxTokens;
            client.Options.TimeoutSeconds = TimeoutSeconds;
            client.Options.ApiKey = ApiKey;
        }
    }
}
