using System;
using System.IO;

namespace AURA.Core.Launchers
{
    /// <summary>
    /// Runs .NET assemblies (.dll) inside a cell via the "dotnet" host.
    /// </summary>
    public sealed class DllLauncher : ILauncher
    {
        private static readonly string[] Extensions = { ".dll" };

        public string[] SupportedExtensions => Extensions;

        public bool Supports(string filePath)
        {
            return !string.IsNullOrWhiteSpace(filePath) &&
                Array.IndexOf(Extensions, Path.GetExtension(filePath)) >= 0;
        }

        public CellCommand BuildCommand(string filePath, string arguments)
        {
            return new CellCommand("dotnet", "\"" + filePath + "\" " + arguments);
        }
    }
}
