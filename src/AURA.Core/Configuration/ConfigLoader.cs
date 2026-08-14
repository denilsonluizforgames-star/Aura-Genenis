using System;
using System.IO;
using System.Text.Json;
using AURA.Core.Logging;

namespace AURA.Core.Configuration
{
    /// <summary>
    /// Loads and saves the JSON configuration files used by AURA
    /// (config/settings.json and config/modules.json). Uses System.Text.Json
    /// (part of the .NET runtime) so the project has zero third-party NuGet
    /// dependencies and restores/builds offline.
    /// </summary>
    public sealed class ConfigLoader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly ILogger _logger;

        public ConfigLoader(ILogger logger)
        {
            _logger = logger ?? new ConsoleLogger();
        }

        public AuraConfiguration LoadSettings(string path)
        {
            AuraConfiguration config = Load<AuraConfiguration>(path);

            if (config == null)
            {
                config = new AuraConfiguration();
                SaveSettings(path, config);
            }

            return config;
        }

        public void SaveSettings(string path, AuraConfiguration config)
        {
            Save(path, config);
        }

        public ModulesConfiguration LoadModules(string path)
        {
            ModulesConfiguration config = Load<ModulesConfiguration>(path);

            if (config == null)
            {
                config = new ModulesConfiguration();
                SaveModules(path, config);
            }

            return config;
        }

        public void SaveModules(string path, ModulesConfiguration config)
        {
            Save(path, config);
        }

        private T Load<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    _logger.Warning("Arquivo de configuração não encontrado: " + path);
                    return null;
                }

                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, Options);
            }
            catch (Exception ex)
            {
                _logger.Error("Falha ao carregar '" + path + "': " + ex.Message);
                return null;
            }
        }

        private void Save<T>(string path, T config)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(config, Options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.Error("Falha ao salvar '" + path + "': " + ex.Message);
            }
        }
    }
}
