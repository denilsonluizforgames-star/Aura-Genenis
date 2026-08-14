using System;
using System.IO;

namespace AURA.Core.Launchers
{
    /// <summary>
    /// Runs Java archive files (.jar) inside a cell via "java -jar".
    /// Termux provides openjdk-17 (headless JRE).
    /// </summary>
    public sealed class JarLauncher : ILauncher
    {
        private static readonly string[] Extensions = { ".jar" };

        public string[] SupportedExtensions => Extensions;

        public bool Supports(string filePath)
        {
            return !string.IsNullOrWhiteSpace(filePath) &&
                Array.IndexOf(Extensions, Path.GetExtension(filePath)) >= 0;
        }

        public CellCommand BuildCommand(string filePath, string arguments)
        {
            return new CellCommand("java", "-jar \"" + filePath + "\" " + arguments);
        }
    }
}
