using System;
using System.IO;

namespace AURA.Core.Launchers
{
    /// <summary>
    /// Runs Go source files (.go) inside a cell via "go run".
    /// Instalável no Termux com: pkg install golang
    /// </summary>
    public sealed class GoLauncher : ILauncher
    {
        private static readonly string[] Extensions = { ".go" };

        public string[] SupportedExtensions => Extensions;

        public bool Supports(string filePath)
        {
            return !string.IsNullOrWhiteSpace(filePath) &&
                Array.IndexOf(Extensions, Path.GetExtension(filePath)) >= 0;
        }

        public CellCommand BuildCommand(string filePath, string arguments)
        {
            return new CellCommand("go", "run \"" + filePath + "\" " + arguments);
        }
    }
}
