using AURA.AI;
using AURA.Mobile.Diagnostics;
using AURA.Mobile.Speech;

namespace AURA.Mobile.Pages;

public partial class ChatPage : ContentPage
{
    private readonly OpenRouterClient _client;
    private readonly AURA.Memory.MemoryStore _memory;
    private readonly VoiceAssistantService? _voice;

    public ChatPage(OpenRouterClient client, AURA.Memory.MemoryStore memory,
        VoiceAssistantService? voice = null)
    {
        InitializeComponent();
        _client = client;
        _memory = memory;
        _voice = voice;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AiConfig.Load(_client);
    }

    private async void OnCopyClicked(object sender, EventArgs e)
    {
        string text = AnswerLabel.Text ?? string.Empty;
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

    private async void OnSendClicked(object sender, EventArgs e)
    {
        // O painel AiConfig persiste a chave/provedor/modelo a cada alteração;
        // aqui só reforça no client antes de chamar a IA.
        AiConfig.ApplyToClient();
        string apiKey = _client.Options.ApiKey ?? string.Empty;
        string question = QuestionEditor.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(question))
        {
            AnswerLabel.Text = "Digite uma pergunta primeiro.";
            return;
        }

        QuestionEditor.Text = string.Empty;

        if (!string.IsNullOrWhiteSpace(apiKey) &&
            (apiKey.Length > 200 ||
             apiKey.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0))
        {
            AnswerLabel.Text = "Chave de API inválida (parece conter texto de log). " +
                "Toque em 'Restaurar padrão' na aba Correções e digite a chave manualmente.";
            return;
        }

        bool hasUsableKey = !string.IsNullOrWhiteSpace(apiKey) &&
            apiKey.Length <= 200 &&
            apiKey.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) < 0;

        SendButton.IsEnabled = false;
        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;
        AnswerLabel.Text = hasUsableKey ? "Pensando..." : "Buscando na internet aberta...";

        try
        {
            string answer;

            if (hasUsableKey)
            {
                var assistant = new AiAssistant(_client, _memory);
                answer = await assistant.AskAsync(question);
            }
            else
            {
                // Sem chave de API: refaz a solicitação na internet aberta
                // (sites de IA/busca que não exigem login) e retorna os resultados.
                var web = new WebSearchTool();
                answer = await web.ExecuteAsync(
                    "{\"query\": " + System.Text.Json.JsonSerializer.Serialize(question) + "}");
                answer = "Sem chave de IA configurada, busquei na internet aberta:\n\n" + answer;
            }

            AnswerLabel.Text = answer;
            _voice?.SetLastUtterance(answer);
        }
        catch (Exception ex)
        {
            AnswerLabel.Text = "Erro: " + ex.Message;
            AuraLog.Exception("ChatPage.OnSendClicked", ex);
        }
        finally
        {
            SendButton.IsEnabled = true;
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }
}
