using AURA.Mobile.Diagnostics;
using AURA.Core.Events;
using AURA.Core.Runtime;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace AURA.Mobile.Pages
{
    public partial class BrowserPage : ContentPage
    {
        public const string HomeUrlKey = "browser_home";
        public const string JsEnabledKey = "browser_js";
        public const string UserAgentModeKey = "browser_ua_mode";
        public const string UserAgentCustomKey = "browser_ua_custom";

        public const string EnginePrefKey = "browser_engine";
        public const string EngineNamePrefKey = "browser_engine_name";
        public const string AdsEnabledKey = "browser_ads";
        public const string StealthEnabledKey = "browser_stealth";

        private const string DefaultHome = "https://www.google.com";
        private const string DesktopUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        // Bloqueador de anúncios (esconde banners/pop-ups comuns via CSS; não é
        // bloqueio de rede, mas melhora a leitura em sites muito poluídos).
        private const string AdBlockScript =
            "var s=document.createElement('style');" +
            "s.textContent='ins.adsbygoogle,[id^=\"google_ads_iframe\"],[class*=\"advertisement\"]," +
            "[class*=\"ad-banner\"],[class*=\"sponsored\"],[class*=\"ads\"],.adsbox,#banner-ad,#ad-container" +
            "{display:none!important}';" +
            "document.head.appendChild(s);";

        // Anti-fingerprint: o WebView padrão expõe "wv", Version/4.0, navigator.
        // webdriver e window.chrome ausente — assinaturas que sites usam para
        // detectar "WebView/clone/espaço separado". Mascara para parecer um
        // Chrome comum rodando direto no aparelho.
        private const string StealthScript =
            "try{" +
            "window.chrome=window.chrome||{loadTimes:function(){return{}},csi:function(){return{}},runtime:{}};" +
            "Object.defineProperty(navigator,'webdriver',{get:function(){return undefined}});" +
            "if(navigator.plugins&&navigator.plugins.length===0){" +
            "Object.defineProperty(navigator,'plugins',{get:function(){return [" +
            "{name:'Chrome PDF Plugin',filename:'internal-pdf-viewer',description:'Portable Document Format'}," +
            "{name:'Chrome PDF Viewer',filename:'mhjfbmdgcfjbbpaeojofohoefgiehjai',description:''}," +
            "{name:'Native Client',filename:'internal-nacl-plugin',description:''}]}});" +
            "}" +
            "}catch(e){}";

        private readonly List<SearchEngine> _engines = SearchCatalog.Engines;
        private readonly ImageSearchPage _imageSearch;
        private readonly SimulationRuntime _runtime;
        private readonly EventBus _events;
        private readonly List<BrowserTab> _tabs = new();
        private BrowserTab? _active;
        private bool _initialized;
        private bool _isolated;
        private string? _isolatedCellId;

        public BrowserPage(ImageSearchPage imageSearch, SimulationRuntime runtime, EventBus events)
        {
            InitializeComponent();
            _imageSearch = imageSearch;
            _runtime = runtime;
            _events = events;

            NavigationPage.SetHasNavigationBar(this, false);

            _events.Subscribe<CellStateChangedEvent>(OnCellStateChanged);

#if ANDROID
            AURA.Mobile.Platforms.Android.WebView.AuraWebViewHandler.ImageLongPress += OnImageLongPress;
#endif
        }

        private void OnCellStateChanged(CellStateChangedEvent evt)
        {
            if (_isolatedCellId == null || evt.CellId != _isolatedCellId || evt.To != "Deleted")
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AuraLog.Info("Célula isolada '" + evt.CellId + "' excluída. Limpando dados de navegação.");
                _isolatedCellId = null;
                _isolated = false;
                IsolatedLabel.IsVisible = false;
                PurgeBrowsingData();
            });
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();

            SetScreenshotProtection(true);

            if (!_initialized)
            {
                _initialized = true;
                NewTab(HomeUrl());
            }

            ApplySettings();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            SetScreenshotProtection(false);
        }

        /// <summary>
        /// Bloqueia captura de tela/gravação e a pré-visualização no app switcher
        /// enquanto o navegador está na tela (FLAG_SECURE no window).
        /// </summary>
        private static void SetScreenshotProtection(bool on)
        {
#if ANDROID
            try
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                var window = activity?.Window;
                if (window == null)
                {
                    return;
                }

                var flag = global::Android.Views.WindowManagerFlags.Secure;
                if (on)
                {
                    window.AddFlags(flag);
                }
                else
                {
                    window.ClearFlags(flag);
                }
            }
            catch (System.Exception ex)
            {
                AURA.Mobile.AuraLog.Exception("Browser.ScreenshotProtection", ex);
            }
