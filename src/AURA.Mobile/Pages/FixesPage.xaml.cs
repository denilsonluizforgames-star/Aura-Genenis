using AURA.AI;
using AURA.Mobile.Diagnostics;

namespace AURA.Mobile.Pages;

public partial class FixesPage : ContentPage
{
    private readonly OpenRouterClient _client;
    private List<FixProposal> _pending = new();

    public FixesPage(OpenRouterClient client)
    {
        InitializeComponent();
        _client = client;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RuntimeConfig.Apply(_client);
        ShowCurrentConfig();
    }

    private void ShowCurrentConfig()
    {
        string current =
            $"Configuração atual:\n" +
            $"Provedor: {RuntimeConfig.Provider} ({(RuntimeConfig.Provider.Length == 0 ? "padrão" : RuntimeConfig.Provider)})\n" +
            $"Modelo: {_client.Options.Model}\n" +
            $"max_tokens: {_client.Options.MaxTokens}\n" +
            $"timeout: {_client.Options.TimeoutSeconds}s\n" +
            $"linhas de log analisadas: {RuntimeConfig.LogLinesForAnalysis}\n" +
            $"chave: {(string.IsNullOrWhiteSpace(_client.Options.ApiKey) ? "ausente" : "configurada")}\n" +
            $"URL: {_client.Options.BaseUrl}";

        StatusLabel.Text = current;
    }

    private async void OnAnalyzeClicked(object sender, EventArgs e)
    {
        string apiKey = _client.Options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            StatusLabel.Text = "Configure a chave de API na aba Assistente primeiro.";
            return;
        }

        AnalyzeButton.IsEnabled = false;
        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;
        StatusLabel.Text = "Analisando log e configuração...";

        string log = AuraLog.ReadRecentLog(RuntimeConfig.LogLinesForAnalysis);
        string systemPrompt =
            "Você é o engenheiro de manutenção do app AURA (.NET MAUI para Android). " +
            "Receba o log de execução e a configuração atual do app. Identifique problemas " +
            "e proponha correções que possam ser aplicadas em tempo de execução " +
            "(sem recompilar o APK). Responda EXCLUSIVAMENTE com um JSON válido " +
            "no formato: {\"fixes\":[{\"key\":\"...\",\"label\":\"...\",\"current\":\"...\"," +
            "\"suggested\":\"...\",\"reason\":\"...\"}]}. " +
            "Keys aceitas: model, provider, max_tokens, timeout_seconds, log_lines, api_key. " +
            "Use model/provider exatamente como estão no catálogo (ex.: qwen/qwen-plus, " +
            "openrouter/free, openai/gpt-oss-20b:free, google/gemma-4-26b-a4b-it:free, " +
            "llama-3.3-70b-versatile, gemini-2.5-flash). " +
            "Se não houver correção necessária, retorne {\"fixes\":[]}.";

        string question =
            "LOG:\n" + (string.IsNullOrWhiteSpace(log) ? "(vazio)" : log) +
            "\n\nCONFIGURAÇÃO ATUAL:\n" + ShowCurrentConfigRaw();

        try
        {
            string answer = await _client.ChatAsync(question, systemPrompt: systemPrompt);
            _pending = FixProposalParser.Parse(answer);

            if (_pending.Count == 0)
            {
                StatusLabel.Text =
                    "Nenhuma correção identificada pela IA.\n\n" +
                    "Resposta da IA:\n" + answer;
                FixesView.ItemsSource = null;
            }
            else
            {
                FixesView.ItemsSource = null;
                FixesView.ItemsSource = _pending;
                StatusLabel.Text =
                    $"{_pending.Count} correção(ões) proposta(s) pela IA. Marque as desejadas e toque em Aplicar.\n\n" +
                    "Configuração atual:\n" + ShowCurrentConfigRaw();
            }

            AuraLog.Info("Correções propostas: " + _pending.Count);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Falha na análise: " + ex.Message;
            AuraLog.Exception("FixesPage.OnAnalyzeClicked", ex);
        }
        finally
        {
            AnalyzeButton.IsEnabled = true;
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }

    private string ShowCurrentConfigRaw()
    {
        return
            $"Provedor: {RuntimeConfig.Provider}\n" +
            $"Modelo: {_client.Options.Model}\n" +
            $"max_tokens: {_client.Options.MaxTokens}\n" +
            $"timeout_seconds: {_client.Options.TimeoutSeconds}\n" +
            $"log_lines: {RuntimeConfig.LogLinesForAnalysis}\n" +
            $"api_key: {(string.IsNullOrWhiteSpace(_client.Options.ApiKey) ? "(vazio)" : "(configurada)")}";
    }

    private void OnApplyClicked(object sender, EventArgs e)
    {
        var selected = _pending.Where(p => p.Selected).ToList();
        if (selected.Count == 0)
        {
            StatusLabel.Text = "Nenhuma correção marcada para aplicar.";
            return;
        }

        int applied = 0;
        int ignoredKeyFixes = 0;
        foreach (FixProposal fix in selected)
        {
            try
            {
                switch (fix.Key)
                {
                    case "model":
                        RuntimeConfig.Model = fix.Suggested;
                        _client.Options.Model = fix.Suggested;
                        break;
                    case "provider":
                        RuntimeConfig.Provider = fix.Suggested;
                        break;
                    case "max_tokens":
                        if (int.TryParse(fix.Suggested, out int tokens) && tokens > 0)
                        {
                            RuntimeConfig.MaxTokens = tokens;
                            _client.Options.MaxTokens = tokens;
                        }
                        break;
                    case "timeout_seconds":
                        if (int.TryParse(fix.Suggested, out int to) && to > 0)
                        {
                            RuntimeConfig.TimeoutSeconds = to;
                            _client.Options.TimeoutSeconds = to;
                        }
                        break;
                    case "log_lines":
                        if (int.TryParse(fix.Suggested, out int lines) && lines > 0)
                        {
                            RuntimeConfig.LogLinesForAnalysis = lines;
                        }
                        break;
                    case "api_key":
                        ignoredKeyFixes++;
                        continue;
                    default:
                        continue;
                }

                applied++;
            }
            catch (Exception ex)
            {
                AuraLog.Exception("FixesPage.Apply '" + fix.Key + "'", ex);
            }
        }

        // Reaplica tudo (caso provider tenha mudado o modelo/base).
        RuntimeConfig.Apply(_client);
        ShowCurrentConfig();

        string ignoredNote = ignoredKeyFixes > 0
            ? $"\n{ignoredKeyFixes} sugestão(ões) de chave de API ignorada(s): digite a chave manualmente na aba Assistente."
            : string.Empty;

        StatusLabel.Text =
            $"Aplicadas {applied} de {selected.Count} correção(ões).\n\n" +
            "Configuração atual:\n" + ShowCurrentConfigRaw() + ignoredNote;
        AuraLog.Info("Correções aplicadas: " + applied + "/" + selected.Count);
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        Preferences.Default.Remove("ai_provider");
        Preferences.Default.Remove("ai_model");
        Preferences.Default.Remove("ai_max_tokens");
        Preferences.Default.Remove("ai_timeout_seconds");
        Preferences.Default.Remove("ai_log_lines");
        Preferences.Default.Remove("ai_api_key");

        RuntimeConfig.Apply(_client);
        _pending = new List<FixProposal>();
        FixesView.ItemsSource = null;
        ShowCurrentConfig();
        StatusLabel.Text = "Configuração restaurada para o padrão.";
    }
}
