using System.Diagnostics;
using AURA.Abstractions.Runtime;

namespace AURA.Modules.Runtime;

/// <summary>
/// Valida a sintaxe de um arquivo com a ferramenta nativa da linguagem, sem
/// executar o programa. Roda ANTES da instalação (não instale nada para um
/// arquivo inválido). Equivalente a <c>syntax.py</c>.
/// </summary>
public sealed class SyntaxValidator : ISyntaxValidator
{
    public SyntaxResult Validate(string filePath, string language, string? binary = null)
    {
        (string tool, string[] args)? checker = CheckerFor(language, binary);
        if (checker is null)
        {
            return new SyntaxResult
            {
                Valid = true,
                Tool = "none",
                Detail = $"sem validador de sintaxe para '{language}'",
            };
        }

        var (toolName, prefix) = checker.Value;
        if (prefix.Length == 0)
        {
            // Ferramenta exigida mas ausente no PATH.
            return new SyntaxResult
            {
                Valid = false,
                Tool = toolName,
                Errors = { $"ferramenta '{toolName}' não disponível para validar sintaxe" },
            };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = prefix[0],
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            for (int i = 1; i < prefix.Length; i++) psi.ArgumentList.Add(prefix[i]);
            psi.ArgumentList.Add(filePath);

            using var process = new Process { StartInfo = psi };
            process.Start();
            if (!process.WaitForExit(15000)) process.Kill();

            if (process.ExitCode == 0)
            {
                return new SyntaxResult { Valid = true, Tool = toolName, Detail = "sintaxe OK" };
            }

            return new SyntaxResult
            {
                Valid = false,
                Tool = toolName,
                Errors = ParseErrors(process.StandardError.ReadToEnd() +
                                    process.StandardOutput.ReadToEnd()),
                Detail = "sintaxe inválida — corrija antes de executar",
            };
        }
        catch (Exception ex)
        {
            return new SyntaxResult
            {
                Valid = false,
                Tool = toolName,
                Errors = { ex.Message },
                Detail = "falha ao validar sintaxe",
            };
        }
    }

    // ------------------------------------------------------------------ //
    /// <summary>Retorna (nome_da_ferramenta, comando_pronto). Prefixo vazio = ferramenta ausente.</summary>
    private static (string, string[])? CheckerFor(string language, string? binary)
    {
        binary ??= FallbackBinary(language);
        if (binary is null) return null;

        switch (language)
        {
            case "python": return ("py_compile", new[] { binary, "-m", "py_compile" });
            case "shell": return ("bash -n", new[] { binary, "-n" });
            case "node": return ("node --check", new[] { binary, "--check" });
            case "java":
                return ("javac", OnPath("javac") ? new[] { "javac", "-proc:none" } : Array.Empty<string>());
            case "go":
                return ("gofmt", OnPath("gofmt") ? new[] { "gofmt", "-e" } : Array.Empty<string>());
            default:
                return null;
        }
    }

    private static string? FallbackBinary(string language)
    {
        string[] candidates = language switch
        {
            "python" => new[] { "python3", "python" },
            "shell" => new[] { "bash", "sh" },
            "node" => new[] { "node" },
            "java" => new[] { "java" },
            "go" => new[] { "go" },
            _ => Array.Empty<string>(),
        };

        return candidates.Select(c => BinaryPath.FindOnPath(c))
            .FirstOrDefault(path => path is not null);
    }

    private static bool OnPath(string name) => BinaryPath.IsOnPath(name);

    private static List<string> ParseErrors(string text)
    {
        var lines = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Take(10)
            .ToList();
        return lines.Count > 0 ? lines : new List<string> { "(sem detalhes do validador)" };
    }
}
