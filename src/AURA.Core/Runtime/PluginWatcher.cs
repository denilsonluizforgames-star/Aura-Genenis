using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using AURA.Core.Abstractions;
using AURA.Core.Launchers;
using AURA.Core.Logging;

namespace AURA.Core.Runtime
{
    /// <summary>
    /// Loads external assemblies ("plugins") from a plugins directory into a
    /// collectible AssemblyLoadContext and watches that directory for changes.
    /// When a .dll is added or replaced the affected load context is unloaded
    /// and the plugins are reloaded, enabling hot-reload of launchers and other
    /// extension points while AURA keeps running.
    /// </summary>
    public sealed class PluginWatcher : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _pluginsRoot;
        private readonly FileSystemWatcher _watcher;
        private readonly object _sync = new object();
        private readonly List<string> _pluginPaths = new List<string>();

        private PluginLoadContext _context;
        private List<ILauncher> _launchers = new List<ILauncher>();
        private List<IPlugin> _plugins = new List<IPlugin>();

        public PluginWatcher(ILogger logger, string pluginsRoot = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _pluginsRoot = string.IsNullOrWhiteSpace(pluginsRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AURA", "plugins")
                : ExpandHome(pluginsRoot);

            Directory.CreateDirectory(_pluginsRoot);

            _watcher = new FileSystemWatcher(_pluginsRoot)
            {
                Filter = "*.dll",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Created += (s, e) => ScheduleReload();
            _watcher.Changed += (s, e) => ScheduleReload();
            _watcher.Deleted += (s, e) => ScheduleReload();
            _watcher.Renamed += (s, e) => ScheduleReload();
            _watcher.EnableRaisingEvents = true;

            Reload();
        }

        /// <summary>Plugins root directory (created on demand).</summary>
        public string PluginsRoot => _pluginsRoot;

        /// <summary>Launchers discovered in the current plugins set.</summary>
        public IReadOnlyList<ILauncher> Launchers => _launchers;

        /// <summary>Plugins implementing <see cref="IPlugin"/> discovered in the current set.</summary>
        public IReadOnlyList<IPlugin> Plugins => _plugins;

        /// <summary>Full paths of the plugin assemblies currently loaded.</summary>
        public IReadOnlyList<string> PluginPaths => _pluginPaths;

        /// <summary>
        /// Does an initial (re)load of all plugins. Call after plugin files are
        /// replaced to force a refresh; the watcher calls this on its own when
        /// the directory changes.
        /// </summary>
        public void Reload()
        {
            lock (_sync)
            {
                try
                {
                    UnloadContext();

                    _launchers = new List<ILauncher>();
                    _plugins = new List<IPlugin>();

                    string[] dlls = Directory.GetFiles(_pluginsRoot, "*.dll");
                    if (dlls.Length == 0)
                    {
                        return;
                    }

                    _context = new PluginLoadContext(_pluginsRoot);
                    foreach (string dll in dlls.OrderBy(d => d))
                    {
                        TryLoadPlugin(dll);
                    }

                    _logger.Info("Plugins carregados: " + string.Join(", ", _pluginPaths) +
                        " | launchers: " + _launchers.Count);
                }
                catch (Exception ex)
                {
                    _logger.Warning("Falha ao recarregar plugins: " + ex.Message);
                }
            }
        }

        public void Dispose()
        {
            _watcher.Dispose();
            lock (_sync)
            {
                UnloadContext();
            }
        }

        private void TryLoadPlugin(string dllPath)
        {
            try
            {
                Assembly assembly = _context.LoadFromAssemblyPath(dllPath);

                Type[] launcherTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface
                        && typeof(ILauncher).IsAssignableFrom(t))
                    .ToArray();

                Type[] pluginTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface
                        && typeof(IPlugin).IsAssignableFrom(t))
                    .ToArray();

                if (launcherTypes.Length == 0 && pluginTypes.Length == 0)
                {
                    _logger.Warning("Plugin sem tipos conhecidos ignorado: " + Path.GetFileName(dllPath));
                    return;
                }

                foreach (Type type in launcherTypes)
                {
                    ILauncher launcher = (ILauncher)Activator.CreateInstance(type);
                    _launchers.Add(launcher);
                }

                foreach (Type type in pluginTypes)
                {
                    IPlugin plugin = (IPlugin)Activator.CreateInstance(type);
                    plugin.Load();
                    _plugins.Add(plugin);
                }

                _pluginPaths.Add(dllPath);
            }
            catch (Exception ex)
            {
                _logger.Warning("Plugin inválido: " + Path.GetFileName(dllPath) + " -> " + ex.Message);
            }
        }

        private void UnloadContext()
        {
            foreach (IPlugin plugin in _plugins)
            {
                try
                {
                    plugin.Unload();
                }
                catch (Exception ex)
                {
                    _logger.Warning("Falha ao descarregar plugin: " + ex.Message);
                }
            }

            _plugins.Clear();
            _pluginPaths.Clear();

            if (_context == null)
            {
                return;
            }

            try
            {
                _context.Unload();
            }
            catch (Exception ex)
            {
                _logger.Warning("Falha ao descarregar contexto de plugins: " + ex.Message);
            }
            finally
            {
                _context = null;
            }
        }

        private void ScheduleReload()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(200);
                Reload();
            });
        }

        private static string ExpandHome(string path)
        {
            if (path.StartsWith("~/", StringComparison.Ordinal))
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    path.Substring(2));
            }
            else if (path == "~")
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Collectible load context so plugin assemblies can be released on
        /// reload. Plugin dependencies are resolved from the plugins directory
        /// first, then from the default context (framework + AURA.Core).
        /// </summary>
        private sealed class PluginLoadContext : AssemblyLoadContext
        {
            private readonly string _pluginsRoot;

            public PluginLoadContext(string pluginsRoot)
                : base("AURA.Plugins." + Guid.NewGuid().ToString("N"), isCollectible: true)
            {
                _pluginsRoot = pluginsRoot;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (assemblyName.Name == "AURA.Core")
                {
                    return typeof(ILauncher).Assembly;
                }

                string candidate = Path.Combine(_pluginsRoot, assemblyName.Name + ".dll");
                if (File.Exists(candidate))
                {
                    return LoadFromAssemblyPath(candidate);
                }

                return null;
            }
        }
    }
}
