using System;
using System.IO;

namespace AURA.Core.Launchers
{
    /// <summary>
    /// Runs Python files (.py) inside a cell. Uses the "python" (or python3)
    /// interpreter found on PATH, which exists out-of-the-box on Termux.
    /// </summary>
    public sealed class PythonLauncher : ILauncher
    {
        private static readonly string[] Extensions = { ".py" };

        public string[] SupportedExtensions => Extensions;

        public bool Supports(string filePath)
        {
            return !string.IsNullOrWhiteSpace(filePath) &&
                Array.IndexOf(Extensions, Path.GetExtension(filePath)) >= 0;
        }

        public CellCommand BuildCommand(string filePath, string arguments)
        {
            string python = FindPython();
            return new CellCommand(python, "\"" + filePath + "\" " + arguments);
        }

        private static string FindPython()
        {
            string python3 = FindOnPath("python3");
            if (python3 != null)
            {
                return python3;
            }

            string python = FindOnPath("python");
            if (python != null)
            {
                return python;
            }

            throw new FileNotFoundException("Python não encontrado no PATH. Instale com: pkg install python");
        }

        private static string FindOnPath(string name)
        {
            string pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar))
            {
                return null;
            }

            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
