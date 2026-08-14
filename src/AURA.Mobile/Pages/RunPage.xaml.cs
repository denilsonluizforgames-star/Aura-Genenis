using AURA.Core.Launchers;
using AURA.Core.Runtime;
using Cell = AURA.Core.Runtime.Cell;

namespace AURA.Mobile.Pages;

public partial class RunPage : ContentPage
{
    private readonly SimulationRuntime _runtime;
    private readonly Runner _runner;
    private string? _filePath;

    public RunPage(SimulationRuntime runtime, Runner runner)
    {
        InitializeComponent();
        _runtime = runtime;
        _runner = runner;
    }

    private async void OnCopyClicked(object sender, EventArgs e)
    {
        string text = ResultLabel.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await Clipboard.Default.SetTextAsync(text);
        string original = CopyButton.Text;
        CopyButton.Text = "✓ Copiado";
        await Task.Delay(1500);
        CopyButton.Text = original;
    }

    private async void OnPickClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Escolha um programa para rodar em célula"
            });

            if (result == null)
            {
                return;
            }

            _filePath = result.FullPath;
            FileLabel.Text = "Arquivo: " + _filePath;
            UpdateLauncherInfo();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
            AuraLog.Exception("RunPage.Pick", ex);
        }
    }

    private void UpdateLauncherInfo()
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            LauncherLabel.Text = string.Empty;
            return;
        }

        var launcher = _runner.ResolveLauncher(_filePath);
        if (launcher == null)
        {
            string supported = string.Join(", ",
                _runner.Launchers.SelectMany(l => l.SupportedExtensions));
            LauncherLabel.Text = "Sem launcher para esta extensão. Suportados: " + supported;
        }
        else
        {
            LauncherLabel.Text = "Launcher: " + launcher.GetType().Name;
        }
    }

    private async void OnRunClicked(object sender, EventArgs e)
    {
        string exe = ExeEntry.Text?.Trim() ?? string.Empty;
        string id = CellIdEntry.Text?.Trim() ?? string.Empty;
        string args = ArgsEntry.Text?.Trim() ?? string.Empty;

        var limits = new ResourceLimits();
        if (long.TryParse(MemEntry.Text, out long mb) && mb > 0)
        {
            limits.MemoryLimitMb = mb;
        }

        if (string.IsNullOrWhiteSpace(exe) && string.IsNullOrWhiteSpace(_filePath))
        {
            ResultLabel.Text = "Escolha um arquivo ou informe um executável.";
            return;
        }

        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;

        try
        {
            Cell cell;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = Path.GetFileNameWithoutExtension(exe) + "-" +
                        Guid.NewGuid().ToString("N").Substring(0, 6);
                }

                cell = _runtime.CreateCell(id, exe, args,
                    workingDirectory: FileSystem.AppDataDirectory,
                    limits: limits.IsEmpty ? null : limits);
                await _runtime.StartCellAsync(cell.Id);
            }
            else
            {
                cell = await _runner.RunAsync(_runtime, id, _filePath!, args,
                    limits: limits.IsEmpty ? null : limits);
            }

            ResultLabel.Text =
                $"Célula '{cell.Id}' criada e iniciada (pid {cell.ProcessId}). " +
                "Gerencie na aba Células.";
            AuraLog.Info("RunPage: célula iniciada " + cell.Id + " (" + (cell.AppPath ?? "") + ")");
        }
        catch (Exception ex)
        {
            ResultLabel.Text = "Erro: " + ex.Message;
            AuraLog.Exception("RunPage.Run", ex);
        }
        finally
        {
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }
}
