using System.IO;

namespace AURA.Mobile.Diagnostics
{
    /// <summary>
    /// Workspace onde o agente (estilo opencode) lê, escreve e edita arquivos
    /// no próprio dispositivo. Fica na pasta privada do app, sem permissão extra.
    /// </summary>
    public static class AgentWorkspace
    {
        public static string WorkspaceRoot => Path.Combine(FileSystem.AppDataDirectory, "workspace");

        /// <summary>
        /// Cópia local do projeto explicitamente vinculado pelo usuário.
        /// Se não houver projeto vinculado, cai no workspace privado original.
        /// </summary>
        public static string ActiveRoot => ProjectAccessService.IsLinked
            ? ProjectAccessService.ProjectWorkspaceRoot
            : WorkspaceRoot;

        public static string EnsureCreated()
        {
            Directory.CreateDirectory(WorkspaceRoot);
            return WorkspaceRoot;
        }

        public static int CountFiles(string? root = null)
        {
            root ??= WorkspaceRoot;
            if (!Directory.Exists(root))
            {
                return 0;
            }

            return Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length;
        }
    }
}
