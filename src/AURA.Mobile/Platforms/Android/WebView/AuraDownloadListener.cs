namespace AURA.Mobile.Platforms.Android.WebView
{
    /// <summary>
    /// Abre downloads/arquivos (ex.: um link que dispara um download) no
    /// navegador/app externo do aparelho, já que o WebView embutido não tem
    /// gerenciador próprio de downloads.
    /// </summary>
    public sealed class AuraDownloadListener : Java.Lang.Object, global::Android.Webkit.IDownloadListener
    {
        public void OnDownloadStart(
            string? url,
            string? userAgent,
            string? contentDisposition,
            string? mimeType,
            long contentLength)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                var context = global::Android.App.Application.Context;
                var uri = global::Android.Net.Uri.Parse(url!);
                if (uri == null)
                {
                    return;
                }

                var intent = new global::Android.Content.Intent(
                    global::Android.Content.Intent.ActionView,
                    uri);
                intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);

                AURA.Mobile.AuraLog.Info("WebView: download/recurso aberto externamente: " + url);
            }
            catch (System.Exception ex)
            {
                AURA.Mobile.AuraLog.Exception("WebView.Download", ex);
            }
        }
    }
}