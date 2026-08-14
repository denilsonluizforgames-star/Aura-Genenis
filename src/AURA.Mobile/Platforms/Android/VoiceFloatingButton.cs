using System;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using AURA.Mobile.Speech;
using Microsoft.Extensions.DependencyInjection;
using Button = Android.Widget.Button;
using Color = Android.Graphics.Color;

namespace AURA.Mobile.Platforms.Android
{
    /// <summary>
    /// Botão flutuante de voz (FAB) anexado ao decor view da Activity, visível
    /// em TODAS as abas do app. Um toque fala a última resposta da IA (ou a
    /// saudação de assistente); outro toque para a fala.
    ///
    /// Implementado em Android nativo porque o MAUI não expõe overlay global
    /// sobre o TabbedPage sem reestruturar o layout.
    /// </summary>
    public static class VoiceFloatingButton
    {
        private static Button? _fab;
        private static bool _attached;

        /// <summary>
        /// Cria o FAB sobre o conteúdo da Activity, no canto inferior direito,
        /// acima da barra de abas. Chamado depois do OnCreate da Activity.
        /// </summary>
        public static void Attach(Activity activity)
        {
            if (_attached)
            {
                return;
            }

            _attached = true;

            if (activity?.Window == null || activity.Window.DecorView is not ViewGroup decor)
            {
                return;
            }

            // FAB circular com o padrão visual da AURA (accent #4f8aff).
            var fab = new Button(activity)
            {
                Text = "🔊",
                TextSize = 20
            };
            fab.SetAllCaps(false);
            fab.SetBackgroundDrawable(CreateCircle(activity, Color.ParseColor("#4f8aff"), Color.White));
            fab.SetTextColor(Color.White);
            fab.Gravity = GravityFlags.Center;

            int size = Dp(activity, 56);
            int marginEnd = Dp(activity, 18);
            int marginBottom = Dp(activity, 76); // acima da barra de abas

            var lp = new FrameLayout.LayoutParams(size, size)
            {
                Gravity = GravityFlags.Bottom | GravityFlags.End,
                RightMargin = marginEnd,
                BottomMargin = marginBottom
            };
            fab.LayoutParameters = lp;

            fab.Click += OnFabClicked;
            decor.AddView(fab);
            _fab = fab;

            AuraLog.Info("VoiceFloatingButton.Attach OK");
        }

        /// <summary>Remove o FAB da tela (OnDestroy da Activity).</summary>
        public static void Detach()
        {
            if (_fab?.Parent is ViewGroup parent)
            {
                _fab.Click -= OnFabClicked;
                parent.RemoveView(_fab);
            }

            _fab = null;
            _attached = false;
        }

        private static async void OnFabClicked(object? sender, EventArgs e)
        {
            try
            {
                var services = Microsoft.Maui.IPlatformApplication.Current?.Services;
                var voice = services?.GetService<VoiceAssistantService>();
                if (voice == null)
                {
                    return;
                }

                await voice.ToggleAsync();

                // Feedback visual: mostra "■" enquanto fala, "🔊" quando parado.
                if (sender is Button fab)
                {
                    fab.Post(() =>
                    {
                        fab.Text = voice.IsSpeaking ? "■" : "🔊";
                    });
                }
            }
            catch (Exception ex)
            {
                AuraLog.Exception("VoiceFloatingButton.Click", ex);
            }
        }

        private static GradientDrawable CreateCircle(Activity activity, Color stroke, Color fill)
        {
            var d = new GradientDrawable();
            d.SetShape(ShapeType.Oval);
            d.SetColor(fill.ToArgb());
            d.SetStroke(Dp(activity, 1), stroke);
            return d;
        }

        private static int Dp(Activity activity, float value)
        {
            return (int)(value * activity.Resources!.DisplayMetrics!.Density);
        }
    }
}
