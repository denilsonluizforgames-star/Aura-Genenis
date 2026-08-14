using AURA.Agents;
using AURA.Network;
using AURA.SystemInfo;
using CommunityToolkit.Maui.Views;

namespace AURA.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly SystemAnalyzer _systemAnalyzer;
    private readonly NetworkManager _networkManager;
    private readonly AgentManager _agentManager;
    private bool _pulseRunning;

    private const string VideoBgPrefKey = "aura_video_bg";

    public HomePage(SystemAnalyzer systemAnalyzer, NetworkManager networkManager, AgentManager agentManager)
    {
        InitializeComponent();
        _systemAnalyzer = systemAnalyzer;
        _networkManager = networkManager;
        _agentManager = agentManager;
        App.ThemeChanged += OnThemeChanged;
        UpdateThemeIcon();

        // Long-press no botão de tema alterna vídeo de fundo on/off (Preference).
        var longPress = new TapGestureRecognizer
        {
            NumberOfTapsRequired = 1
        };
        // Usamos GestureRecognizer de long-press via pointer se disponível; fallback = double-tap no botão.
        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += OnThemeDoubleTapped;
        BtnTheme.GestureRecognizers.Add(doubleTap);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateThemeIcon();
        await RefreshAsync();
        StartOrbPulse();
        ApplyVideoBackground();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopOrbPulse();
        PauseVideoBackground();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    // ── Tema Solar / Lunar ─────────────────────────────────────────

    private void OnThemeToggleClicked(object? sender, EventArgs e)
    {
        App.ToggleTheme();
        // Icon is refreshed via ThemeChanged subscription.
    }

    private void OnThemeChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateThemeIcon();
            ApplyVideoBackground(); // troca source se vídeo estiver ativo
        });
    }

    private void UpdateThemeIcon()
    {
        if (BtnTheme is null) return;
        // Solar → mostra lua (próximo estado = Lunar); Lunar → mostra sol
        BtnTheme.Text = App.IsSolar ? "☾" : "☀";
    }

    // ── Vídeo de fundo opcional (C1) ───────────────────────────────

    private bool IsVideoBgEnabled => Preferences.Default.Get(VideoBgPrefKey, false);

    private void OnThemeDoubleTapped(object? sender, TappedEventArgs e)
    {
        bool next = !IsVideoBgEnabled;
        Preferences.Default.Set(VideoBgPrefKey, next);
        AuraLog.Info($"Vídeo de fundo {(next ? "ativado" : "desativado")} (Preference {VideoBgPrefKey})");
        ApplyVideoBackground();
        // Feedback visual rápido
        _ = PlayButtonFeedbackAsync(BtnTheme);
    }

    private void ApplyVideoBackground()
    {
        if (BgVideo is null) return;

        if (!IsVideoBgEnabled)
        {
            PauseVideoBackground();
            BgVideo.IsVisible = false;
            return;
        }

        try
        {
            // Assets esperados em Resources/Raw/ (MauiAsset LogicalName = filename)
            string resource = App.IsSolar ? "solar_bg.mp4" : "lunar_bg.mp4";
            BgVideo.Source = MediaSource.FromResource(resource);
            BgVideo.IsVisible = true;
            BgVideo.Play();
        }
        catch (Exception ex)
        {
            AuraLog.Exception("HomePage.ApplyVideoBackground", ex);
            BgVideo.IsVisible = false;
        }
    }

    private void PauseVideoBackground()
    {
        try
        {
            BgVideo?.Pause();
        }
        catch
        {
            // ignore
        }
    }

    // ── Pulse holográfico (F2 — MAUI nativo, zero NuGet) ───────────

    private void StartOrbPulse()
    {
        if (_pulseRunning || CoreOrb is null)
            return;

        _pulseRunning = true;
        _ = RunPulseLoopAsync();
    }

    private void StopOrbPulse()
    {
        _pulseRunning = false;
        CoreOrb?.AbortAnimation("OrbPulse");
        MiddleRing?.AbortAnimation("RingPulse");
        OuterRing?.AbortAnimation("OuterPulse");
    }

    private async Task RunPulseLoopAsync()
    {
        // Loop suave: escala 1.0 ↔ 1.08 + leve variação de opacidade no anel médio.
        while (_pulseRunning)
        {
            try
            {
                var scaleUp = CoreOrb.ScaleTo(1.08, 900, Easing.SinInOut);
                var fadeOut = MiddleRing.FadeTo(0.45, 900, Easing.SinInOut);
                await Task.WhenAll(scaleUp, fadeOut);
                if (!_pulseRunning) break;

                var scaleDown = CoreOrb.ScaleTo(1.0, 900, Easing.SinInOut);
                var fadeIn = MiddleRing.FadeTo(0.7, 900, Easing.SinInOut);
                await Task.WhenAll(scaleDown, fadeIn);
            }
            catch
            {
                // Página pode ter sido descarregada; encerra o loop.
                break;
            }
        }
    }

    private static async Task PlayButtonFeedbackAsync(View? button)
    {
        if (button is null) return;
        try
        {
            await button.ScaleTo(0.85, 80, Easing.CubicOut);
            await button.ScaleTo(1.0, 120, Easing.CubicIn);
        }
        catch
        {
            // ignore
        }
    }

    // ── Bottom bar alinhada à referência (Início | Diagnóstico | Módulos | Agentes | Config) ──

    private async void OnInicioClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnInicio);
        await RefreshAsync();
    }

    private async void OnDiagnosticoClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnDiagnostico);
        await NavigateToSectionAndPageAsync("Sistema", "Diagnóstico");
    }

    private async void OnModulosClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnModulos);
        await NavigateToSectionAndPageAsync("Ferramentas", "Módulos");
    }

    private async void OnAgentesClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnAgentes);
        await NavigateToSectionAndPageAsync("Assistente", "Agente");
    }

    private async void OnConfigClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnConfig);
        // Ainda não existe página Config dedicada. Leva à seção Sistema (Início/Diagnóstico/Logs).
        if (!TrySwitchToSection("Sistema"))
            await DisplayAlert("Config", "Seção Sistema não disponível no momento.", "OK");
    }

    /// <summary>
    /// Troca a aba do TabbedPage para a seção correspondente.
    /// </summary>
    private async Task NavigateToSectionAndPageAsync(string sectionTitle, string pageLabel)
    {
        if (!TrySwitchToSection(sectionTitle))
        {
            await DisplayAlert(pageLabel, $"Seção \"{sectionTitle}\" ainda não está ativa (módulo não aplicado).", "OK");
            return;
        }
    }

    private bool TrySwitchToSection(string sectionTitle)
    {
        if (Parent is not NavigationPage nav || nav.Parent is not TabbedPage tabs)
            return false;

        foreach (var child in tabs.Children)
        {
            if (child is NavigationPage np && string.Equals(np.Title, sectionTitle, StringComparison.OrdinalIgnoreCase))
            {
                tabs.CurrentPage = child;
                return true;
            }
        }
        return false;
    }

    // ── Refresh ────────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        try
        {
            VersionLabel.Text = AURA.Core.VersionInfo.FullName;

            await Task.WhenAll(RefreshSystemOnlyAsync(), RefreshNetworkOnlyAsync());

            var available = _agentManager.AvailableAssistants();
            AgentsLabel.Text = available.Count == 0
                ? "Nenhum agente CLI instalado no dispositivo. Use a aba Assistente."
                : string.Join("  •  ", available.Select(a => a.Name));
        }
        catch (Exception ex)
        {
            VersionLabel.Text = "Erro ao coletar diagnóstico: " + ex.Message;
        }
    }

    private async Task RefreshSystemOnlyAsync()
    {
        var diagnostics = await Task.Run(() => _systemAnalyzer.Analyze());
        OsLabel.Text = "SO: " + diagnostics.OperatingSystem;
        CpuLabel.Text = "Arquitetura: " + diagnostics.Architecture + "  |  Núcleos: " + diagnostics.ProcessorCount;
        RamLabel.Text = $"RAM: {diagnostics.TotalMemoryGb:0.0} GB total / {diagnostics.AvailableMemoryGb:0.0} GB livre";
        DiskLabel.Text = $"Disco {diagnostics.SystemDrive}: {diagnostics.FreeDiskSpaceGb:0.0}/{diagnostics.TotalDiskSpaceGb:0.0} GB";
    }

    private async Task RefreshNetworkOnlyAsync()
    {
        var network = await Task.Run(() => _networkManager.CheckConnection());
        NetLabel.Text = network.Message
            + (network.HasInternetAccess ? $"  (latência {network.LatencyMilliseconds} ms)" : "");
        IpLabel.Text = "IP local: " + network.LocalIpAddress;
    }
}
