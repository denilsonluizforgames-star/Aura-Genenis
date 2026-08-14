namespace AURA.Mobile.Platforms.Android.WebView
{
    /// <summary>
    /// Captura o toque longo (long-press) sobre uma imagem: consulta o
    /// HitTestResult do WebView e, se for uma imagem, publica o evento
    /// ImageLongPress (consumido pela BrowserPage para buscar imagem reversa).
    /// Em links/texto devolve false e mantém o menu de contexto nativo.
    /// </summary>
    public sealed class AuraLongClickListener : Java.Lang.Object, global::Android.Views.View.IOnLongClickListener
    {
        private readonly Action<global::Android.Webkit.WebView> _onImage;

        public AuraLongClickListener(Action<global::Android.Webkit.WebView> onImage)
        {
            _onImage = onImage;
        }

        public bool OnLongClick(global::Android.Views.View? v)
        {
            if (v is not global::Android.Webkit.WebView wv)
            {
                return false;
            }

            try
            {
                var hit = wv.GetHitTestResult();
                if (hit == null)
                {
                    return false;
                }

                int type = (int)hit.Type;
                bool isImage = type == (int)global::Android.Webkit.HitTestResult.ImageType
                    || type == (int)global::Android.Webkit.HitTestResult.SrcImageAnchorType;

                if (!isImage)
                {
                    return false;
                }

                string? url = hit.Extra;
                if (string.IsNullOrWhiteSpace(url))
                {
                    return false;
                }

                _onImage(wv);
                return true;
            }
            catch (System.Exception ex)
            {
                AURA.Mobile.AuraLog.Exception("WebView.LongPress", ex);
                return false;
            }
        }
    }
}