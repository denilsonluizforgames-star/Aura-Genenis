using AURA.Abstractions.Execution;
using AURA.Modules.Executors;

namespace AURA.Mobile.Pages;

public partial class TerminalPage : ContentPage
{
    private readonly ShellExecutor _shell;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private string _currentDir;

    public TerminalPage(ShellExecutor shell)
    {
        InitializeComponent();
        _shell = shell;

        _currentDir = FileSystem.AppDataDirectory;
        UpdateDirLabel();

        AppendLine("AURA Terminal — comandos via /bin/sh (sandbox do app).");
        AppendLine("Comandos internos: clear, cd <dir>, pwd, help.", dim: true);
        AppendLine();
    }

    private async void OnCommandCompleted(object sender, EventArgs e)
    {
        await RunCommandAsync(CommandEntry.Text);
    }

    private void OnHistoryUpClicked(object sender, EventArgs e)
    {
        if (_history.Count == 0)
        {
            return;
        }

        _historyIndex = Math.Max(0, _historyIndex - 1);
        CommandEntry.Text = _history[_historyIndex];
        CommandEntry.CursorPosition = CommandEntry.Text.Length;
    }

    private void OnHistoryDownClicked(object sender, EventArgs e)
    {
        if (_history.Count == 0)
        {
            return;
        }

        _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
        CommandEntry.Text = _historyIndex >= _history.Count ? string.Empty : _history[_historyIndex];
        CommandEntry.CursorPosition = CommandEntry.Text.Length;
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        OutputStack.Children.Clear();
    }

    private async void OnCopyClicked(object sender, EventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var child in OutputStack.Children)
        {
            if (child is Label label && !string.IsNullOrEmpty(label.Text))
            {
                sb.AppendLine(label.Text);
            }
        }

        string output = sb.ToString();
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        await Clipboard.Default.SetTextAsync(output.TrimEnd());
        var button = sender as Button;
        if (button == null)
        {
            return;
        }

        string original = button.Text;
        button.Text = "✓";
        await Task.Delay(1500);
        button.Text = original;
    }

    private async Task RunCommandAsync(string? input)
    {
        string command = input?.Trim() ?? string.Empty;
        if (command.Length == 0)
        {
            return;
        }

        _history.Add(command);
        _historyIndex = _history.Count;
        CommandEntry.Text = string.Empty;

        AppendLine("$ " + command, prompt: true);

        string lower = command.ToLowerInvariant();

        if (lower == "clear" || lower == "cls")
        {
            OutputStack.Children.Clear();
            return;
        }

        if (lower == "help")
        {
            AppendLine("Comandos internos: clear, cd <dir>, pwd, help.");
            AppendLine("Qualquer outro comando roda via sh -c no diretório atual.", dim: true);
            return;
        }

        if (lower == "pwd")
        {
            AppendLine(_currentDir);
            return;
        }

        if (lower.StartsWith("cd "))
        {
            string target = command.Substring(3).Trim();
            if (target.Length > 0)
            {
                string newDir = Path.Combine(_currentDir, target);
                if (Directory.Exists(newDir))
                {
                    _currentDir = Path.GetFullPath(newDir);
                    UpdateDirLabel();
                }
                else
                {
                    AppendLine("cd: diretório não existe: " + target, error: true);
                }
            }

            return;
        }

        try
        {
            var request = new ExecutionRequest
            {
                Command = command,
                WorkingDirectory = _currentDir,
                Timeout = TimeSpan.FromMinutes(5)
            };

            ExecutionResult result = await _shell.ExecuteAsync(request);

            string output = result.CombineOutput();
            if (string.IsNullOrWhiteSpace(output))
            {
                AppendLine("(sem saída)");
            }
            else
            {
                AppendLine(output, error: !result.Success);
            }

            if (!result.Success)
            {
                AppendLine($"exit {result.ExitCode} em {result.Duration.TotalSeconds:0.0}s", dim: true);
            }
        }
        catch (Exception ex)
        {
            AppendLine("Erro: " + ex.Message, error: true);
            AuraLog.Exception("TerminalPage.RunCommand", ex);
        }
    }

    private void UpdateDirLabel()
    {
        DirLabel.Text = _currentDir;
    }

    private void AppendLine(string text = "", bool prompt = false, bool dim = false, bool error = false)
    {
        Color color = prompt
            ? Color.FromArgb("#4caf6f")
            : error
                ? Color.FromArgb("#e2555c")
                : dim
                    ? Color.FromArgb("#9a9aa5")
                    : Color.FromArgb("#f2f2f5");

        var label = new Label
        {
            Text = text,
            TextColor = color,
            FontFamily = "monospace",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };

        MainThread.BeginInvokeOnMainThread(() =>
        {
            OutputStack.Add(label);

            Dispatcher.Dispatch(async () =>
            {
                await OutputScroll.ScrollToAsync(0, OutputStack.Height, animated: true);
            });
        });
    }
}
