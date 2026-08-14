using System;
using System.IO;

namespace AURA.Core.Launchers
{
    /// <summary>
    /// Runs Node.js files (.js, .mjs) inside a cell via the "node" runtime.
    /// Instalável no Termux com: pkg install nodejs
    /// </summary>
    public sealed class NodeLauncher : ILauncher
    {
        private static readonly string[] Extensions = { ".js", ".mjs" };

        public string[] SupportedExtensions => Extensions;

        public bool Supports(string filePath)
        {
            return !string.IsNullOrWhiteSpace(filePath) &&
                Array.IndexOf(Extensions, Path.GetExtension(filePath)) >= 0;
        }

        public CellCommand BuildCommand(string filePath, string arguments)
        {
            return new CellCommand("node", "\"" + filePath + "\" " + arguments);
        }
    }
}
