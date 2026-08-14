using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AURA.AI
{
    /// <summary>
    /// Busca na internet aberta (sem login, sem chave de API) e retorna os
    /// principais resultados (título, URL e trecho) ao modelo. Usa o endpoint
    /// HTML público do Bing como fonte primária e o DuckDuckGo HTML como
    /// fallback — nenhum exige autenticação. Ferramenta pensada para o agente
    /// "refazer a solicitação do usuário na web" quando a pergunta precisa de
    /// informação atual ou externa, inclusive sem chave de API configurada.
    /// </summary>
    public sealed class WebSearchTool : AgentTool
    {
        private const int DefaultTimeoutSeconds = 30;
        private const int MaxResults = 8;
        private const int MaxResultChars = 220;
        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36";

        private readonly HttpClient _http;

        /// <param name="httpClient">Cliente HTTP compartilhado (opcional).</param>
        public WebSearchTool(HttpClient? httpClient = null)
        {
            _http = httpClient ?? CreateDefaultClient();
        }

        private static HttpClient CreateDefaultClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent", UserAgent);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
            return client;
        }

        public override AgentToolDefinition Definition => new AgentToolDefinition
        {
            Name = "web_search",
            Description = "Pesquisa na internet aberta (sem login, sem chave de API) " +
                "e retorna os principais resultados: título, URL e trecho de cada página. " +
                "Use para responder perguntas que exigem informação atual, notícias, " +
                "documentação externa ou conteúdo que não está no workspace.",
            Parameters =
            {
                ["query"] = new AgentToolParameter
                {
                    Type = "string",
                    Description = "Termos de busca em texto livre (idioma da pergunta do usuário)."
                }
            },
            Required = { "query" }
        };

        public override async Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
        {
            string query;
            using (JsonDocument doc = JsonDocument.Parse(argumentsJson))
            {
                query = ReadString(doc.RootElement, "query") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return "ERRO: query vazia.";
            }

            try
            {
                List<WebResult> results = await SearchAsync(query, ct).ConfigureAwait(false);
                return FormatForLlm(query, results);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return "ERRO: tempo de busca esgotado (" + DefaultTimeoutSeconds + "s).";
            }
            catch (Exception ex)
            {
                return "ERRO: falha na busca web: " + ex.Message;
            }
        }

        public override async Task<AgentToolResult> ExecuteStructuredAsync(
            string argumentsJson, CancellationToken ct = default)
        {
            string query;
            using (JsonDocument doc = JsonDocument.Parse(argumentsJson))
            {
                query = ReadString(doc.RootElement, "query") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return AgentToolResult.Error("ERRO: query vazia.");
            }

            try
            {
                List<WebResult> results = await SearchAsync(query, ct).ConfigureAwait(false);
                if (results.Count == 0)
                {
                    return AgentToolResult.Ok("(nenhum resultado encontrado na busca web.)");
                }

                return AgentToolResult.Ok(FormatForLlm(query, results));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return AgentToolResult.Error("ERRO: tempo de busca esgotado (" + DefaultTimeoutSeconds + "s).");
            }
            catch (Exception ex)
            {
                return AgentToolResult.Error("ERRO: falha na busca web: " + ex.Message);
            }
        }

        private async Task<List<WebResult>> SearchAsync(string query, CancellationToken ct)
        {
            string bingUrl = "https://www.bing.com/search?q=" +
                Uri.EscapeDataString(query) + "&setlang=pt-br";

            try
            {
                string html = await GetHtmlAsync(bingUrl, ct).ConfigureAwait(false);
                List<WebResult> bingResults = ParseBing(html);
                if (bingResults.Count > 0)
                {
                    return bingResults;
                }
            }
            catch (Exception)
            {
                // Fallback para DuckDuckGo abaixo.
            }

            string ddgUrl = "https://html.duckduckgo.com/html/?q=" +
                Uri.EscapeDataString(query);
            string ddgHtml = await GetHtmlAsync(ddgUrl, ct).ConfigureAwait(false);
            return ParseDdg(ddgHtml);
        }

        private async Task<string> GetHtmlAsync(string url, CancellationToken ct)
        {
            using (HttpResponseMessage response = await _http.GetAsync(url, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
        }

        private static List<WebResult> ParseBing(string html)
        {
            var results = new List<WebResult>();

            foreach (string block in SplitBlocks(html, "<li class=\"b_algo\""))
            {
                string? title = ExtractBetween(block, "<h2", "</h2>");
                if (title == null)
                {
                    continue;
                }

                string? url = ExtractHref(title);
                string cleanTitle = StripHtml(title);

                string? snippet = ExtractBetween(block, "<p", "</p>");
                string cleanSnippet = snippet != null ? StripHtml(snippet).Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(cleanTitle) || string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                results.Add(new WebResult
                {
                    Title = cleanTitle,
                    Url = url,
                    Snippet = cleanSnippet
                });

                if (results.Count >= MaxResults)
                {
                    break;
                }
            }

            return results;
        }

        private static List<WebResult> ParseDdg(string html)
        {
            var results = new List<WebResult>();

            foreach (string block in SplitBlocks(html, "class=\"result__a\""))
            {
                string? title = ExtractBetween(block, ">", "</a>");
                string? url = ExtractHref(block);
                string? snippet = ExtractBetween(block, "result__snippet", "</a>");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                results.Add(new WebResult
                {
                    Title = StripHtml(title).Trim(),
                    Url = url,
                    Snippet = snippet != null ? StripHtml(snippet).Trim() : string.Empty
                });

                if (results.Count >= MaxResults)
                {
                    break;
                }
            }

            return results;
        }

        private static IEnumerable<string> SplitBlocks(string text, string marker)
        {
            int idx = 0;
            while (idx < text.Length)
            {
                int start = text.IndexOf(marker, idx, StringComparison.Ordinal);
                if (start < 0)
                {
                    yield break;
                }

                int next = text.IndexOf(marker, start + marker.Length, StringComparison.Ordinal);
                int end = next < 0 ? text.Length : next;
                yield return text.Substring(start, end - start);

                idx = end;
            }
        }

        private static string? ExtractBetween(string text, string startAnchor, string endAnchor)
        {
            int a = text.IndexOf(startAnchor, StringComparison.Ordinal);
            if (a < 0)
            {
                return null;
            }

            int b = text.IndexOf('>', a);
            if (b < 0)
            {
                return null;
            }

            b += 1;
            int c = text.IndexOf(endAnchor, b, StringComparison.Ordinal);
            return c < 0 ? null : text.Substring(b, c - b);
        }

        private static string? ExtractHref(string anchorHtml)
        {
            int a = anchorHtml.IndexOf("href=\"", StringComparison.Ordinal);
            if (a < 0)
            {
                return null;
            }

            a += "href=\"".Length;
            int b = anchorHtml.IndexOf('"', a);
            if (b < 0)
            {
                return null;
            }

            string url = WebUtility.HtmlDecode(anchorHtml.Substring(a, b - a)).Trim();
            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                url = "https:" + url;
            }

            return url;
        }

        private static string StripHtml(string text)
        {
            return WebUtility.HtmlDecode(
                System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " "))
                    .Replace("&amp;", "&").Replace("&quot;", "\"")
                    .Replace("&#x27;", "'").Replace("&lt;", "<").Replace("&gt;", ">")
                    .Replace("  ", " ");
        }

        private static string FormatForLlm(string query, List<WebResult> results)
        {
            if (results.Count == 0)
            {
                return "(nenhum resultado encontrado para: " + query + ")";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("Resultados da busca web para \"").Append(query).AppendLine("\":");

            for (int i = 0; i < results.Count; i++)
            {
                WebResult r = results[i];
                sb.Append('\n').Append(i + 1).Append(". ").AppendLine(r.Title);

                string snippet = r.Snippet ?? string.Empty;
                if (snippet.Length > MaxResultChars)
                {
                    snippet = snippet.Substring(0, MaxResultChars) + "\u2026";
                }

                if (snippet.Length > 0)
                {
                    sb.AppendLine(snippet);
                }

                sb.AppendLine(r.Url);
            }

            return sb.ToString();
        }

        private sealed class WebResult
        {
            public string Title { get; set; } = string.Empty;

            public string Url { get; set; } = string.Empty;

            public string Snippet { get; set; } = string.Empty;
        }
    }
}