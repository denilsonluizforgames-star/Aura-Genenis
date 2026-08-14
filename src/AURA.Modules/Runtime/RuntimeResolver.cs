using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AURA.Abstractions.Runtime;

namespace AURA.Modules.Runtime;

/// <summary>
/// Resolve o runtime de uma linguagem: procura o binário no PATH, obtém a
/// versão e verifica a versão mínima exigida. Equivalente a <c>resolver.py</c>.
/// </summary>
public sealed class RuntimeResolver : IRuntimeResolver
{
    public RuntimeResolution Resolve(string language)
    {
        if (RuntimeCatalog.NonRuntimeLanguages.Contains(language))
        {
            return new RuntimeResolution
            {
                Language = language,
                Available = false,
                Detail = "linguagem sem runtime executável (dado/documento)",
            };
        }

        if (!RuntimeCatalog.Languages.TryGetValue(language, out RuntimeCatalog.LanguageDefinition? definition))
        {
            return new RuntimeResolution
            {
                Language = language,
                Available = false,
                Detail = $"runtime não catalogado para '{language}'",
            };
        }

        var result = new RuntimeResolution
        {
            Language = language,
            MinVersionRequired = definition.MinVersion.ToString(),
            InstallHint = definition.InstallHint,
        };

        // Procurar binário no PATH
        string? found = null;
        foreach (string candidate in definition.Candidates)
        {
            string? path = BinaryPath.FindOnPath(candidate);
            if (path is not null)
            {
                found = path;
                break;
            }
            result.Missing.Add(candidate);
        }

        if (found is null)
        {
            result.Available = false;
            result.Detail = $"Runtime '{language}' não encontrado no PATH. " +
                            $"Instale com: {definition.InstallHint}";
            return result;
        }

        result.Binary = found;
        result.Available = true;

        result.Version = FetchVersion(found, definition.VersionArgs);
        result.VersionSatisfied = Satisfies(
            result.Version, definition.MinVersion, new Regex(definition.VersionRegex));
        result.Detail = $"{found} {result.Version}";

        return result;
    }

    // ------------------------------------------------------------------ //
    private static string FetchVersion(string binary, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = binary,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string arg in args) psi.ArgumentList.Add(arg);

            using var process = new Process { StartInfo = psi };
            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.AppendLine(e.Data); };

            if (!process.Start()) return string.Empty;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(8000)) process.Kill();

            string text = output.ToString() + error.ToString();
            foreach (string line in text.Split('\n'))
            {
                if (Regex.IsMatch(line, @"\d+\.\d+")) return line.Trim();
            }
            return text.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool Satisfies(string versionText, Version minimum, Regex versionRegex)
    {
        Match match = versionRegex.Match(versionText ?? string.Empty);
        if (!match.Success || match.Groups.Count < 3)
        {
            return true; // não conseguiu ler a versão → não bloqueia
        }

        if (!int.TryParse(match.Groups[1].Value, out int major) ||
            !int.TryParse(match.Groups[2].Value, out int minor))
        {
            return true;
        }

        var actual = new Version(major, minor);
        return actual >= minimum;
    }
}
