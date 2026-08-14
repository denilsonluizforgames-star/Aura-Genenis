namespace AURA.Mobile.Platforms.Android.WebView
{
    /// <summary>
    /// Evita que um contêiner pai (NavigationPage/TabbedPage) intercepte o gesto
    /// de rolar do WebView. No ACTION_DOWN pede ao pai para não interceptar
    /// (RequestDisallowInterceptTouchEvent) e devolve false, deixando o WebView
    /// nativo fazer o scroll normalmente.
    /// </summary>
    public sealed class AuraTouchListener : Java.Lang.Object, global::Android.Views.View.IOnTouchListener
    {
        public bool OnTouch(global::Android.Views.View? v, global::Android.Views.MotionEvent? e)
        {
            if (e?.Action == global::Android.Views.MotionEventActions.Down)
            {
                v?.Parent?.RequestDisallowInterceptTouchEvent(true);
            }
            return false;
        }
    }
}