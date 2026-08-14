using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AURA.AI;

namespace AURA.AI.Providers
{
    /// <summary>
    /// Resolvedor padrão de provedor por API key.
    ///
    /// Ordem de decisão (regras 5 e 6):
    ///  1. Determinístico: prefixos confiáveis da chave (sem rede).
    ///  2. Se ambíguo ou sem prefixo conhecido: contexto (provedor preferido).
    ///  3. Se ainda inconclusivo e AllowProbe=true: testa apenas endpoints
    ///     compatíveis (GET /models) até achar o provedor.
    /// A chave nunca é enviada a terceiros sem AllowProbe e nunca é logada.
    /// </summary>
    public sealed class ApiKeyProviderResolver : IApiKeyProviderResolver
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

        public ProviderDetectionResult Detect(ProviderCredential credential)
        {
            string key = (credential.ApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                return new ProviderDetectionResult
                {
                    Source = ProviderDetectionSource.None,
                    Message = "Chave vazia."
                };
            }

            // 1) Formato da chave (determinístico, sem rede).
            // Coleta o provedor cujo prefixo mais específico (mais longo)
            // casa com a chave. Ex.: "sk-or-" vence "sk-" para chaves OpenRouter.
            var matched = new List<IAiProvider>();
            int longestPrefix = 0;
            foreach (ProviderInfo p in ProviderCatalog.Providers)
            {
                if (!p.NeedsKey) continue;
                foreach (string prefix in p.KeyPrefixes)
                {
                    if (key.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        if (prefix.Length > longestPrefix)
                        {
                            longestPrefix = prefix.Length;
                            matched.Clear();
                            matched.Add(p);
                        }
                        else if (prefix.Length == longestPrefix)
                        {
                            matched.Add(p);
                        }

                        break;
                    }
                }
            }

            if (matched.Count == 1)
            {
                return new ProviderDetectionResult
                {
                    Provider = matched[0],
                    Source = ProviderDetectionSource.KeyFormat,
                    Message = "Provedor identificado pelo formato da chave: " + matched[0].Name + "."
                };
            }

            // 2) Ambigua ou sem prefixo: usa o contexto/provedor preferido.
            if (!string.IsNullOrWhiteSpace(credential.PreferredProviderName))
            {
                ProviderInfo? preferred = ProviderCatalog.Find(credential.PreferredProviderName);
                if (preferred != null)
                {
                    if (matched.Count > 1 && !matched.Contains(preferred))
                    {
                        // O formato aponta para outros; contexto não bate -> fica ambíguo.
                        return new ProviderDetectionResult
                        {
                            Candidates = matched,
                            Source = ProviderDetectionSource.None,
                            Message = "Chave ambígua (formato compatível com vários provedores); " +
                                      "o selecionado não é um deles."
                        };
                    }

                    return new ProviderDetectionResult
                    {
                        Provider = preferred,
                        Candidates = matched,
                        Source = matched.Count > 0
                            ? ProviderDetectionSource.KeyFormat
                            : ProviderDetectionSource.Context,
                        Message = "Sem prefixo de chave conhecido; usando o provedor selecionado: " +
                                  preferred.Name + "."
                    };
                }
            }

            if (matched.Count > 1)
            {
                return new ProviderDetectionResult
                {
                    Candidates = matched,
                    Source = ProviderDetectionSource.None,
                    Message = "Chave ambígua (formato compatível com " + matched.Count +
                              " provedores). Toque em 'Testar' para descobrir."
                };
            }

            return new ProviderDetectionResult
            {
                Candidates = ProviderCatalog.KeyedProbeCandidates() is var all
                    ? new List<IAiProvider>(all)
                    : Array.Empty<IAiProvider>(),
                Source = ProviderDetectionSource.None,
                Message = "Formato da chave desconhecido. Toque em 'Testar' para descobrir o provedor."
            };
        }

        public async Task<ProviderHealthResult> ValidateAsync(
            ProviderCredential credential,
            HttpClient? http = null,
            CancellationToken ct = default)
        {
            ProviderDetectionResult detection = Detect(credential);

            // Sem provedor candidato e sem autorização para testar.
            if (detection.Provider == null && detection.Candidates.Count == 0)
            {
                return new ProviderHealthResult
                {
                    Status = ProviderHealthStatus.UnknownProvider,
                    Message = "Não foi possível identificar o provedor."
                };
            }

            // Sem conclusão e sem autorização explícita: não envia a chave.
            if (detection.Provider == null && !credential.AllowProbe)
            {
                return new ProviderHealthResult
                {
                    Status = ProviderHealthStatus.UnknownProvider,
                    Message = "Provedor não identificado e teste externo não autorizado. " +
                              "Habilite a validação para testar os provedores compatíveis."
                };
            }

            var candidates = new List<IAiProvider>();
            if (detection.Provider != null) candidates.Add(detection.Provider);
            foreach (IAiProvider c in detection.Candidates)
            {
                if (!candidates.Contains(c)) candidates.Add(c);
            }

            // Ordena: preferido primeiro, depois por número de prefixos casados (mais específico).
            HttpClient client = http ?? new HttpClient();
            bool ownsClient = http == null;

            ProviderHealthResult? best = null;
            foreach (IAiProvider provider in candidates)
            {
                if (!provider.NeedsKey) continue;

                ProviderHealthResult r = await ProbeAsync(
                    client, provider, credential.ApiKey, credential.Timeout ?? DefaultTimeout, ct);

                if (r.Status == ProviderHealthStatus.Valid)
                {
                    return r; // Achou.
                }

                // Guarda o resultado mais informativo (Unauthorized é forte).
                if (best == null || Prefer(best.Status, r.Status))
                {
                    best = r;
                }

                if (!credential.AllowProbe) break; // só testa o preferido/contexto
            }

            if (ownsClient) client.Dispose();

            if (best == null)
            {
                return new ProviderHealthResult
                {
                    Status = ProviderHealthStatus.UnknownProvider,
                    Message = "Nenhum provedor compatível pôde ser testado."
                };
            }

            if (best.Status == ProviderHealthStatus.Unauthorized &&
                candidates.Count > 1)
            {
                best.Message = "Chave rejeitada pelos provedores testados (" +
                               best.Provider!.Name + " e outros).";
            }

            return best;
        }

