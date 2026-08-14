using System;
using System.IO;

namespace AURA.Core.Launchers
{
    /// <summary>
    /// A fully resolved command for a cell: the executable plus any extra
    /// arguments, ready to be passed to SimulationRuntime.CreateCell.
    /// </summary>
    public sealed class CellCommand
    {
        public CellCommand(string fileName, string arguments = null)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            Arguments = arguments ?? string.Empty;
        }

        public string FileName { get; }

        public string Arguments { get; }
    }
}
