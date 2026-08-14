using System.Net;
using System.Net.Http;
using AURA.AI;
using AURA.Mobile.Diagnostics;

namespace AURA.Mobile.Pages;

public partial class LogsPage : ContentPage
{
    private readonly OpenRouterClient _client;

    public LogsPage(OpenRouterClient client)
    {
        InitializeComponent();
        _client = client;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RuntimeConfig.Apply(_client);
        LoadLog();
    }

    private void OnRefreshClicked(object sender, EventArgs e)
    {
        LoadLog();
    }

    private void LoadLog()
    {
        try
        {
            LogViewer.Text = AuraLog.ReadRecentLog(400);
        }
        catch (Exception ex)
        {
            LogViewer.Text = "Erro ao carregar o log: " + ex.Message;
        }
    }

    private async void OnCopyClicked(object sender, EventArgs e)
    {
        string content = AuraLog.ReadRecentLog(2000);
        if (string.IsNullOrWhiteSpace(content))
        {
            LogViewer.Text = "(log vazio)";
            return;
        }

        await Clipboard.Default.SetTextAsync(content);
        LogViewer.Text = "Log copiado para a área de transferência.\n\n" + content;
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        string content = AuraLog.ReadRecentLog(2000);
        if (string.IsNullOrWhiteSpace(content))
        {
            LogViewer.Text = "(log vazio)";
            return;
        }

        string filePath = Path.Combine(FileSystem.CacheDirectory, "aura_log.txt");
        await File.WriteAllTextAsync(filePath, content);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Log AURA",
            File = new ShareFile(filePath, "text/plain")
        });
    }

    private async void OnTestClicked(object sender, EventArgs e)
    {
        RuntimeConfig.Apply(_client);
        TestButton.IsEnabled = false;
        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;
        LogViewer.Text = "Testando conexão...\n";

        var sb = new System.Text.StringBuilder();
        try
        {
            var hasKey = !string.IsNullOrWhiteSpace(_client.Options.ApiKey);
            sb.AppendLine($"Chave OpenRouter: {(hasKey ? "configurada (" + _client.Options.ApiKey.Length + " chars)" : "AUSENTE — defina na aba Assistente")}");
            sb.AppendLine($"Modelo: {_client.Options.Model}");
            sb.AppendLine($"URL: {_client.Options.BaseUrl}");
            sb.AppendLine();

            if (!hasKey)
            {
                sb.AppendLine("RESULTADO: falha — nenhuma chave de API configurada.");
                LogViewer.Text = sb.ToString();
                return;
            }

            // 1. Conexão de rede local.
            var current = Connectivity.Current.NetworkAccess;
            sb.AppendLine($"1) Acesso à rede: {current}");

            // 2. DNS/HTTPS até a base da OpenRouter.
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All
            };
            using var http = new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(Math.Max(30, _client.Options.TimeoutSeconds));

            var baseUri = new Uri(_client.Options.BaseUrl);
            sb.AppendLine($"2) Conectando a {baseUri.Host} (TLS)...");
            using (var probe = new HttpRequestMessage(HttpMethod.Head, new Uri(baseUri.GetLeftPart(UriPartial.Authority))))
            {
                using HttpResponseMessage ping = await http.SendAsync(probe);
                sb.AppendLine($"   Resposta: HTTP {(int)ping.StatusCode} {ping.StatusCode}");
            }

            // 3. Chamada real de chat (1-token) para verificar credenciais + modelo.
            sb.AppendLine("3) Chamada de teste ao LLM...");
            string modelEcho = await _client.ChatAsync(
                "Responda apenas: OK",
                http,
                systemPrompt: "Você responde apenas OK.");
            sb.AppendLine($"   Resposta do modelo: \"{modelEcho}\"");
            sb.AppendLine();
            sb.AppendLine("RESULTADO: CONEXÃO OK — a IA respondeu.");
            AuraLog.Info("Teste de conexão AURA: OK");
        }
        catch (HttpRequestException hex)
        {
            sb.AppendLine();
            sb.AppendLine("RESULTADO: FALHA de HTTP.");
            sb.AppendLine("Erro: " + hex.Message);
            AuraLog.Exception("LogsPage.OnTestClicked (Http)", hex);
        }
        catch (TaskCanceledException)
        {
            sb.AppendLine();
            sb.AppendLine("RESULTADO: FALHA — tempo esgotado (60s).");
            sb.AppendLine("Dica: verifique se o Wi-Fi/dados está ativo e se o aparelho tem acesso à internet.");
        }
        catch (Exception ex)
        {
            sb.AppendLine();
            sb.AppendLine("RESULTADO: FALHA inesperada.");
            sb.AppendLine("Erro: " + ex);
            AuraLog.Exception("LogsPage.OnTestClicked", ex);
        }

        LogViewer.Text = sb.ToString();
        TestButton.IsEnabled = true;
        BusyIndicator.IsRunning = false;
        BusyIndicator.IsVisible = false;
    }

    private async void OnAnalyzeClicked(object sender, EventArgs e)
    {
        RuntimeConfig.Apply(_client);
        string apiKey = _client.Options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogViewer.Text = "Configure a chave OpenRouter na aba Assistente primeiro.";
            return;
        }

        string logContent = AuraLog.ReadRecentLog(RuntimeConfig.LogLinesForAnalysis);
        if (string.IsNullOrWhiteSpace(logContent))
        {
            LogViewer.Text = "Log vazio — não há o que analisar.";
            return;
        }

        AnalyzeButton.IsEnabled = false;
        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;
        LogViewer.Text = "Enviando log para análise da IA...\n\n" + logContent;

        string systemPrompt =
            "Você é o engenheiro de diagnóstico do app AURA (assistente de IA para Android, " +
            "feito em .NET MAUI). Receba o log de execução do app e: " +
            "1) identifique a causa raiz de qualquer exceção/erro; " +
            "2) explique em português de forma clara e curta; " +
            "3) sugira a correção exata (arquivo, linha e trecho de código quando possível). " +
            "Se não houver erro, apenas resuma o que o log mostra. Responda de forma objetiva.";

        try
        {
            string analysis = await _client.ChatAsync(logContent, systemPrompt: systemPrompt);
            LogViewer.Text = "=== ANÁLISE DA IA ===\n\n" + analysis;
            AuraLog.Info("Análise IA concluída.");
        }
        catch (Exception ex)
        {
            LogViewer.Text = "Falha na análise: " + ex.Message +
                "\n\nUse 'Testar conexão' para ver detalhes.";
            AuraLog.Exception("LogsPage.OnAnalyzeClicked", ex);
        }
        finally
        {
            AnalyzeButton.IsEnabled = true;
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }
}