        public async Task<ProviderDetectionResult> ResolveAsync(
            ProviderCredential credential,
            HttpClient? http = null,
            CancellationToken ct = default)
        {
            ProviderDetectionResult detection = Detect(credential);

            if (detection.Provider != null)
            {
                // Mesmo conclusivo pelo formato, valida a credencial de verdade.
                ProviderHealthResult health = await ValidateAsync(credential, http, ct);
                detection.Message = detection.Message + " " + health.Message;
            }
            else if (credential.AllowProbe && detection.Candidates.Count > 0)
            {
                ProviderHealthResult health = await ValidateAsync(credential, http, ct);
                if (health.Provider != null && health.Status == ProviderHealthStatus.Valid)
                {
                    detection.Provider = health.Provider;
                    detection.Source = ProviderDetectionSource.Probe;
                    detection.Message = "Provedor descoberto testando os endpoints: " +
                                        health.Provider.Name + ".";
                }
                else
                {
                    detection.Message = detection.Message + " " + health.Message;
                }
            }

            return detection;
        }

        public void ApplyToClient(OpenRouterClient client, ProviderDetectionResult result)
        {
            if (client == null || result.Provider is not ProviderInfo p)
            {
                return;
            }

            client.Options.BaseUrl = p.BaseUrl;
            client.Options.Model = string.IsNullOrWhiteSpace(p.DefaultModelId) && p.Models.Count > 0
                ? p.Models[0].Id
                : p.DefaultModelId;
            client.Options.AuthHeaderName = p.AuthHeaderName;
            client.Options.AuthScheme = p.AuthScheme;
            client.Options.ApiFormat = p.ApiFormat;
            client.Options.AnthropicVersion = p.AnthropicVersion;
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private static bool Prefer(ProviderHealthStatus current, ProviderHealthStatus next)
        {
            // Unauthorized indica chave rejeitada (provedor errado); é o sinal
            // mais confiável para descartar um provedor. ProviderUnavailable e
            // Invalid são mais fracos.
            return next == ProviderHealthStatus.Unauthorized &&
                   current != ProviderHealthStatus.Unauthorized;
        }

        private static async Task<ProviderHealthResult> ProbeAsync(
            HttpClient client, IAiProvider provider, string key, TimeSpan timeout, CancellationToken ct)
        {
            var result = new ProviderHealthResult { Provider = provider };
            string url = string.IsNullOrWhiteSpace(provider.ModelsUrl) ? provider.BaseUrl : provider.ModelsUrl;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(key))
            {
                request.Headers.TryAddWithoutValidation(
                    provider.AuthHeaderName, provider.AuthScheme + key);
            }

            try
            {
                HttpResponseMessage response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
                result.HttpStatusCode = (int)response.StatusCode;
                string body = string.Empty;
                try
                {
                    body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch
                {
                    // corpo opcional; não é essencial para o status
                }

                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        result.Status = ProviderHealthStatus.Valid;
                        result.Message = "Credencial válida em " + provider.Name + ".";
                        break;

                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden:
                        result.Status = ProviderHealthStatus.Unauthorized;
                        result.Message = "Chave rejeitada por " + provider.Name +
                                         " (" + (int)response.StatusCode + ").";
                        break;

                    case (HttpStatusCode)402:
                    case (HttpStatusCode)429:
                        result.Status = ProviderHealthStatus.InsufficientCredits;
                        result.Message = provider.Name + " aceitou a chave mas está sem " +
                                         "créditos/cota (" + (int)response.StatusCode + ").";
                        break;

                    case HttpStatusCode.BadRequest:
                    case HttpStatusCode.NotFound:
                        result.Status = ProviderHealthStatus.Invalid;
                        result.Message = "Endpoint inválido para " + provider.Name +
                                         " (" + (int)response.StatusCode + ").";
                        break;

                    default:
                        if ((int)response.StatusCode >= 500)
                        {
                            result.Status = ProviderHealthStatus.ProviderUnavailable;
                            result.Message = provider.Name + " indisponível (" +
                                             (int)response.StatusCode + ").";
                        }
                        else
                        {
                            result.Status = ProviderHealthStatus.Invalid;
                            result.Message = "Resposta inesperada de " + provider.Name +
                                             " (" + (int)response.StatusCode + ").";
                        }
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                result.Status = ProviderHealthStatus.ProviderUnavailable;
                result.Message = "Timeout ao contatar " + provider.Name + ".";
            }
            catch (HttpRequestException hre)
            {
                result.Status = ProviderHealthStatus.ProviderUnavailable;
                result.Message = "Falha de rede ao contatar " + provider.Name +
                                 (hre.InnerException != null ? " (" + hre.InnerException.GetType().Name + ")" : string.Empty) + ".";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.Status = ProviderHealthStatus.ProviderUnavailable;
                result.Message = "Falha ao contatar " + provider.Name + ".";
            }

            return result;
        }
    }
}
