using System;
using System.IO;
using System.Text.Json;
using AURA.Core.Logging;

namespace AURA.Core.Runtime
{
    /// <summary>
    /// Persists the cell index to disk (default ~/AURA/cells.json) so cells
    /// survive AURA restarts. On boot the runtime loads the index back and
    /// recovers live processes (see SimulationRuntime.LoadFromStoreAsync).
    /// </summary>
    public sealed class CellStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly ILogger _logger;
        private readonly string _path;
        private readonly object _sync = new object();

        public CellStore(ILogger logger, string path = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _path = path ?? SimulationRuntime.ExpandUserHome("~/AURA/cells.json");
        }

        public string Path => _path;

        /// <summary>Saves all runtime cells to disk (atomic replace).</summary>
        public void Save(SimulationRuntime runtime)
        {
            lock (_sync)
            {
                try
                {
                    string directory = System.IO.Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var document = new CellStoreDocument
                    {
                        Cells = new System.Collections.Generic.List<Cell>(runtime.Cells),
                        SavedAtUtc = DateTime.UtcNow
                    };

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
                catch (Exception ex)
                {
                    _logger.Error("Falha ao salvar células em '" + _path + "': " + ex.Message);
                }
            }
        }

        /// <summary>Loads the persisted cell index. Returns an empty list when missing/corrupt.</summary>
        public System.Collections.Generic.List<Cell> Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return new System.Collections.Generic.List<Cell>();
                }

                string json = File.ReadAllText(_path);
                CellStoreDocument document = JsonSerializer.Deserialize<CellStoreDocument>(json, Options);

                return document?.Cells ?? new System.Collections.Generic.List<Cell>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Falha ao carregar células de '" + _path + "': " + ex.Message);
                return new System.Collections.Generic.List<Cell>();
            }
        }

        private sealed class CellStoreDocument
        {
            public System.Collections.Generic.List<Cell> Cells { get; set; } =
                new System.Collections.Generic.List<Cell>();

            public DateTime? SavedAtUtc { get; set; }
        }
    }
}
