using System;
using System.Threading;
using System.Threading.Tasks;
using AURA.Mobile.Diagnostics;

namespace AURA.Mobile.Speech
{
    /// <summary>
    /// Voz da AURA como assistente (não recepcionista): guarda a última
    /// resposta da IA e permite falar/parar com um toque no botão flutuante,
    /// em qualquer aba do app. É registrado como singleton e exposto também
    /// como instância estática para o FAB nativo do Android acionar.
    /// </summary>
    public sealed class VoiceAssistantService
    {
        private static VoiceAssistantService? _instance;

        private readonly ISpeechService _tts;
        private readonly object _lock = new();
        private CancellationTokenSource? _cts;

        /// <summary>Última resposta/contexto a ser falado.</summary>
        public string LastUtterance { get; private set; } = string.Empty;

        public bool IsSpeaking
        {
            get
            {
                lock (_lock)
                {
                    return _cts != null;
                }
            }
        }

        /// <summary>Instância estática para o FAB nativo (Android) acessar.</summary>
        public static VoiceAssistantService? Instance
        {
            get => _instance;
            set => _instance = value;
        }

        public VoiceAssistantService(ISpeechService tts)
        {
            _tts = tts;
            _instance = this;
        }

        /// <summary>Registra a resposta mais recente (usada pelo FAB).</summary>
        public void SetLastUtterance(string text)
        {
            LastUtterance = text ?? string.Empty;
        }

        /// <summary>
        /// Alterna: se está falando, para; senão fala a última resposta.
        /// Se ainda não houver resposta, fala uma saudação de assistente.
        /// </summary>
        public async Task ToggleAsync()
        {
            lock (_lock)
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    return;
                }
            }

            string text = string.IsNullOrWhiteSpace(LastUtterance)
                ? "Estou aqui. Me pergunte qualquer coisa na aba Chat ou no Agente."
                : LastUtterance;

            await SpeakAsync(text).ConfigureAwait(false);
        }

        /// <summary>Fala o texto informado, interrompendo qualquer fala anterior.</summary>
        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Stop();

            var cts = new CancellationTokenSource();
            lock (_lock)
            {
                _cts = cts;
            }

            try
            {
                await _tts.InitializeAsync(cts.Token).ConfigureAwait(false);
                await _tts.SpeakAsync(text, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // fala interrompida pelo usuário: comportamento esperado
            }
            catch (Exception ex)
            {
                AuraLog.Exception("VoiceAssistantService.SpeakAsync", ex);
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_cts, cts))
                    {
                        _cts = null;
                    }
                }

                cts.Dispose();
            }
        }

        /// <summary>Para a fala em andamento, se houver.</summary>
        public void Stop()
        {
            CancellationTokenSource? cts;
            lock (_lock)
            {
                cts = _cts;
                _cts = null;
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _ = _tts.StopAsync();
        }
    }
}
