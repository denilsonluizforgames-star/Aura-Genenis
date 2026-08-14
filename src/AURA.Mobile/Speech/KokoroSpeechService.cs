using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Media;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AURA.Mobile.Speech
{
    /// <summary>
    /// Kokoro TTS (v1.0 int8) via ONNX Runtime no Android. Pipeline:
    /// texto → fonemas (KokoroPhonemizer) → tokens (KokoroVocab) →
    /// sessão ONNX (tokens/style/speed) → amostras float 24kHz →
    /// PCM16 → reprodução via AudioTrack.
    ///
    /// Assets empacotados como MauiAsset: kokoro-v1.0.int8.onnx,
    /// pf_dora.f32 (vetor de voz pt-br) e kokoro-config.json (vocab).
    /// O modelo é copiado do pacote para o cache na primeira carga.
    /// </summary>
    public sealed class KokoroSpeechService : ISpeechService, IDisposable
    {
        public const int SampleRate = 24000;
        private const int StyleDim = 256;
        private const int VoiceRows = 510;

        private readonly KokoroPhonemizer _phonemizer = new();
        private readonly object _lock = new();

        private InferenceSession? _session;
        private float[]? _voice; // 510 * 256

        public bool IsReady => Volatile.Read(ref _session) != null;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (_session != null)
                {
                    return;
                }
            }

            string modelPath = await CopyAssetToCacheAsync("kokoro-v1.0.int8.onnx", ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            float[] voice = await LoadVoiceAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            var options = new SessionOptions
            {
                EnableMemoryPattern = true,
                IntraOpNumThreads = Environment.ProcessorCount,
                InterOpNumThreads = 1
            };

            lock (_lock)
            {
                if (_session == null)
                {
                    _session = new InferenceSession(modelPath, options);
                    _voice = voice;
                }
            }
        }

        public async Task SpeakAsync(string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!IsReady)
            {
                throw new InvalidOperationException(
                    "TTS não inicializado. Chame InitializeAsync antes de SpeakAsync.");
            }

            // Inferência ONNX é CPU-bound: executa fora do thread da UI.
            byte[] pcm = await Task.Run(() =>
            {
                string phonemes = _phonemizer.Phonemize(text);
                float[] audio = Synthesize(phonemes, ct);
                return ToPcm16(audio);
            }, ct).ConfigureAwait(false);

            await Task.Run(() => PlayPcm(pcm, ct), ct).ConfigureAwait(false);
        }

        public Task StopAsync()
        {
            StopAudio();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executa a inferência ONNX: tokens + style(voice[len(tokens)]) + speed.
        /// Retorna as amostras float em 24kHz (mesma convenção do kokoro-onnx).
        /// </summary>
        private float[] Synthesize(string phonemes, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // tokenize: mapeia cada caractere de fonema via vocab, descarta inválidos.
            var tokenIds = new List<int>(phonemes.Length);
            foreach (char ch in phonemes)
            {
                if (KokoroVocab.Map.TryGetValue(ch, out int id))
                {
                    tokenIds.Add(id);
                }
            }

            if (tokenIds.Count == 0)
            {
                throw new InvalidOperationException("Fonemas não geraram nenhum token.");
            }

            if (tokenIds.Count > VoiceRows - 1)
            {
                throw new InvalidOperationException(
                    "Texto muito longo para o modelo (" + tokenIds.Count + " tokens; máx " +
                    (VoiceRows - 1) + ").");
            }

            // style = voice[len(tokens)] (convenção kokoro-onnx v1.0).
            int styleIndex = tokenIds.Count;
            float[] style = new float[StyleDim];
            int rowBase = styleIndex * StyleDim;
            for (int j = 0; j < StyleDim; j++)
            {
                style[j] = _voice![rowBase + j];
            }

            // tokens de entrada: [0] + tokens + [0] (pad).
            int T = tokenIds.Count;
            var tokenTensor = new DenseTensor<long>(new[] { 1, T + 2 });
            tokenTensor[0, 0] = 0;
            for (int i = 0; i < T; i++)
            {
                int id = tokenIds[i];
                tokenTensor[0, i + 1] = id >= 0 ? id : 4;
            }
            tokenTensor[0, T + 1] = 0;

            var styleTensor = new DenseTensor<float>(style, new[] { 1, StyleDim });
            var speedTensor = new DenseTensor<float>(new[] { 1.0f }, new[] { 1 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("tokens", tokenTensor),
                NamedOnnxValue.CreateFromTensor("style", styleTensor),
                NamedOnnxValue.CreateFromTensor("speed", speedTensor)
            };

            InferenceSession session;
            lock (_lock)
            {
                session = _session ?? throw new InvalidOperationException("TTS não inicializado.");
            }

            using var results = session.Run(inputs);
            float[] audio = results[0].AsTensor<float>().ToArray();
            return audio;
        }

        private static byte[] ToPcm16(float[] samples)
        {
            var pcm = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i];
                if (s > 1f) s = 1f;
                if (s < -1f) s = -1f;
                short v = (short)Math.Round(s * short.MaxValue);
                pcm[i * 2] = (byte)(v & 0xFF);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            return pcm;
        }

        private static AudioTrack? _activeTrack;
        private static readonly object AudioLock = new();

        private void PlayPcm(byte[] pcm, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            int minBuffer = AudioTrack.GetMinBufferSize(SampleRate, ChannelOut.Mono,
                Android.Media.Encoding.Pcm16bit);
            int bufferSize = Math.Max(minBuffer, pcm.Length);

            var track = new AudioTrack.Builder()
                .SetAudioAttributes(new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Media)
                    .SetContentType(AudioContentType.Speech)
                    .Build())
                .SetAudioFormat(new AudioFormat.Builder()
                    .SetEncoding(Android.Media.Encoding.Pcm16bit)
                    .SetSampleRate(SampleRate)
                    .SetChannelMask(ChannelOut.Mono)
                    .Build())
                .SetBufferSizeInBytes(bufferSize)
                .SetTransferMode(AudioTrackMode.Static)
                .Build();

            lock (AudioLock)
            {
                _activeTrack?.Stop();
                _activeTrack?.Release();
                _activeTrack = track;
            }

            try
            {
                track.Write(pcm, 0, pcm.Length);
                track.Play();

                // Espera o áudio terminar (ou ser cancelado/interrompido).
                while (track.PlayState == PlayState.Playing)
                {
                    if (ct.IsCancellationRequested)
                    {
                        track.Stop();
                        return;
                    }
                    Thread.Sleep(20);
                }
            }
            finally
            {
                if (ReferenceEquals(_activeTrack, track))
                {
                    _activeTrack = null;
                }
                track.Release();
            }
        }

        private static void StopAudio()
        {
            lock (AudioLock)
            {
                if (_activeTrack != null)
                {
                    try { _activeTrack.Stop(); } catch { /* ignorar */ }
                    try { _activeTrack.Release(); } catch { /* ignorar */ }
                    _activeTrack = null;
                }
            }
        }

        private static async Task<string> CopyAssetToCacheAsync(string assetName, CancellationToken ct)
        {
            string cacheDir = FileSystem.CacheDirectory;
            string dest = Path.Combine(cacheDir, assetName);

            if (File.Exists(dest))
            {
                return dest;
            }

            Directory.CreateDirectory(cacheDir);
            using System.IO.Stream src = await FileSystem.OpenAppPackageFileAsync(assetName).ConfigureAwait(false);
            using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write);
            await src.CopyToAsync(dst, 81920, ct).ConfigureAwait(false);
            return dest;
        }

        private static async Task<float[]> LoadVoiceAsync(CancellationToken ct)
        {
            using System.IO.Stream stream = await FileSystem.OpenAppPackageFileAsync("pf_dora.f32").ConfigureAwait(false);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, 81920, ct).ConfigureAwait(false);
            byte[] bytes = ms.ToArray();

            int count = VoiceRows * StyleDim;
            if (bytes.Length != count * 4)
            {
                throw new InvalidOperationException(
                    "pf_dora.f32 inválido: " + bytes.Length + " bytes (esperado " + (count * 4) + ").");
            }

            var result = new float[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = BitConverter.ToSingle(bytes, i * 4);
            }
            return result;
        }

        public void Dispose()
        {
            StopAudio();
            lock (_lock)
            {
                _session?.Dispose();
                _session = null;
                _voice = null;
            }
        }
    }
}
