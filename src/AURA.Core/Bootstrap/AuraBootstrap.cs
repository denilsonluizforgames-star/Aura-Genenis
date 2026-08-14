using System;
using AURA.Core.Configuration;
using AURA.Core.DependencyInjection;
using AURA.Core.Events;
using AURA.Core.Logging;

namespace AURA.Core.Bootstrap
{
    /// <summary>
    /// Wires up the core services (logger, config loader, event bus,
    /// service container) that every AURA front-end (CLI or GUI) needs
    /// before it can start. Front-ends load their own modules afterwards.
    /// </summary>
    public sealed class AuraBootstrap
    {
        public ILogger Logger { get; private set; }

        public ServiceContainer Services { get; private set; }

        public EventBus Events { get; private set; }

        public AuraConfiguration Settings { get; private set; }

        public ModulesConfiguration Modules { get; private set; }

        public string SettingsPath { get; private set; }

        public string ModulesPath { get; private set; }

        public AuraBootstrap()
            : this(new ConsoleLogger())
        {
        }

        public AuraBootstrap(ILogger logger)
        {
            Logger = logger ?? new ConsoleLogger();
            Services = new ServiceContainer();
            Events = new EventBus();

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            SettingsPath = System.IO.Path.Combine(baseDirectory, "config", "settings.json");
            ModulesPath = System.IO.Path.Combine(baseDirectory, "config", "modules.json");
        }

        /// <summary>
        /// Initializes core services. Safe to call once at application start.
        /// </summary>
        public void Start()
        {
            Logger.Info("Inicializando " + Core.VersionInfo.FullName + "...");

            Services.RegisterInstance(Logger);
            Services.RegisterInstance(Events);
            Logger.Info("Core..............OK");

            var configLoader = new ConfigLoader(Logger);
            Services.RegisterInstance(configLoader);

            Settings = configLoader.LoadSettings(SettingsPath);
            Modules = configLoader.LoadModules(ModulesPath);
            Logger.Info("Configuração......OK");

            Logger.Info("Bootstrap.........OK");
            Logger.Info("Sistema iniciado.");
        }

        public void SaveModules()
        {
            var configLoader = Services.Resolve<ConfigLoader>();
            configLoader.SaveModules(ModulesPath, Modules);
        }

        public void SaveSettings()
        {
            var configLoader = Services.Resolve<ConfigLoader>();
            configLoader.SaveSettings(SettingsPath, Settings);
        }
    }
}
