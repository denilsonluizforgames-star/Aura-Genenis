namespace AURA.Mobile.Pages
{
    /// <summary>
    /// Configurações do navegador embutido: JavaScript on/off, perfil de
    /// user-agent (padrão / desktop / personalizado) e página inicial.
    /// Tudo é salvo em Preferences na hora; o BrowserPage reaplica ao voltar.
    /// </summary>
    public sealed class BrowserSettingsPage : ContentPage
    {
        public BrowserSettingsPage()
        {
            Title = "Navegador · Configurações";
            BackgroundColor = Color.FromArgb("#101014");

            var stack = new VerticalStackLayout { Padding = 20, Spacing = 18 };

            // JavaScript
            var jsSwitch = new Switch
            {
                IsToggled = Preferences.Default.Get(BrowserPage.JsEnabledKey, true),
                OnColor = Color.FromArgb("#4c6ef5")
            };
            jsSwitch.Toggled += (s, e) => Preferences.Default.Set(BrowserPage.JsEnabledKey, e.Value);
            stack.Add(Row("JavaScript habilitado", jsSwitch));

            var adsSwitch = new Switch
            {
                IsToggled = Preferences.Default.Get(BrowserPage.AdsEnabledKey, true),
                OnColor = Color.FromArgb("#4c6ef5")
            };
            adsSwitch.Toggled += (s, e) => Preferences.Default.Set(BrowserPage.AdsEnabledKey, e.Value);
            stack.Add(Row("Bloquear anúncios (oculta banners)", adsSwitch));

            var stealthSwitch = new Switch
            {
                IsToggled = Preferences.Default.Get(BrowserPage.StealthEnabledKey, true),
                OnColor = Color.FromArgb("#4c6ef5")
            };
            stealthSwitch.Toggled += (s, e) => Preferences.Default.Set(BrowserPage.StealthEnabledKey, e.Value);
            stack.Add(Row("Anti-identificação (esconder WebView)", stealthSwitch));

            stack.Add(new Label
            {
                Text = "Anti-identificação: usa um User-Agent de Chrome comum (sem o marcador \"wv\") e mascara sinais de WebView, para o site não detectar um navegador embutido/espaço separado. Na célula isolada essa proteção é sempre forçada.",
                FontSize = 11,
                TextColor = Color.FromArgb("#8a8a95")
            });

            stack.Add(new Label
            {
                Text = "Captura de tela fica bloqueada enquanto o navegador estiver aberto (sem prints nem pré-visualização no app switcher).",
                FontSize = 11,
                TextColor = Color.FromArgb("#8a8a95")
            });

            // User-Agent
            stack.Add(new Label
            {
                Text = "User-Agent",
                FontSize = 12,
                TextColor = Color.FromArgb("#8a8a95")
            });

            var uaPicker = new Picker
            {
                Title = "Perfil de User-Agent",
                TextColor = Color.FromArgb("#f2f2f5"),
                ItemsSource = new[] { "Padrão do dispositivo", "Desktop", "Personalizado" }
            };
            uaPicker.SelectedIndex = Math.Clamp(Preferences.Default.Get(BrowserPage.UserAgentModeKey, 0), 0, 2);
            uaPicker.SelectedIndexChanged += (s, e) =>
                Preferences.Default.Set(BrowserPage.UserAgentModeKey, uaPicker.SelectedIndex);
            stack.Add(uaPicker);

            var customUaEntry = new Entry
            {
                Placeholder = "User-agent personalizado",
                Text = Preferences.Default.Get(BrowserPage.UserAgentCustomKey, string.Empty),
                TextColor = Color.FromArgb("#f2f2f5"),
                PlaceholderColor = Color.FromArgb("#5a5a66"),
                BackgroundColor = Color.FromArgb("#1b1b22"),
                IsVisible = uaPicker.SelectedIndex == 2
            };
            customUaEntry.TextChanged += (s, e) => Preferences.Default.Set(BrowserPage.UserAgentCustomKey, e.NewTextValue);
            uaPicker.SelectedIndexChanged += (s, e) => customUaEntry.IsVisible = uaPicker.SelectedIndex == 2;
            stack.Add(customUaEntry);

            // Página inicial
            stack.Add(new Label
            {
                Text = "Página inicial",
                FontSize = 12,
                TextColor = Color.FromArgb("#8a8a95")
            });
            var homeEntry = new Entry
            {
                Placeholder = "https://...",
                Text = Preferences.Default.Get(BrowserPage.HomeUrlKey, string.Empty),
                Keyboard = Keyboard.Url,
                TextColor = Color.FromArgb("#f2f2f5"),
                PlaceholderColor = Color.FromArgb("#5a5a66"),
                BackgroundColor = Color.FromArgb("#1b1b22")
            };
            homeEntry.TextChanged += (s, e) => Preferences.Default.Set(BrowserPage.HomeUrlKey, e.NewTextValue);
            stack.Add(homeEntry);

            Content = new ScrollView { Content = stack };
        }

        private static View Row(string title, View right)
        {
            var grid = new Grid
            {
                ColumnSpacing = 12,
                VerticalOptions = LayoutOptions.Center
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Auto)));
            grid.Add(new Label
            {
                Text = title,
                FontSize = 15,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#f2f2f5")
            });
            grid.Add(right);
            Grid.SetColumn(right, 1);
            return grid;
        }
    }
}