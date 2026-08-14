using System;
using System.IO;

namespace AURA.Core.Runtime
{
    /// <summary>
    /// A cell is an isolated instance of an application. On Termux (no root)
    /// each cell is a directory under ~/AURA/cells/&lt;id&gt;/ plus its own
    /// OS process, which gives real crash isolation without kernel features.
    /// </summary>
    public sealed class Cell
    {
        public string Id { get; set; }

        public string AppPath { get; set; }

        public string Args { get; set; }

        public string WorkingDirectory { get; set; }

        public CellState State { get; set; } = CellState.Created;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? LastStartedUtc { get; set; }

        public int? ProcessId { get; set; }

        public int RestartCount { get; set; }

        public string TemplatePath { get; set; }

        public ResourceLimits Limits { get; set; }

        /// <summary>
        /// Root real da célula, definido pelo runtime no momento da criação
        /// (Path.Combine(cellsRoot, Id)). Mantém RootDirectory/LogFile corretos
        /// mesmo quando o runtime usa um root customizado (ex.: Android).
        /// Vazio = usa o default ~/AURA/cells (comportamento original).
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string CellRoot { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string RootDirectory => string.IsNullOrEmpty(CellRoot)
            ? Path.Combine(SimulationRuntime.ExpandUserHome(SimulationRuntime.DefaultCellsRoot), Id)
            : CellRoot;

        [System.Text.Json.Serialization.JsonIgnore]
        public string LogFile =>
            Path.Combine(RootDirectory, "cell.log");

        public override string ToString()
        {
            return Id + " [" + State + "] pid=" +
                (ProcessId.HasValue ? ProcessId.Value.ToString() : "-");
        }
    }
}
