using System.Threading;
using System.Threading.Tasks;

namespace AURA.Mobile.Speech
{
    /// <summary>
    /// Síntese de fala local (on-device) da AURA. Abstrai o motor TTS
    /// (atualmente Kokoro TTS via ONNX Runtime) para que as páginas e a
    /// resposta da IA apenas chamem SpeakAsync sem conhecer a implementação.
    /// </summary>
    public interface ISpeechService
    {
        /// <summary>True após o modelo/voz serem carregados com sucesso.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Carrega o modelo ONNX, a voz e o vocab do pacote do app.
        /// Pode ser chamado uma vez (resultado é cacheado).
        /// </summary>
        Task InitializeAsync(CancellationToken ct = default);

        /// <summary>
        /// Converte o texto em áudio (fala) e reproduz no dispositivo.
        /// Em textos não cobertos pelo fonemizador atual lança NotSupportedException.
        /// </summary>
        Task SpeakAsync(string text, CancellationToken ct = default);

        /// <summary>Interrompe qualquer fala em andamento.</summary>
        Task StopAsync();
    }
}
