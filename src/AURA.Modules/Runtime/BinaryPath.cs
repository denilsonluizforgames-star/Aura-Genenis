namespace AURA.Modules.Runtime;

/// <summary>
/// Procura binários no PATH. Espelha <c>ProcessExecutorBase.ResolveBinary</c>
/// (que é <c>protected</c>) sem precisar alterar a classe base existente.
/// </summary>
public static class BinaryPath
{
    public static string? FindOnPath(params string[] candidates)
    {
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] dirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (string candidate in candidates)
        {
            foreach (string dir in dirs)
            {
                if (File.Exists(Path.Combine(dir, candidate)))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static bool IsOnPath(string name) => FindOnPath(name) is not null;
}