#endif
        }

        protected override bool OnBackButtonPressed()
        {
            if (_active?.View.CanGoBack == true)
            {
                _active.View.GoBack();
                return true;
            }

            return base.OnBackButtonPressed();
        }

        // --- Abas ---

        private sealed class BrowserTab
        {
            public int Id { get; }
            public WebView View { get; }
            public string Url { get; set; }
            public string Title { get; set; } = "Nova aba";
            public bool Active { get; set; }

            public BrowserTab(int id, WebView view, string url)
            {
                Id = id;
                View = view;
                Url = url;
            }
        }

        private void NewTab(string url)
        {
            var view = new WebView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

            var tab = new BrowserTab(_tabs.Count + 1, view, url);
            _tabs.Add(tab);
            TabHost.Add(view);
            view.IsVisible = false;

            view.Navigated += (s, e) =>
            {
                tab.Url = e.Url;
                if (tab.Active)
                {
                    UrlEntry.Text = e.Url;
                    tab.Title = TitleOf(e.Url);
                    UpdateNavState();
                    RefreshTabsChrome();
                    InjectAdBlocker(tab.View);
                    InjectStealth(tab.View);
                }
            };
            view.Navigating += (s, e) => AuraLog.Info("Browser: navegando para " + e.Url);

            ActivateTab(tab);
            view.Source = url;
        }

        private void ActivateTab(BrowserTab tab)
        {
            if (_active == tab)
            {
                UpdateNavState();
                return;
            }

            if (_active != null)
            {
                _active.Active = false;
            }

            _active = tab;
            tab.Active = true;

            foreach (BrowserTab t in _tabs)
            {
                t.View.IsVisible = t == tab;
            }

            UrlEntry.Text = tab.Url;
            UpdateNavState();
            RefreshTabsChrome();
        }

        private void CloseTab(BrowserTab tab)
        {
            if (_tabs.Count <= 1)
            {
                _active?.View.Source = HomeUrl();
                UrlEntry.Text = HomeUrl();
                return;
            }

            int idx = _tabs.IndexOf(tab);
            _tabs.Remove(tab);
            TabHost.Remove(tab.View);

            if (tab.Active)
            {
                ActivateTab(_tabs[Math.Min(idx, _tabs.Count - 1)]);
            }
            else
            {
                RefreshTabsChrome();
            }
        }

        private void RefreshTabsChrome()
        {
            TabBar.Children.Clear();

            foreach (BrowserTab tab in _tabs)
            {
                TabBar.Children.Add(BuildChip(tab));
            }
        }

        private View BuildChip(BrowserTab tab)
        {
            var title = new Button
            {
                Text = tab.Title,
                FontSize = 12,
                HeightRequest = 34,
                Padding = new Thickness(10, 0),
                CornerRadius = 6,
                BackgroundColor = tab.Active
                    ? Color.FromArgb("#2a2a33")
                    : Color.FromArgb("#1b1b22"),
                TextColor = Color.FromArgb("#f2f2f5")
            };
            title.Clicked += (s, e) => ActivateTab(tab);

            var close = new Button
            {
                Text = "✕",
                FontSize = 10,
                HeightRequest = 34,
                WidthRequest = 30,
                Padding = new Thickness(4, 0),
                CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#1b1b22"),
                TextColor = Color.FromArgb("#8a8a95")
            };
            close.Clicked += (s, e) => CloseTab(tab);

            var chip = new Grid
            {
                ColumnSpacing = 4,
                Padding = new Thickness(0)
            };
            chip.Add(title);
            chip.Add(close);
            Grid.SetColumn(close, 1);
            return chip;
        }

        private static string TitleOf(string url)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
                {
                    return uri.Host.Replace("www.", string.Empty);
                }
            }
            catch
            {
            }

            return "Nova aba";
        }

        // --- Navegação ---

        private void OnNewTabClicked(object sender, EventArgs e) => NewTab(HomeUrl());

        private void OnGoClicked(object sender, EventArgs e)
        {
            string input = UrlEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            if (IsOnionAddress(input))
            {
                HandleOnion(input);
                return;
            }

            LoadInActive(NormalizeUrl(input));
        }

        private void LoadInActive(string url)
        {
            if (_active == null)
            {
                return;
            }

            _active.View.Source = url;
            UrlEntry.Text = url;
        }

        private void OnBackClicked(object sender, EventArgs e)
        {
            if (_active?.View.CanGoBack == true)
            {
                _active.View.GoBack();
            }
        }

        private void OnForwardClicked(object sender, EventArgs e)
        {
            if (_active?.View.CanGoForward == true)
            {
                _active.View.GoForward();
            }
        }

        private void OnReloadClicked(object sender, EventArgs e)
        {
            if (_active != null)
            {
                _active.View.Reload();
            }
        }

        private void UpdateNavState()
        {
            BackButton.IsEnabled = _active?.View.CanGoBack == true;
            ForwardButton.IsEnabled = _active?.View.CanGoForward == true;
        }

        // --- Menu ---

        private async void OnMenuClicked(object sender, EventArgs e)
        {
            string action = await DisplayActionSheetAsync(
                "Navegador",
                "Cancelar",
                null,
                "Buscador padrão",
                "Buscar imagem",
                "VPN / Tor",
                "Nova célula isolada",
                "Fechar célula isolada",
                "Buscar na página",
                "Abrir página inicial",
                "Compartilhar link",
                "Copiar link",
                "Abrir externamente",
                "Configurações");

            switch (action)
            {
                case "Buscador padrão":
                    await PickEngineAsync();
                    break;
                case "Buscar imagem":
                    await PickImageSearchAsync();
                    break;
                case "VPN / Tor":
                    OpenVpn();
                    break;
                case "Nova célula isolada":
                    StartIsolatedSession();
                    break;
                case "Fechar célula isolada":
                    EndIsolatedSession();
                    break;
                case "Buscar na página":
                    ShowFindBar();
                    break;
                case "Abrir página inicial":
                    LoadInActive(HomeUrl());
                    break;
                case "Compartilhar link":
                    await ShareLinkAsync();
                    break;
                case "Copiar link":
                    await CopyLinkAsync();
                    break;
                case "Abrir externamente":
                    await OpenExternallyAsync();
                    break;
                case "Configurações":
                    await Navigation.PushAsync(new BrowserSettingsPage());
                    ApplySettings();
                    break;
            }
        }

        // --- Célula isolada (navegação sem deixar rastro) ---

        private void StartIsolatedSession()
        {
            if (_isolated)
            {
                DisplayAlert("Célula isolada", "Você já está numa sessão isolada (" + _isolatedCellId + ").", "OK");
                return;
            }

            string id = "web-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            try
            {
                // Célula real da AURA: tem diretório próprio, persiste em
                // cells.json e aparece em Células — mas não sobe processo (o
                // WebView roda no app). Ao excluir a célula, os dados de
                // navegação são limpos.
                _runtime.CreateCell(id, "com.aura.webview", "browser-isolado");
                _isolatedCellId = id;
                _isolated = true;
                IsolatedLabel.IsVisible = true;
                NewTab(HomeUrl());
                ApplySettings();
                AuraLog.Info("Célula isolada criada: " + id);
                DisplayAlert("Célula isolada",
                    "Navegação isolada ativa (" + id + ").\n\nNada de cookies, histórico ou cache fica salvo: ao fechar a célula (aqui ou em Células), todos os dados de navegação são apagados.",
                    "OK");
            }
            catch (Exception ex)
            {
                AuraLog.Exception("IsolatedCell.Start", ex);
                DisplayAlert("Célula isolada", "Não foi possível criar a célula: " + ex.Message, "OK");
            }
        }

        private void EndIsolatedSession()
        {
            if (!_isolated || _isolatedCellId == null)
            {
                return;
            }

            try
            {
                string id = _isolatedCellId;
                _isolated = false;
                _isolatedCellId = null;
                IsolatedLabel.IsVisible = false;
                _runtime.DeleteCell(id);
                PurgeBrowsingData();
            }
            catch (Exception ex)
            {
                AuraLog.Exception("IsolatedCell.End", ex);
            }
        }

        private async void OnIsolatedLabelTapped(object sender, EventArgs e)
        {
            await DisplayAlertAsync("Célula isolada",
                "Sessão isolada ativa (" + (_isolatedCellId ?? "-") + ").\n\nCookies, histórico e cache são apagados ao fechar a célula.",
                "OK");
        }

        private void PurgeBrowsingData()
        {
#if ANDROID
            try
            {
                var cm = global::Android.Webkit.CookieManager.Instance;
                if (OperatingSystem.IsAndroidVersionAtLeast(21))
                {
                    cm.RemoveAllCookies(null);
                    cm.Flush();
                }
                else
                {
                    cm.RemoveAllCookie();
                }

                global::Android.Webkit.WebStorage.Instance.DeleteAllData();

                foreach (BrowserTab tab in _tabs)
                {
                    (tab.View.Handler?.PlatformView as global::Android.Webkit.WebView)?.ClearCache(true);
                }

                AuraLog.Info("Célula isolada: dados de navegação apagados.");
            }
            catch (Exception ex)
            {
                AuraLog.Exception("IsolatedCell.Purge", ex);
            }
#endif
        }

        private async Task PickEngineAsync()
        {
            string[] names = _engines.Select(e => e.Name).ToArray();
            string chosen = await DisplayActionSheetAsync("Buscador padrão", "Cancelar", null, names);
            int idx = _engines.FindIndex(e => e.Name == chosen);
            if (idx < 0)
            {
                return;
            }

            Preferences.Default.Set(EnginePrefKey, idx);
            Preferences.Default.Set(EngineNamePrefKey, chosen);
        }

        private async Task PickImageSearchAsync()
        {
            string[] names = SearchCatalog.ImageProviders.Select(p => p.Name).ToArray();
            string chosen = await DisplayActionSheetAsync("Buscar imagem por", "Cancelar", null, names);
            ImageSearchProvider? provider = SearchCatalog.ImageProviders.FirstOrDefault(p => p.Name == chosen);
            if (provider == null || string.IsNullOrEmpty(_active?.Url))
            {
                return;
            }

            OpenImageSearch(provider, _active.Url);
        }

        private async Task ShareLinkAsync()
        {
            if (string.IsNullOrEmpty(_active?.Url))
            {
                return;
            }

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = _active.Url,
                Title = "Compartilhar link"
            });
        }

        private async Task CopyLinkAsync()
        {
            if (string.IsNullOrEmpty(_active?.Url))
            {
                return;
            }

            await Clipboard.Default.SetTextAsync(_active.Url);
        }

        private async Task OpenExternallyAsync()
        {
            if (string.IsNullOrEmpty(_active?.Url))
            {
                return;
            }

            try
            {
                await Browser.Default.OpenAsync(_active.Url, BrowserLaunchMode.External);
            }
            catch (Exception ex)
            {
                AuraLog.Exception("Browser.OpenExternal", ex);
            }
        }

        private void OpenVpn()
        {
#if ANDROID
            AURA.Mobile.Platforms.Android.VpnHelper.OpenVpnSettings();
#else
            LoadInActive("https://www.android.com/vpn/");
#endif
        }

        // --- Buscar imagem (toque longo na imagem) ---

        private async void OnImageLongPress(global::Android.Webkit.WebView wv, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            string[] names = SearchCatalog.ImageProviders.Select(p => p.Name).ToArray();
            string chosen = await DisplayActionSheetAsync("Buscar imagem", "Cancelar", null, names);
            ImageSearchProvider? provider = SearchCatalog.ImageProviders.FirstOrDefault(p => p.Name == chosen);
            if (provider == null)
            {
                return;
            }

            OpenImageSearch(provider, imageUrl);
        }

        private void OpenImageSearch(ImageSearchProvider provider, string imageUrl)
        {
            string encoded = Uri.EscapeDataString(imageUrl);
            NewTab(string.Format(provider.ByUrlTemplate, encoded));
        }

        // --- Busca na página ---

        private void ShowFindBar()
        {
            FindEntry.Text = string.Empty;
            FindResultLabel.Text = string.Empty;
            FindBar.IsVisible = true;
            FindEntry.Focus();
        }

        private void OnFindSubmit(object sender, EventArgs e) => RunFind(FindEntry.Text);

        private void OnFindPrev(object sender, EventArgs e) => FindNext(false);

        private void OnFindNext(object sender, EventArgs e) => FindNext(true);

        private void OnFindClose(object sender, EventArgs e)
        {
            FindBar.IsVisible = false;
            ClearFind();
        }

        private void RunFind(string? term)
        {
#if ANDROID
            var wv = ActivePlatformView();
            if (wv == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(term))
            {
                wv.ClearMatches();
                FindResultLabel.Text = string.Empty;
                return;
            }

            wv.SetFindListener(new AuraFindListener(count =>
                Dispatcher.Dispatch(() =>
                    FindResultLabel.Text = count > 0 ? count + " ocorrência(s)" : "nada encontrado")));
            wv.FindAllAsync(term);
            wv.FindNext(true);
#endif
        }

        private void FindNext(bool forward)
        {
#if ANDROID
            var wv = ActivePlatformView();
            if (wv != null)
            {
                wv.FindNext(forward);
            }
#endif
        }

        private void ClearFind()
        {
#if ANDROID
            ActivePlatformView()?.ClearMatches();
#endif
        }

