using System.Diagnostics;
using AURA.Abstractions.Runtime;

namespace AURA.Modules.Runtime;

/// <summary>
/// Verifica ANTES de executar se o ambiente está pronto: runtime presente e
/// com versão satisfeita, dependências resolvíveis e binários auxiliares
/// presentes. Equivalente a <c>compatibility.py</c>.
/// </summary>
public sealed class CompatibilityChecker : ICompatibilityChecker
{
    public CompatReport Check(RuntimeResolution runtime, DependencyReport deps, bool checkNetwork = false)
    {
        var report = new CompatReport();

        // 1) Runtime
        if (runtime.Available && runtime.VersionSatisfied)
        {
            report.RuntimeOk = true;
            report.Messages.Add($"Runtime '{runtime.Language}' OK: {runtime.Binary} {runtime.Version}");
        }
        else
        {
            report.RuntimeOk = false;
            report.Messages.Add(!runtime.Available
                ? $"Runtime '{runtime.Language}' ausente. {runtime.Detail}"
                : $"Versão {runtime.Version} não satisfaz mínima {runtime.MinVersionRequired}");
        }

        // 2) Dependências
        report.DependenciesOk = true;
        foreach (Dependency dep in deps.Dependencies)
        {
            if (dep.Kind == "manifest")
            {
                report.Messages.Add($"Manifesto encontrado: {dep.Name} → {dep.InstallCommand}");
                continue;
            }

            bool ok = DependencyAvailable(dep, checkNetwork);
            if (ok)
            {
                report.Messages.Add($"Dependência OK: {dep.Name}");
            }
            else
            {
                report.DependenciesOk = false;
                report.Messages.Add($"Dependência faltando: {dep.Name} → {dep.InstallCommand}");
            }
        }

        return report;
    }

    // ------------------------------------------------------------------ //
    private static bool DependencyAvailable(Dependency dep, bool checkNetwork)
    {
        if (dep.Kind == "binary")
        {
            return BinaryPath.FindOnPath(dep.Name) is not null;
        }

        // import: heurística local — pacote já instalado?
        if (IsPythonPackageInstalled(dep.Name))
        {
            return true;
        }

        // Sem rede: não bloqueia — avisa apenas (o instalador decide).
        return !checkNetwork;
    }

    private static bool IsPythonPackageInstalled(string package)
    {
        string? python = BinaryPath.FindOnPath("python3", "python");
        if (python is null) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = python,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(
                "import importlib.util,sys;sys.exit(0 if importlib.util.find_spec(sys.argv[1]) else 1)");
            psi.ArgumentList.Add(package);

            using var process = new Process { StartInfo = psi };
            process.Start();
            return process.WaitForExit(10000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
