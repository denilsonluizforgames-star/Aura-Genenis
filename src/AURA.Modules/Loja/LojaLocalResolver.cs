using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AURA.Core.Logging;
using AURA.Modules;

namespace AURA.Modules.Loja
{
    public sealed class LojaEntry
    {
        public string Id { get; set; } = string.Empty;
        public List<string> PayloadFiles { get; set; } = new List<string>();
    }

    public sealed class LojaLocalResolver
    {
        private static readonly Regex SafeName = new Regex("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

        private readonly ILogger _logger;
        private readonly string _lojaRoot;
        private readonly string _packagesDir;
        private readonly string _pluginsRoot;
        private readonly Func<string, ModuleInfo?> _getById;

        public LojaLocalResolver(ILogger logger, string lojaRoot, string packagesDir, string pluginsRoot,
            Func<string, ModuleInfo?>? getById = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lojaRoot = NormalizePath(lojaRoot ?? "~/AURA/loja");
            _packagesDir = NormalizePath(packagesDir ?? "~/AURA/packages");
            _pluginsRoot = NormalizePath(pluginsRoot ?? "~/AURA/plugins");
            _getById = getById ?? ModuleCatalog.GetById;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) path = "~/AURA/loja";

            // expand ~ to user profile
            if (path.StartsWith("~"))
            {
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string rest = path.Length > 1 && (path[1] == '/' || path[1] == '\\') ? path.Substring(2) : path.Substring(1);
                path = string.IsNullOrEmpty(rest) ? userHome : Path.Combine(userHome, rest);
            }

            // expand environment variables
            path = Environment.ExpandEnvironmentVariables(path);

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        public IReadOnlyList<LojaEntry> ListAvailable()
        {
            if (!Directory.Exists(_lojaRoot))
            {
                return Array.Empty<LojaEntry>();
            }

            var dirs = Directory.GetDirectories(_lojaRoot);
            var list = new List<LojaEntry>();
            foreach (string dir in dirs)
            {
                string manifest = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifest))
                {
                    _logger.Warning($"Loja entry missing manifest: {dir}");
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(manifest);
                    var entry = JsonSerializer.Deserialize<LojaEntry>(json);
                    if (entry != null)
                    {
                        list.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Failed to read manifest {manifest}: {ex.Message}");
                }
            }

            return list;
        }

        public void InstallFromLoja(string id, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id required", nameof(id));

            string entryDir = Path.Combine(_lojaRoot, id);
            string manifestPath = Path.Combine(entryDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidOperationException($"No manifest for id '{id}' in loja.");
            }

            ModuleInfo? info = _getById(id);
            if (info == null)
            {
                throw new InvalidOperationException($"Module id '{id}' not found in ModuleCatalog.");
            }

            string manifestJson = File.ReadAllText(manifestPath);
            LojaEntry manifest = JsonSerializer.Deserialize<LojaEntry>(manifestJson) ?? throw new InvalidOperationException("Invalid manifest");
            if (!string.Equals(manifest.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Manifest id mismatch");
            }

            if (manifest.PayloadFiles == null || manifest.PayloadFiles.Count == 0)
            {
                throw new InvalidOperationException("Manifest has no payloadFiles");
            }

            foreach (string f in manifest.PayloadFiles)
            {
                if (!SafeName.IsMatch(f))
                {
                    throw new InvalidOperationException("Invalid payload file name: " + f);
                }
            }

            string packageDirForId = Path.Combine(_packagesDir, id);
            Directory.CreateDirectory(packageDirForId);

            string lockPath = Path.Combine(packageDirForId, ".install.lock");
            using (FileStream? lockFs = LockHelper.TryAcquireLock(lockPath, TimeSpan.FromSeconds(5)))
            {
                if (lockFs == null)
                {
                    throw new InvalidOperationException("Could not acquire install lock for id: " + id);
                }

                string payloadRoot = Path.Combine(entryDir, "payload");
                foreach (string f in manifest.PayloadFiles)
                {
                    string src = Path.Combine(payloadRoot, f);
                    if (!File.Exists(src))
                    {
                        throw new InvalidOperationException("Payload file missing: " + f);
                    }
                }

                Directory.CreateDirectory(_pluginsRoot);

                string tmpInstallDir = Path.Combine(_pluginsRoot, ".tmp_install_" + id + "_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tmpInstallDir);

                try
                {
                    var installed = new List<string>();
                    foreach (string f in manifest.PayloadFiles)
                    {
                        string src = Path.Combine(payloadRoot, f);
                        string tmpDest = Path.Combine(tmpInstallDir, f);
                        File.Copy(src, tmpDest, overwrite: true);
                    }

                    foreach (string f in manifest.PayloadFiles)
                    {
                        string tmpSrc = Path.Combine(tmpInstallDir, f);
                        string finalDest = Path.Combine(_pluginsRoot, f);

                        if (File.Exists(finalDest))
                        {
                            if (!overwrite)
                            {
                                throw new InvalidOperationException("Target file already exists: " + finalDest);
                            }
                            else
                            {
                                File.Delete(finalDest);
                            }
                        }

                        File.Move(tmpSrc, finalDest);
                        installed.Add(f);
                    }

                    string moduleJsonTmp = Path.Combine(packageDirForId, "module.json.tmp");
                    var moduleDoc = new
                    {
                        id = info.Id,
                        name = info.DisplayName,
                        version = info.PackageVersion,
                        description = info.ShortDescription,
                        features = info.Features,
                        pages = info.Includes
                    };

                    string moduleJson = JsonSerializer.Serialize(moduleDoc, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(moduleJsonTmp, moduleJson);
                    string moduleJsonFinal = Path.Combine(packageDirForId, "module.json");
                    if (File.Exists(moduleJsonFinal)) File.Delete(moduleJsonFinal);
                    File.Move(moduleJsonTmp, moduleJsonFinal);

                    string installedJsonTmp = Path.Combine(packageDirForId, "installedFiles.json.tmp");
                    string installedJson = JsonSerializer.Serialize(installed, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(installedJsonTmp, installedJson);
                    string installedJsonFinal = Path.Combine(packageDirForId, "installedFiles.json");
                    if (File.Exists(installedJsonFinal)) File.Delete(installedJsonFinal);
                    File.Move(installedJsonTmp, installedJsonFinal);

                    _logger.Info($"Installed module '{id}' with {installed.Count} files.");
                }
                catch
                {
                    try
                    {
                        if (Directory.Exists(tmpInstallDir)) Directory.Delete(tmpInstallDir, true);
                    }
                    catch { }

                    throw;
                }
                finally
                {
                    // release lock by disposing lockFs
                }
            }
        }
    }
}
