using AURA.Agents;
using AURA.Network;
using AURA.SystemInfo;

namespace AURA.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly SystemAnalyzer _systemAnalyzer;
    private readonly NetworkManager _networkManager;
    private readonly AgentManager _agentManager;
    private bool _pulseRunning;

    public HomePage(SystemAnalyzer systemAnalyzer, NetworkManager networkManager, AgentManager agentManager)
    {
        InitializeComponent();
        _systemAnalyzer = systemAnalyzer;
        _networkManager = networkManager;
        _agentManager = agentManager;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
        StartOrbPulse();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopOrbPulse();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await RefreshAsync();
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

    // ── Bottom bar (conceito holográfico) ──────────────────────────

    private async void OnNetworkClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnNetwork);
        await RefreshNetworkOnlyAsync();
        await DisplayAlert("Network", "Status de rede atualizado.", "OK");
    }

    private async void OnSensorClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnSensor);
        await RefreshSystemOnlyAsync();
        await DisplayAlert("Sensor", "Diagnóstico de sistema atualizado.", "OK");
    }

    private async void OnEthereumClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnEthereum);
        await DisplayAlert("Ethereum", "Módulo reservado para integração futura.", "OK");
    }

    private async void OnSystemClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnSystem);
        await RefreshAsync();
        await DisplayAlert("System", "Painel de sistema atualizado.", "OK");
    }

    private async void OnDeviceClicked(object? sender, EventArgs e)
    {
        await PlayButtonFeedbackAsync(BtnDevice);

        // Navega para a seção Apps (Células) se existir no TabbedPage pai.
        if (Parent is NavigationPage nav && nav.Parent is TabbedPage tabs)
        {
            foreach (var child in tabs.Children)
            {
                if (child is NavigationPage np && np.Title == "Apps")
                {
                    tabs.CurrentPage = child;
                    return;
                }
            }
        }

        await DisplayAlert("Device", "Abra a seção Apps → Células para gerenciar o dispositivo.", "OK");
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
