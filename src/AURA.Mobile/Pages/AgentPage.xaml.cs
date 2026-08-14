using AURA.AI;
using AURA.Memory;
using AURA.Mobile.Diagnostics;
using AURA.Mobile.Speech;
using AURA.Modules.Executors;

namespace AURA.Mobile.Pages;

public partial class AgentPage : ContentPage
{
    private readonly OpenRouterClient _client;
    private readonly MemoryStore _memory;
    private readonly ISpeechService _speech;
    private readonly VoiceAssistantService? _voice;
    private readonly ShellExecutor _shellExecutor;
    private AgentSession? _session;

    public AgentPage(OpenRouterClient client, MemoryStore memory, ISpeechService speech,
        ShellExecutor shellExecutor, VoiceAssistantService? voice = null)
    {
        InitializeComponent();
        _client = client;
        _memory = memory;
        _speech = speech;
        _shellExecutor = shellExecutor;
        _voice = voice;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RuntimeConfig.Apply(_client);
        AiConfig.Load(_client);

        string workspace = AgentWorkspace.EnsureCreated();
        string activeRoot = AgentWorkspace.ActiveRoot;
        WorkspaceLabel.Text = ProjectAccessService.StatusText + "\n" +
            "Workspace: " + activeRoot +
            $" ({AgentWorkspace.CountFiles(activeRoot)} arquivo(s))";
        ModelLabel.Text = $"Modelo: {_client.Options.Model} · {_client.Options.BaseUrl}";

        EnsureSession();
    }

    private void EnsureSession()
    {
        if (_session != null)
        {
            return;
        }

        string root = AgentWorkspace.ActiveRoot;
        var registry = new ToolRegistry();
        registry.Register(new ListDirTool(root));
        registry.Register(new ReadFileTool(root));
        registry.Register(new WriteFileTool(root));
        registry.Register(new EditFileTool(root));
        registry.Register(new ShellAgentTool(root, _shellExecutor));

        string systemPrompt =
            "Você é o agente de arquivos da AURA, um assistente que trabalha " +
            "dentro do workspace local da AURA. Quando houver um projeto vinculado, " +
            "esse workspace é uma cópia de trabalho sincronizada com a pasta escolhida. " +
            "Você PODE listar, ler, criar, editar e sobrescrever arquivos do " +
            "workspace e executar comandos shell (sh -c) nesse diretório. " +
            "Prefira ferramentas a respostas vagas: quando o usuário pedir uma " +
            "tarefa, use as ferramentas e confirme o que foi feito. " +
            "Responda em português, de forma curta e objetiva. " +
            "Caminhos são sempre relativos ao workspace.";

        _session = new AgentSession(_client, registry, systemPrompt, memory: _memory);
        _session.Step += OnAgentStep;

        AppendBubble(
            "Pronto. Posso listar, ler, criar e editar arquivos do workspace e " +
            "rodar comandos shell. O que deseja fazer?", user: false);
    }

    private async void OnLinkProjectClicked(object sender, EventArgs e)
    {
        ProjectButton.IsEnabled = false;
        try
        {
            bool linked = await ProjectAccessService.LinkAsync();
            if (!linked)
                return;

            // As ferramentas guardam a raiz no momento da criação da sessão.
            // Ao trocar o projeto, recriamos a sessão para apontar para a nova raiz.
            _session = null;
            WorkspaceLabel.Text = ProjectAccessService.StatusText + "\n" +
                "Workspace: " + AgentWorkspace.ActiveRoot +
                $" ({AgentWorkspace.CountFiles(AgentWorkspace.ActiveRoot)} arquivo(s))";

            EnsureSession();
            AppendBubble(
                "Projeto vinculado. A AURA trabalha na cópia local e sincroniza " +
                "as alterações de volta ao projeto após cada tarefa.", user: false);
        }
        catch (OperationCanceledException)
        {
            AppendBubble("Seleção de projeto cancelada.", user: false);
        }
        catch (Exception ex)
        {
            AppendBubble("Erro ao vincular projeto: " + ex.Message, user: false, isError: true);
            AuraLog.Exception("AgentPage.OnLinkProjectClicked", ex);
        }
        finally
        {
            ProjectButton.IsEnabled = true;
        }
    }

