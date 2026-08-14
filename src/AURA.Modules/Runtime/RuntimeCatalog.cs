using System.Collections.Generic;

namespace AURA.Modules.Runtime;

/// <summary>
/// Registro central de linguagens suportadas e seus runtimes. Equivalente a
/// <c>RUNTIME_DEFS</c>/<c>NON_RUNTIME_LANGUAGES</c> do protótipo Python.
/// Cada entrada define: binários candidatos, como obter a versão (regex),
/// versão mínima exigida e o comando de instalação sugerido.
/// </summary>
public static class RuntimeCatalog
{
    public sealed record LanguageDefinition(
        string[] Candidates,
        string[] VersionArgs,
        string VersionRegex,
        string InstallHint,
        Version MinVersion);

    public static readonly IReadOnlyDictionary<string, LanguageDefinition> Languages =
        new Dictionary<string, LanguageDefinition>
        {
            ["python"] = new(new[] { "python3", "python" }, new[] { "--version" },
                @"Python\s+(\d+)\.(\d+)", "pkg install python", new Version(3, 8)),
            ["shell"] = new(new[] { "bash", "sh" }, new[] { "--version" },
                @"version\s+(\d+)\.(\d+)", "pkg install bash", new Version(4, 0)),
            ["java"] = new(new[] { "java" }, new[] { "-version" },
                @"version\s+""(\d+)\.(\d+)", "pkg install openjdk-17", new Version(8, 0)),
            ["dotnet"] = new(new[] { "dotnet" }, new[] { "--version" },
                @"(\d+)\.(\d+)", "pkg install dotnet-10.0", new Version(6, 0)),
            ["node"] = new(new[] { "node" }, new[] { "--version" },
                @"v?(\d+)\.(\d+)", "pkg install nodejs", new Version(12, 0)),
            ["go"] = new(new[] { "go" }, new[] { "version" },
                @"go(\d+)\.(\d+)", "pkg install golang", new Version(1, 16)),
            ["ruby"] = new(new[] { "ruby" }, new[] { "--version" },
                @"ruby\s+(\d+)\.(\d+)", "pkg install ruby", new Version(2, 6)),
            ["php"] = new(new[] { "php" }, new[] { "--version" },
                @"PHP\s+(\d+)\.(\d+)", "pkg install php", new Version(7, 4)),
            ["lua"] = new(new[] { "lua" }, new[] { "-v" },
                @"Lua\s+(\d+)\.(\d+)", "pkg install lua54", new Version(5, 1)),
        };

    /// <summary>Linguagens de dados/documentos que não têm runtime executável.</summary>
    public static readonly HashSet<string> NonRuntimeLanguages = new()
    {
        "json", "markdown", "text", "zip", "pe", "class",
    };

    /// <summary>Extensões por linguagem (ponto 1 do detector).</summary>
    public static readonly IReadOnlyDictionary<string, string> Extensions =
        new Dictionary<string, string>
        {
            [".py"] = "python", [".pyw"] = "python", [".sh"] = "shell",
            [".bash"] = "shell", [".zsh"] = "shell", [".ksh"] = "shell",
            [".jar"] = "java", [".java"] = "java", [".class"] = "java",
            [".dll"] = "dotnet", [".exe"] = "dotnet", [".cs"] = "dotnet",
            [".csproj"] = "dotnet", [".sln"] = "dotnet",
            [".js"] = "node", [".mjs"] = "node", [".cjs"] = "node", [".ts"] = "node",
            [".go"] = "go", [".rb"] = "ruby", [".php"] = "php", [".lua"] = "lua",
            [".r"] = "r", [".json"] = "json", [".md"] = "markdown", [".txt"] = "text",
        };
}
