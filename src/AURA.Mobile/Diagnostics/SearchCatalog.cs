using System.Collections.Generic;

namespace AURA.Mobile.Diagnostics
{
    public sealed class SearchEngine
    {
        public string Name { get; init; } = string.Empty;
        public string SearchUrl { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
    }

    public sealed class ImageSearchProvider
    {
        public string Name { get; init; } = string.Empty;
        public string ByUrlTemplate { get; init; } = string.Empty;
        public string UploadEndpoint { get; init; } = string.Empty;
        public bool SupportsUpload => !string.IsNullOrWhiteSpace(UploadEndpoint);
    }

    /// <summary>
    /// Catálogo de provedores de pesquisa (texto) e de busca de imagem reversa.
    /// Todos usam URLs públicas (sem API key) para abrir direto no WebView.
    /// </summary>
    public static class SearchCatalog
    {
        public static List<SearchEngine> Engines { get; } = new()
        {
            new SearchEngine { Name = "Google", SearchUrl = "https://www.google.com/search?q={0}",
                Note = "Resultados por popularidade; operadores: site:, filetype:, intitle:, \"exata\"" },
            new SearchEngine { Name = "DuckDuckGo", SearchUrl = "https://duckduckgo.com/?q={0}",
                Note = "Privacidade; operadores: site:, filetype:, !bang (ex.: !w)" },
            new SearchEngine { Name = "Startpage", SearchUrl = "https://www.startpage.com/sp/search?query={0}",
                Note = "Privacidade + resultados do Google; operadores do Google" },
            new SearchEngine { Name = "Brave", SearchUrl = "https://search.brave.com/search?q={0}",
                Note = "Índice próprio, foco em privacidade" },
            new SearchEngine { Name = "Bing", SearchUrl = "https://www.bing.com/search?q={0}",
                Note = "Operadores: site:, filetype:, contains:" },
            new SearchEngine { Name = "Yandex", SearchUrl = "https://yandex.com/search/?text={0}",
                Note = "Bom para imagem e RU" },
            new SearchEngine { Name = "Mojeek", SearchUrl = "https://www.mojeek.com/search?q={0}",
                Note = "Índice independente, sem rastreio" }
        };

        public static List<ImageSearchProvider> ImageProviders { get; } = new()
        {
            new ImageSearchProvider
            {
                Name = "Google Lens",
                ByUrlTemplate = "https://lens.google.com/uploadbyurl?url={0}",
                UploadEndpoint = "https://lens.google.com/v3/upload"
            },
            new ImageSearchProvider
            {
                Name = "Bing Visual",
                ByUrlTemplate = "https://www.bing.com/images/search?q=imgurl:{0}&view=detailv2&iss=sbi",
                UploadEndpoint = "https://www.bing.com/images/search?view=detailv2&iss=sbi&form=SBIHMP&sbisrc=Upload"
            },
            new ImageSearchProvider
            {
                Name = "TinEye",
                ByUrlTemplate = "https://tineye.com/search?url={0}",
                UploadEndpoint = ""
            },
            new ImageSearchProvider
            {
                Name = "Yandex",
                ByUrlTemplate = "https://yandex.com/images/search?rpt=imageview&url={0}",
                UploadEndpoint = ""
            }
        };
    }
}