#if ANDROID
        private Android.Webkit.WebView? ActivePlatformView() =>
            _active?.View.Handler?.PlatformView as Android.Webkit.WebView;

        private sealed class AuraFindListener : Java.Lang.Object, Android.Webkit.WebView.IFindListener
        {
            private readonly Action<int> _onCount;

            public AuraFindListener(Action<int> onCount) => _onCount = onCount;

            public void OnFindResultReceived(int numberOfMatches, int activeMatchOrdinal, bool isDoneCounting)
            {
                if (isDoneCounting)
                {
                    _onCount(numberOfMatches);
                }
            }
        }
#endif

        // --- Bloqueador de anúncios + anti-identificação (CSS/JS) ---

        private void InjectAdBlocker(WebView view)
        {
            if (!Preferences.Default.Get(AdsEnabledKey, true))
            {
                return;
            }

            try
            {
                view.EvaluateJavaScriptAsync(AdBlockScript);
            }
            catch (Exception ex)
            {
                AURA.Mobile.AuraLog.Exception("Browser.AdBlock", ex);
            }
        }

        private void InjectStealth(WebView view)
        {
            if (!_isolated && !Preferences.Default.Get(StealthEnabledKey, true))
            {
                return;
            }

            try
            {
                view.EvaluateJavaScriptAsync(StealthScript);
            }
            catch (Exception ex)
            {
                AURA.Mobile.AuraLog.Exception("Browser.Stealth", ex);
            }
        }

        // --- Configurações ---

        private void ApplySettings()
        {
            foreach (BrowserTab tab in _tabs)
            {
                ApplySettings(tab.View);
            }
        }

        private void ApplySettings(WebView view)
        {
#if ANDROID
            var wv = view.Handler?.PlatformView as Android.Webkit.WebView;
            if (wv == null)
            {
                return;
            }

            wv.Settings.JavaScriptEnabled = Preferences.Default.Get(JsEnabledKey, true);

            string ua = ResolveUserAgent();
            if (!string.IsNullOrEmpty(ua))
            {
                wv.Settings.UserAgentString = ua;
            }
#endif
        }

        private string ResolveUserAgent()
        {
            // Célula isolada força o mascaramento: nunca deixa o "wv" do WebView
            // vazar, para o site não identificar "WebView/espaço separado".
            if (_isolated || Preferences.Default.Get(StealthEnabledKey, true))
            {
                return CleanChromeUserAgent();
            }

            switch (Preferences.Default.Get(UserAgentModeKey, 0))
            {
                case 1:
                    return DesktopUserAgent;
                case 2:
                    return Preferences.Default.Get(UserAgentCustomKey, string.Empty);
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// User-Agent de um Chrome móvel comum construído com o modelo real do
        /// aparelho (Build), sem os marcadores "wv" / "Version/4.0" do WebView.
        /// </summary>
        private static string CleanChromeUserAgent()
        {
#if ANDROID
            try
            {
                int sdk = (int)global::Android.OS.Build.VERSION.SdkInt;
                string brand = global::Android.OS.Build.Brand;
                string model = global::Android.OS.Build.Model;
                string build = global::Android.OS.Build.Id;

                string device = (brand + " " + model).Trim();
                if (string.IsNullOrWhiteSpace(device))
                {
                    device = "Android";
                }

                return "Mozilla/5.0 (Linux; Android " + sdk + "; " + device + " Build/" + build + ") " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";
            }
            catch
            {
            }
#endif
            return string.Empty;
        }

        private string HomeUrl()
        {
            string home = (Preferences.Default.Get(HomeUrlKey, string.Empty) ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(home) ? DefaultHome : home;
        }

        // --- Onion (.onion) ---

        private static bool IsOnionAddress(string input)
        {
            string lower = input.ToLowerInvariant();
            return lower.Contains(".onion");
        }

        private async void HandleOnion(string input)
        {
#if ANDROID
            bool installed = AURA.Mobile.Platforms.Android.VpnHelper.IsOrbotInstalled();
#else
            bool installed = false;
#endif

            string message = installed
                ? "Endereço .onion exige Tor ativo.\n\nAbra o Orbot e ative o modo VPN (a conexão de todo o aparelho passa pelo Tor — então o WebView consegue acessar .onion). Depois toque em 'Abrir Orbot' e tente de novo."
                : "Endereço .onion exige Tor, que não vem no Android.\n\nInstale o Orbot (Tor oficial): ele oferece modo VPN que roteia o app inteiro pela rede Tor, permitindo abrir .onion no navegador. O WebView da AURA não pode se conectar a .onion sem ele.";

            string action = installed ? "Abrir Orbot" : "Instalar Orbot";

            bool? answer = await DisplayAlertAsync("Tor (.onion)", message, action, "Cancelar");
            if (answer == true)
            {
#if ANDROID
                if (installed)
                {
                    AURA.Mobile.Platforms.Android.VpnHelper.OpenOrbot();
                }
                else
                {
                    LoadInActive(AURA.Mobile.Platforms.Android.VpnHelper.OrbotPlayStoreUrl);
                }
#endif
            }
        }

        // --- Pesquisa / URL ---

        private SearchEngine CurrentEngine =>
            _engines[Math.Clamp(Preferences.Default.Get(EnginePrefKey, 0), 0, _engines.Count - 1)];

        private string NormalizeUrl(string input)
        {
            input = input.Trim();

            bool hasScheme = input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            bool looksLikeQuery = input.Contains(' ')
                || (!input.Contains('.') && !hasScheme);

            if (looksLikeQuery)
            {
                return string.Format(CurrentEngine.SearchUrl, Uri.EscapeDataString(input));
            }

            return hasScheme ? input : "https://" + input;
        }
    }
}