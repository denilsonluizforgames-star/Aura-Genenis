using System;
using System.IO;

namespace AURA.AI
{
    /// <summary>
    /// Base para ferramentas que operam em arquivos: garante que todo caminho
    /// resolvido permaneça dentro do workspace (evita escrita fora da pasta
    /// controlada pelo app).
    /// </summary>
    public abstract class WorkspaceAgentTool : AgentTool
    {
        protected WorkspaceAgentTool(string workspaceRoot)
        {
            WorkspaceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        }

        public string WorkspaceRoot { get; }

        protected string ResolvePath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new ArgumentException("Caminho vazio.");
            }

            string full = Path.GetFullPath(Path.Combine(WorkspaceRoot, raw.Replace('\\', '/')));
            if (!IsInsideWorkspace(full))
            {
                throw new InvalidOperationException("Caminho fora do workspace: " + raw);
            }

            return full;
        }

        protected bool IsInsideWorkspace(string full)
        {
            string rootPrefix = WorkspaceRoot.TrimEnd(Path.DirectorySeparatorChar) +
                                 Path.DirectorySeparatorChar;
            return string.Equals(full, WorkspaceRoot, StringComparison.OrdinalIgnoreCase) ||
                   full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }

        protected static string Truncate(string text, int max = 40000)
        {
            if (text is null)
            {
                return string.Empty;
            }

            if (text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max) + "\n... (truncado: " + text.Length + " chars)";
        }
    }
}
