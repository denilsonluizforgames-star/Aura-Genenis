using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AURA.Core.Runtime;

namespace AURA.Core.Launchers
{
    /// <summary>
    /// The "AURA decides how to run" layer: given a file the user picked, the
    /// runner resolves which launcher handles it and starts it inside a cell.
    /// The user never deals with interpreters or runtimes directly.
    /// </summary>
    public sealed class Runner
    {
        private readonly IReadOnlyList<ILauncher> _launchers;

        public Runner()
            : this(new ILauncher[]
            {
                new PythonLauncher(),
                new JarLauncher(),
                new DllLauncher(),
                new NodeLauncher(),
                new GoLauncher()
            })
        {
        }

        public Runner(IEnumerable<ILauncher> launchers)
        {
            _launchers = (launchers ?? Array.Empty<ILauncher>()).ToList();
        }

        public IReadOnlyList<ILauncher> Launchers => _launchers;

        public bool CanRun(string filePath)
        {
            return ResolveLauncher(filePath) != null;
        }

        public ILauncher ResolveLauncher(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            foreach (ILauncher launcher in _launchers)
            {
                if (launcher.Supports(filePath))
                {
                    return launcher;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the right launcher for <paramref name="filePath"/> and
        /// starts it inside a brand-new cell owned by the runtime.
        /// </summary>
        public async System.Threading.Tasks.Task<Cell> RunAsync(SimulationRuntime runtime, string id, string filePath,
            string arguments = null, string templatePath = null, ResourceLimits? limits = null)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            ILauncher launcher = ResolveLauncher(filePath);
            if (launcher == null)
            {
                throw new NotSupportedException(
                    "Nenhum launcher registrado para '" + filePath + "'. " +
                    "Extensões suportadas: " + string.Join(", ", SupportedExtensions()));
            }

            CellCommand command = launcher.BuildCommand(filePath, arguments);

            if (string.IsNullOrWhiteSpace(id))
            {
                id = Path.GetFileNameWithoutExtension(filePath) + "-" +
                    Guid.NewGuid().ToString("N").Substring(0, 6);
            }

            Cell cell = runtime.CreateCell(id, command.FileName, command.Arguments,
                templatePath, Path.GetDirectoryName(filePath), limits);

            await runtime.StartCellAsync(cell.Id);

            return cell;
        }

        private IEnumerable<string> SupportedExtensions()
        {
            return _launchers.SelectMany(l => l.SupportedExtensions).Distinct().OrderBy(e => e);
        }
    }
}
