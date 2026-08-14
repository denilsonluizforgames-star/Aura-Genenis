using System;
using System.Threading;
using System.Threading.Tasks;

namespace AURA.Mobile.Speech
{
    /// <summary>
    /// Motor de voz híbrido da AURA:
    ///  1. TTS nativo do Android (fala texto arbitrário, offline, pt-br) — o
    ///     motor padrão para as respostas da IA na conversação.
    ///  2. Kokoro on-device (ONNX) como fallback quando o TTS nativo não
    ///     existe ou falha no dispositivo.
    ///
    /// A UI chama apenas este serviço; a seleção do motor é transparente.
    /// </summary>
    public sealed class HybridSpeechService : ISpeechService
    {
        private readonly AndroidTtsSpeechService _android = new();
        private readonly KokoroSpeechService _kokoro = new();

        public bool IsReady => _android.IsReady || _kokoro.IsReady;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            try
            {
                await _android.InitializeAsync(ct).ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
                // TTS nativo indisponível: o Kokoro assume (carregado sob demanda).
            }
            catch (InvalidOperationException)
            {
                // Sem Activity (ex.: início do app): idem.
            }
        }

        public async Task SpeakAsync(string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                await _android.SpeakAsync(text, ct).ConfigureAwait(false);
                return;
            }
            catch (NotSupportedException)
            {
                // Motor nativo indisponível ou recusou o texto: cai para o Kokoro.
            }
            catch (InvalidOperationException)
            {
                // Sem Activity ainda: idem.
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            // Fallback: Kokoro on-device. Textos fora do dicionário do
            // fonemizador lançam NotSupportedException — o chamador decide.
            await _kokoro.InitializeAsync(ct).ConfigureAwait(false);
            await _kokoro.SpeakAsync(text, ct).ConfigureAwait(false);
        }

        public Task StopAsync()
        {
            _android.StopAsync();
            _kokoro.StopAsync();
            return Task.CompletedTask;
        }
    }
}