    private async void OnRunClicked(object sender, EventArgs e)
    {
        string text = CommandEditor.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        EnsureSession();
        AppendBubble(text, user: true);
        CommandEditor.Text = string.Empty;

        RunButton.IsEnabled = false;
        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;

        try
        {
            string answer = await _session!.RunAsync(text);
            AppendBubble(answer, user: false);
            _voice?.SetLastUtterance(answer);
            await SpeakAsync(answer);

            if (ProjectAccessService.IsLinked)
            {
                int synced = await ProjectAccessService.SyncBackAsync();
                AppendBubble($"↥ Projeto sincronizado: {synced} arquivo(s) atualizado(s).",
                    user: false, isTool: true);
            }
        }
        catch (Exception ex)
        {
            AppendBubble("Erro: " + ex.Message, user: false, isError: true);
            AuraLog.Exception("AgentPage.OnRunClicked", ex);
        }
        finally
        {
            RunButton.IsEnabled = true;
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }

    private void OnAgentStep(AURA.AI.AgentStep step)
    {
        string argsPreview = Shorten(step.Arguments, 70);
        string resultPreview = Shorten(step.Result, 140);
        AppendBubble("◆ " + step.ToolName + " " + argsPreview + "\n" + resultPreview,
            user: false, isTool: true);
    }

    private void OnToggleConfigClicked(object sender, EventArgs e)
    {
        ConfigPanel.IsVisible = !ConfigPanel.IsVisible;
        if (ConfigPanel.IsVisible)
        {
            AiConfig.Load(_client);
        }
    }

    private async void OnSpeakTestClicked(object sender, EventArgs e)
    {
        SpeakTestButton.IsEnabled = false;
        try
        {
            // Fala a última resposta do agente (assistente de verdade), não
            // uma frase fixa de recepção. Sem resposta ainda, usa saudação.
            string text = _voice?.LastUtterance;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "Estou aqui. Me instrua na conversa e eu respondo por voz.";
            }

            _voice?.SetLastUtterance(text);
            await SpeakAsync(text);
        }
        catch (Exception ex)
        {
            AppendBubble("Erro no TTS: " + ex.Message, user: false, isError: true);
            AuraLog.Exception("AgentPage.OnSpeakTestClicked", ex);
        }
        finally
        {
            SpeakTestButton.IsEnabled = true;
        }
    }

    private async Task SpeakAsync(string text)
    {
        try
        {
            await _speech.InitializeAsync();
            await _speech.SpeakAsync(text);
        }
        catch (NotSupportedException)
        {
            // Motor atual não cobre este texto (ex.: Kokoro com dicionário
            // limitado como fallback). Não quebra o chat.
            AuraLog.Info("TTS: texto fora do alcance do motor atual, fala pulada.");
        }
    }

    private void AppendBubble(string text, bool user, bool isTool = false, bool isError = false)
    {
        // Cores alinhadas à nova paleta de App.xaml
        Color background = user
            ? Color.FromArgb("#1e2d54")   // AuraUserBubble
            : isError
                ? Color.FromArgb("#2a0f12")
                : isTool
                    ? Color.FromArgb("#0f1420")   // AuraToolBubble
                    : Color.FromArgb("#13131d");  // AuraAgentBubble

        Color stroke = user
            ? Color.FromArgb("#2a3a6a")   // AuraBorderAccent
            : isError
                ? Color.FromArgb("#5a1f24")
                : Color.FromArgb("#242438");  // AuraBorder

        LayoutOptions alignment = user ? LayoutOptions.End : LayoutOptions.Start;

        Color textColor = isError
            ? Color.FromArgb("#e05560")
            : isTool
                ? Color.FromArgb("#7a7a90")   // AuraTextSecondary
                : Color.FromArgb("#e8e8f0");  // AuraTextPrimary

        string display = isTool ? text : text;

        var label = new Editor
        {
            Text = display,
            IsReadOnly = true,
            TextColor = textColor,
            FontSize = isTool ? 12 : 14,
            BackgroundColor = Colors.Transparent,
            AutoSize = Microsoft.Maui.Controls.EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 24,
            Margin = new Thickness(-4, -6)
        };

        View bubbleContent = label;
        if (!user)
        {
            var copyButton = new Button
            {
                Text = "Copiar",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#7a7a90"),
                FontSize = 10,
                Padding = new Thickness(6, 0),
                HeightRequest = 24,
                HorizontalOptions = LayoutOptions.End
            };
            copyButton.Clicked += async (_, _) =>
            {
                await Clipboard.Default.SetTextAsync(display);
                string original = copyButton.Text;
                copyButton.Text = "\u2713";
                await Task.Delay(1500);
                copyButton.Text = original;
            };
            bubbleContent = new VerticalStackLayout { label, copyButton };
        }

        var border = new Border
        {
            BackgroundColor = background,
            Stroke = stroke,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 8),
            MaximumWidthRequest = 340,
            HorizontalOptions = alignment,
            Content = bubbleContent
        };

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConversationContainer.Add(border);
            Dispatcher.Dispatch(() =>
                ConversationScroll.ScrollToAsync(0, ConversationContainer.Height, true));
        });
    }

    private static string Shorten(string text, int max)
    {
        text ??= string.Empty;
        string oneLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (oneLine.Length <= max)
        {
            return oneLine;
        }

        return oneLine.Substring(0, max) + "\u2026";
    }
}
