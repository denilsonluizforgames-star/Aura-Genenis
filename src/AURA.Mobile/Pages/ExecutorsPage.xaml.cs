using AURA.Abstractions.Execution;
using AURA.Core.Events;
using AURA.Modules.Executors;

namespace AURA.Mobile.Pages;

public partial class ExecutorsPage : ContentPage
{
    private readonly ShellExecutor _shell;
    private readonly GitExecutor _git;
    private readonly PythonExecutor _python;
    private readonly NodeExecutor _node;
    private readonly EventBus _events;

    public ExecutorsPage(ShellExecutor shell, GitExecutor git, PythonExecutor python, NodeExecutor node, EventBus events)
    {
        InitializeComponent();
        _shell = shell;
        _git = git;
        _python = python;
        _node = node;
        _events = events;
        ExecutorPicker.ItemsSource = new[] { "Shell", "Git", "Python", "Node" };
        ExecutorPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async void OnCopyClicked(object sender, EventArgs e)
    {
        string text = ResultEditor.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text == "Resultado aparecerá aqui.")
        {
            return;
        }

        await Clipboard.Default.SetTextAsync(text);
        string original = CopyButton.Text;
        CopyButton.Text = "✓ Copiado";
        await Task.Delay(1500);
        CopyButton.Text = original;
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async void OnExecuteClicked(object sender, EventArgs e)
    {
        string selected = ExecutorPicker.SelectedItem as string;
        ProcessExecutorBase executor = selected switch
        {
            "Shell" => _shell,
            "Git" => _git,
            "Python" => _python,
            "Node" => _node,
            _ => null
        };

        if (executor == null)
        {
            ResultEditor.Text = "Selecione um executor.";
            return;
        }

        if (!executor.IsAvailable())
        {
            ResultEditor.Text = "Ferramenta '" + executor.Name + "' não disponível neste dispositivo.";
            return;
        }

        string command = CommandEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            ResultEditor.Text = "Informe um comando (ex.: git → status; python → script.py; shell → ls).";
            return;
        }

        string[] argParts = (ArgsEntry.Text ?? string.Empty)
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        var request = new ExecutionRequest
        {
            Command = command,
            Arguments = new List<string>(argParts),
            Timeout = TimeSpan.FromSeconds(60)
        };

        ExecButton.IsEnabled = false;
        ResultEditor.Text = "Executando...";
        CommandEntry.Text = string.Empty;
        ArgsEntry.Text = string.Empty;
        try
        {
            ExecutionResult result = await executor.ExecuteAsync(request);
            string status = (result.Success ? "OK" : "FALHOU") +
                " (exit " + result.ExitCode + ", " +
                result.Duration.TotalSeconds.ToString("0.0") + "s)";
            string body = string.IsNullOrWhiteSpace(result.CombineOutput())
                ? "(sem saída)"
                : result.CombineOutput();
            ResultEditor.Text = status + "\n" + new string('-', 32) + "\n" + body;

            _events.Publish(new ExecutorCompletedEvent
            {
                Executor = executor.Name,
                Command = command,
                Success = result.Success,
                Duration = result.Duration
            });
        }
        catch (Exception ex)
        {
            ResultEditor.Text = "Erro ao executar: " + ex.Message;
            AuraLog.Exception("ExecutorsPage.Execute", ex);
        }
        finally
        {
            ExecButton.IsEnabled = true;
        }
    }

    private async Task RefreshAsync()
    {
        var statuses = new[]
        {
            MakeStatus(_shell),
            MakeStatus(_git),
            MakeStatus(_python),
            MakeStatus(_node)
        };

        ExecutorsView.ItemsSource = statuses;
        await Task.CompletedTask;
    }

    private static ExecutorStatus MakeStatus(ProcessExecutorBase executor)
    {
        bool available = executor.IsAvailable();
        return new ExecutorStatus
        {
            Name = executor.Name,
            Status = available ? "Disponível neste dispositivo" : "Não disponível no Android",
            StatusColor = available
                ? Color.FromArgb("#4caf6f")
                : Color.FromArgb("#e2555c")
        };
    }
}

public class ExecutorStatus
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public Color StatusColor { get; set; } = Colors.Gray;
}
