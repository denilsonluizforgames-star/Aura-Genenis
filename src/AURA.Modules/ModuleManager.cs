using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AURA.Core.Configuration;
using AURA.Core.Events;
using AURA.Core.Logging;
using AURA.Modules.Loja;

namespace AURA.Modules
{
    /// <summary>
    /// Gerencia os módulos opcionais: baixa o pacote JSON do repositório,
    /// aplica (habilita em modules.json) e remove (desabilita + limpa os dados).
    /// O código da função já existe no app; o "download + aplicar" é o ato de
    /// desbloquear o módulo e persistir essa escolha.
    /// </summary>
    public sealed class ModuleManager
    {
        private readonly ILogger _logger;
        private readonly string _packagesDir;
        private readonly string _modulesPath;
        private readonly string _pluginsRoot;
        private readonly ConfigLoader _configLoader;
        private readonly HttpClient _http;
        private readonly EventBus _events;

        public ModuleManager(ILogger logger, string packagesDir, string modulesPath, EventBus events = null, HttpMessageHandler httpHandler = null, string pluginsRoot = null)
        {
            _logger = logger;
            _packagesDir = packagesDir;
            _modulesPath = modulesPath;
            _pluginsRoot = pluginsRoot ?? Path.Combine(Path.GetTempPath(), "aura_plugins");
            _configLoader = new ConfigLoader(logger);
            _events = events;
            _http = httpHandler != null
                ? new HttpClient(httpHandler) { Timeout = TimeSpan.FromSeconds(40) }
                : new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        }

        public string GetPackagePath(string id) => Path.Combine(_packagesDir, id, "module.json");

        public bool IsDownloaded(string id) => File.Exists(GetPackagePath(id));

        public bool IsApplied(string id)
        {
            ModulesConfiguration config = LoadModules();
            return config?.Modules != null && config.Modules.IsEnabled(id);
        }

        /// <summary>
        /// Baixa o pacote JSON do módulo (manifesto) e valida o ID. Lança
        /// exceção se a rede falhar ou o pacote for inválido.
        /// </summary>
        public async Task DownloadAsync(string id)
        {
            ModuleInfo info = ModuleCatalog.GetById(id);
            if (info == null)
            {
                throw new InvalidOperationException("Módulo desconhecido: " + id);
            }

            if (info.IsCore)
            {
                throw new InvalidOperationException("Módulo do núcleo não precisa ser baixado: " + id);
            }

            if (string.IsNullOrWhiteSpace(info.PackageUrl))
            {
                throw new InvalidOperationException("Módulo ainda não tem pacote para baixar: " + id);
            }

            _logger.Info("Baixando módulo " + id + " de " + info.PackageUrl);

            string json;
            using (var resp = await _http.GetAsync(info.PackageUrl))
            {
                resp.EnsureSuccessStatusCode();
                json = await resp.Content.ReadAsStringAsync();
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("id", out JsonElement idElement) ||
                !string.Equals(idElement.GetString(), id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Pacote inválido para o módulo " + id);
            }

            string target = GetPackagePath(id);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, json);
            _logger.Info("Módulo baixado e salvo em " + target);
        }

        /// <summary>Aplica (habilita) um módulo já baixado e persiste em modules.json.</summary>
        public void Apply(string id)
        {
            ModuleInfo info = ModuleCatalog.GetById(id);
            if (info == null)
            {
                throw new InvalidOperationException("Módulo desconhecido: " + id);
            }

            if (info.IsCore)
            {
                return; // núcleo já está sempre aplicado
            }

            if (!IsDownloaded(id))
            {
                throw new InvalidOperationException("Baixe o módulo antes de aplicar: " + id);
            }

            ModulesConfiguration config = LoadModules();
            config.Modules.SetEnabled(id, true);
            SaveModules(config);
            _logger.Info("Módulo aplicado: " + id);
            _events?.Publish(new ModuleStateChangedEvent { ModuleId = id, Applied = true });
        }

        /// <summary>Remove (desabilita) um módulo e apaga o pacote baixado.</summary>
        public void Remove(string id)
        {
            ModuleInfo info = ModuleCatalog.GetById(id);
            if (info == null)
            {
                throw new InvalidOperationException("Módulo desconhecido: " + id);
            }

            if (info.IsCore)
            {
                throw new InvalidOperationException("Módulo do núcleo não pode ser removido: " + id);
            }

            ModulesConfiguration config = LoadModules();
            config.Modules.SetEnabled(id, false);
            SaveModules(config);

            // use LojaUninstaller to remove installed files safely
            var uninstaller = new LojaUninstaller(_logger, _packagesDir, _pluginsRoot);
            uninstaller.Uninstall(id);

            _logger.Info("Módulo removido: " + id);
            _events?.Publish(new ModuleStateChangedEvent { ModuleId = id, Applied = false });
        }

        private ModulesConfiguration LoadModules()
        {
            return _configLoader.LoadModules(_modulesPath);
        }

        private void SaveModules(ModulesConfiguration config)
        {
            _configLoader.SaveModules(_modulesPath, config);
        }
    }
}
