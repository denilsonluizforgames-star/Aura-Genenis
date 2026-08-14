using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AURA.Core.Logging;
using AURA.Core.Runtime;

namespace AURA.Memory
{
    /// <summary>
    /// F3/F5 backend: short-term working memory for the assistant. Mirrors the
    /// memory store exposed by the mobile app (AURA.Memory) - an append-only
    /// journal of conversation turns and cell lifecycle events, persisted to
    /// ~/AURA/memory.json so the assistant keeps context across restarts.
    ///
    /// This is the backend the mobile app's MemoryService/MemoryManager consume;
    /// the CLI wiring lives in AURA.AI.
    /// </summary>
    public sealed class MemoryStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };

        private readonly ILogger _logger;
        private readonly string _path;
        private readonly object _sync = new object();

        public MemoryStore(ILogger logger, string path = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _path = path ?? SimulationRuntime.ExpandUserHome("~/AURA/memory.json");
        }

        public string Path => _path;

        public void Append(MemoryEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            lock (_sync)
            {
                try
                {
                    MemoryDocument document = LoadLocked();
                    document.Entries.Add(entry);
                    document.SavedAtUtc = DateTime.UtcNow;

                    PersistLocked(document);
                }
                catch (Exception ex)
                {
                    _logger.Error("Falha ao gravar memória em '" + _path + "': " + ex.Message);
                }
            }
        }

        public IReadOnlyList<MemoryEntry> Read(int tail = 64)
        {
            lock (_sync)
            {
                MemoryDocument document = LoadLocked();
                int skip = document.Entries.Count > tail ? document.Entries.Count - tail : 0;
                var slice = new List<MemoryEntry>();
                for (int i = skip; i < document.Entries.Count; i++)
                {
                    slice.Add(document.Entries[i]);
                }

                return slice;
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                try
                {
                    if (File.Exists(_path))
                    {
                        File.Delete(_path);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning("Não foi possível limpar a memória: " + ex.Message);
                }
            }
        }

        private MemoryDocument LoadLocked()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return new MemoryDocument();
                }

                string json = File.ReadAllText(_path);
                MemoryDocument document = JsonSerializer.Deserialize<MemoryDocument>(json, Options);
                return document ?? new MemoryDocument();
            }
            catch (Exception ex)
            {
                _logger.Warning("Memória em '" + _path + "' está corrompida; recomeçando. (" + ex.Message + ")");
                return new MemoryDocument();
            }
        }

        private void PersistLocked(MemoryDocument document)
        {
            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(document, Options);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            try
            {
                File.Move(tmp, _path, overwrite: true);
            }
            catch
            {
                if (File.Exists(tmp))
                {
                    File.Delete(tmp);
                }

                throw;
            }
        }

        private sealed class MemoryDocument
        {
            public List<MemoryEntry> Entries { get; set; } = new List<MemoryEntry>();

            public DateTime? SavedAtUtc { get; set; }
        }
    }
}
