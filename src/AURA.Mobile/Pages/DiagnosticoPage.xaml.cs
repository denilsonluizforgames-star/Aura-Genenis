using AURA.Network;
using AURA.SystemInfo;

namespace AURA.Mobile.Pages;

public partial class DiagnosticoPage : ContentPage
{
    private readonly SystemAnalyzer _systemAnalyzer;
    private readonly NetworkManager _networkManager;

    public DiagnosticoPage(SystemAnalyzer systemAnalyzer, NetworkManager networkManager)
    {
        InitializeComponent();
        _systemAnalyzer = systemAnalyzer;
        _networkManager = networkManager;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var sys = await Task.Run(() => _systemAnalyzer.Analyze());
            var net = await Task.Run(() => _networkManager.CheckConnection());

            CpuValue.Text = sys.ProcessorCount > 0 ? "Ativo" : "—";
            CpuDetail.Text = sys.Architecture ?? "";

            RamValue.Text = sys.TotalMemoryGb > 0 ? $"{sys.AvailableMemoryGb:0.0} GB" : "—";
            RamDetail.Text = sys.TotalMemoryGb > 0
                ? $"{sys.TotalMemoryGb:0.0} GB total"
                : "";

            DiskValue.Text = sys.FreeDiskSpaceGb > 0 ? $"{sys.FreeDiskSpaceGb:0.0} GB" : "—";
            DiskDetail.Text = sys.TotalDiskSpaceGb > 0
                ? $"{sys.TotalDiskSpaceGb:0.0} GB total"
                : "";

            OsValue.Text = sys.OperatingSystem ?? "—";
            OsDetail.Text = sys.SystemDrive is not null ? $"Disco {sys.SystemDrive}" : "";

            CoresValue.Text = sys.ProcessorCount > 0 ? sys.ProcessorCount.ToString() : "—";
            CoresDetail.Text = sys.ProcessorCount == 1 ? "núcleo" : "núcleos";

            LatencyValue.Text = net.LatencyMilliseconds > 0
                ? $"{net.LatencyMilliseconds} ms"
                : net.HasInternetAccess ? "✓" : "—";
            LatencyDetail.Text = net.HasInternetAccess ? "conectado" : "offline";

            IpValue.Text = !string.IsNullOrWhiteSpace(net.LocalIpAddress)
                ? net.LocalIpAddress
                : "—";
            IpDetail.Text = net.HasInternetAccess ? "roteável" : "local apenas";

            VersionValue.Text = AURA.Core.VersionInfo.FullName ?? "—";
            VersionDetail.Text = "AURA Mobile";

            var online = net.HasInternetAccess;
            ConnectionIcon.Text = online ? "🌐" : "⚠️";
            ConnectionLabel.Text = online
                ? "Dispositivo conectado"
                : "Sem conexão com a internet";
            ConnectionCard.Stroke = online
                ? (Color)Application.Current!.Resources["AuraBorderAccent"]
                : (Color)Application.Current!.Resources["AuraBorder"];
        }
        catch (Exception ex)
        {
            ConnectionLabel.Text = "Erro ao coletar diagnóstico: " + ex.Message;
        }
    }
}