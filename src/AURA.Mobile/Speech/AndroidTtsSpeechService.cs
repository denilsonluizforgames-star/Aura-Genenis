using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Android.Speech.Tts;
using TextToSpeech = Android.Speech.Tts.TextToSpeech;

namespace AURA.Mobile.Speech
{
    /// <summary>
    /// Sintetizador de voz usando o TTS nativo do Android (TextToSpeech).
    /// É o motor preferido da AURA para conversação porque fonemiza texto
    /// arbitrário em pt-br (e qualquer idioma instalado) offline, cobrindo
    /// as respostas reais da IA — que o Kokoro on-device não consegue.
    ///
    /// A sessão é criada sob demanda na primeira fala e reutilizada.
    /// </summary>
    public sealed class AndroidTtsSpeechService : ISpeechService, IDisposable
    {
        private readonly object _lock = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = new();
        private TextToSpeech? _tts;
        private bool _initFailed;
        private bool _disposed;

        public bool IsReady
        {
            get
            {
                lock (_lock)
                {
                    return _tts != null;
                }
            }
        }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (_tts != null)
                {
                    return Task.CompletedTask;
                }

                if (_initFailed)
                {
                    // Já sabemos que o motor nativo não está disponível:
                    // deixa o fallback (Kokoro) assumir.
                    return Task.FromException(new NotSupportedException(
                        "TTS nativo do Android indisponível neste dispositivo."));
                }

                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity == null)
                {
                    return Task.FromException(new InvalidOperationException(
                        "Sem Activity para criar o TTS nativo do Android."));
                }

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                TextToSpeech tts = default!;
                tts = new TextToSpeech(activity, new OnInitListener(status =>
                    OnInitCompleted(tts, status, tcs)));
                return tcs.Task;
            }
        }

        /// <summary>Chamado pelo TextToSpeech quando o motor termina de inicializar.</summary>
        private void OnInitCompleted(TextToSpeech tts, OperationResult status, TaskCompletionSource<bool> tcs)
        {
            lock (_lock)
            {
                if (status == OperationResult.Success)
                {
                    _tts = tts;
                    tcs.TrySetResult(true);
                }
                else
                {
                    _initFailed = true;
                    tts.Dispose();
                    tcs.TrySetException(new NotSupportedException(
                        "Falha ao inicializar o TTS nativo do Android (status " + status + ")."));
                }
            }
        }

        public async Task SpeakAsync(string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            await InitializeAsync(ct).ConfigureAwait(false);

            TextToSpeech tts;
            lock (_lock)
            {
                if (_tts == null)
                {
                    throw new NotSupportedException("TTS nativo do Android não inicializado.");
                }

                tts = _tts;
            }

            // Escolhe português do Brasil se estiver disponível; senão o padrão.
            var lang = new Java.Util.Locale("pt", "BR");
            if (tts.IsLanguageAvailable(lang) < LanguageAvailableResult.Available)
            {
                lang = Java.Util.Locale.Default;
            }

            tts.SetLanguage(lang);

            string utteranceId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[utteranceId] = tcs;

            tts.SetOnUtteranceProgressListener(new UtteranceListener(
                (id, _) => Complete(id, completed: true),
                (id, _) => Complete(id, completed: false)));

            using (ct.Register(() => Complete(utteranceId, completed: false)))
            {
                OperationResult result = tts.Speak(text, QueueMode.Flush, null, utteranceId);
                if (result != OperationResult.Success)
                {
                    _pending.TryRemove(utteranceId, out _);
                    throw new NotSupportedException("TTS nativo recusou falar o texto.");
                }

                await tcs.Task.ConfigureAwait(false);
            }

            void Complete(string id, bool completed)
            {
                if (_pending.TryRemove(id, out var pending))
                {
                    pending.TrySetResult(completed);
                }
            }
        }

        public Task StopAsync()
        {
            TextToSpeech? tts;
            lock (_lock)
            {
                tts = _tts;
            }

            if (tts != null)
            {
                try
                {
                    tts.Stop();
                }
                catch (Exception)
                {
                    // ignora: motor parou com a Activity
                }
            }

            foreach (var tcs in _pending.Values)
            {
                tcs.TrySetResult(false);
            }

            _pending.Clear();
            return Task.CompletedTask;
        }

        /// <summary>Implementação de OnInitListener (callback de inicialização).</summary>
        private sealed class OnInitListener : Java.Lang.Object, TextToSpeech.IOnInitListener
        {
            private readonly Action<OperationResult> _onInit;

            public OnInitListener(Action<OperationResult> onInit)
            {
                _onInit = onInit;
            }

            public void OnInit(OperationResult status)
            {
                _onInit(status);
            }
        }

        /// <summary>Observa o término de cada utterance para o SpeakAsync poder aguardar.</summary>
        private sealed class UtteranceListener : UtteranceProgressListener
        {
            private readonly Action<string, bool> _onDone;
            private readonly Action<string, bool> _onError;

            public UtteranceListener(Action<string, bool> onDone, Action<string, bool> onError)
            {
                _onDone = onDone;
                _onError = onError;
            }

            public override void OnDone(string? utteranceId)
            {
                if (utteranceId != null)
                {
                    _onDone(utteranceId, true);
                }
            }

            public override void OnError(string? utteranceId)
            {
                if (utteranceId != null)
                {
                    _onError(utteranceId, false);
                }
            }

            public override void OnStart(string? utteranceId)
            {
                // nada a fazer
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopAsync().GetAwaiter().GetResult();
            lock (_lock)
            {
                _tts?.Dispose();
                _tts = null;
            }
        }
    }
}
