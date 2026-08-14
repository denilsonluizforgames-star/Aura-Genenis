using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using AURA.Core.Logging;

namespace AURA.Modules.Loja
{
    /// <summary>
    /// Removes a locally installed package.
    /// Packages installed through LojaLocalResolver have installedFiles.json.
    /// Packages downloaded through the normal ModuleManager path may not have it;
    /// in that case only package metadata is removed.
    /// </summary>
    public sealed class LojaUninstaller
    {
        private static readonly Regex SafeName =
            new Regex("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

        private readonly ILogger _logger;
        private readonly string _packagesDir;
        private readonly string _pluginsRoot;

        public LojaUninstaller(
            ILogger logger,
            string packagesDir,
            string pluginsRoot)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _packagesDir = packagesDir ?? throw new ArgumentNullException(nameof(packagesDir));
            _pluginsRoot = pluginsRoot ?? throw new ArgumentNullException(nameof(pluginsRoot));
        }

        public void Uninstall(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id required", nameof(id));

            string packageDir = Path.Combine(_packagesDir, id);
            string installedJsonPath = Path.Combine(packageDir, "installedFiles.json");

            // Nothing exists: nothing to remove.
            if (!Directory.Exists(packageDir))
            {
                _logger.Info($"Package directory not found for {id}; nothing to uninstall.");
                return;
            }

            // installedFiles.json exists only for packages installed through
            // LojaLocalResolver. Normal ModuleManager downloads do not create it.
            if (File.Exists(installedJsonPath))
            {
                List<string> files;

                try
                {
                    string json = File.ReadAllText(installedJsonPath);

                    files = JsonSerializer.Deserialize<List<string>>(json)
                        ?? new List<string>();
                }
                catch (Exception ex)
                {
                    _logger.Warning(
                        $"Failed to read installedFiles.json for {id}: {ex.Message}");

                    throw new InvalidOperationException(
                        "Invalid installedFiles.json",
                        ex);
                }

                foreach (string f in files)
                {
                    if (!SafeName.IsMatch(f))
                    {
                        _logger.Warning(
                            $"Skipping unsafe installed file name when uninstalling {id}: {f}");

                        continue;
                    }

                    try
                    {
                        string target = Path.Combine(_pluginsRoot, f);

                        if (File.Exists(target))
                        {
                            File.Delete(target);

                            _logger.Info(
                                $"Deleted installed file: {target}");
                        }
                        else
                        {
                            _logger.Warning(
                                $"Installed file missing during uninstall: {target}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(
                            $"Failed to delete installed file {f} for {id}: {ex.Message}");
                    }
                }
            }
            else
            {
                // Normal ModuleManager.DownloadAsync packages do not own
                // plugin files, so there is nothing to delete from plugins/.
                _logger.Info(
                    $"No installedFiles.json for {id}; removing package metadata only.");
            }

            // Remove package metadata regardless of whether the package
            // came from LojaLocalResolver or ModuleManager.DownloadAsync.
            try
            {
                if (Directory.Exists(packageDir))
                {
                    Directory.Delete(packageDir, recursive: true);

                    _logger.Info(
                        $"Deleted package directory: {packageDir}");
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    $"Failed to delete package directory {packageDir}: {ex.Message}");
            }
        }
    }
}
