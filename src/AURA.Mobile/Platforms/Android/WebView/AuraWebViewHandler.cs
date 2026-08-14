using Android.Views;
using Android.Webkit;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;

namespace AURA.Mobile.Platforms.Android.WebView
{
    /// <summary>
    /// Handler do WebView da AURA. Mantém o view/clients nativos do MAUI
    /// (MauiWebView implementa IWebViewDelegate e é quem de fato carrega a URL e
    /// dispara Navigating/Navigated), apenas endurece o WebView Android:
    ///   - rolagem: trava a interceptação do gesto pelos pais (fix de scroll);
    ///   - target=_blank abre na mesma aba (sem janela órfã/branca);
    ///   - conteúdo misto permitido (http dentro de https);
    ///   - downloads/recurso externos abertos no app padrão do sistema.
    /// </summary>
    public sealed class AuraWebViewHandler : WebViewHandler
    {
        /// <summary>
        /// Disparado quando o usuário faz toque longo numa imagem. O argumento é
        /// a URL da imagem (src). A BrowserPage usa para busca reversa.
        /// </summary>
        public static event Action<global::Android.Webkit.WebView, string>? ImageLongPress;

        static AuraWebViewHandler()
        {
            Mapper.AppendToMapping("AuraWebViewSetup", MapAuraSetup);
        }

        static void MapAuraSetup(IWebViewHandler handler, IWebView view)
        {
            var webView = handler.PlatformView;
            if (webView == null)
            {
                return;
            }

            try
            {
                var settings = webView.Settings;
                settings.DomStorageEnabled = true;
                settings.JavaScriptCanOpenWindowsAutomatically = true;
                settings.SetSupportMultipleWindows(false);
                settings.SetSupportZoom(false);

                if (OperatingSystem.IsAndroidVersionAtLeast(21))
                {
                    settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
                }

                webView.SetDownloadListener(new AuraDownloadListener());

                // Garante que o gesto de rolar chegue ao WebView.
                webView.OverScrollMode = OverScrollMode.Always;
                webView.SetOnTouchListener(new AuraTouchListener());

                // Toque longo em imagem → busca reversa.
                webView.SetOnLongClickListener(new AuraLongClickListener(wv =>
                {
                    string? url = wv.GetHitTestResult()?.Extra;
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        ImageLongPress?.Invoke(wv, url!);
                    }
                }));
            }
            catch (System.Exception ex)
            {
                AURA.Mobile.AuraLog.Exception("WebView.HandlerSetup", ex);
            }
        }
    }
}