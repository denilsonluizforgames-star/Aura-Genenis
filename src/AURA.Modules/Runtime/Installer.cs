using System.Diagnostics;
using AURA.Abstractions.Runtime;

namespace AURA.Modules.Runtime;

/// <summary>
/// Monta e opcionalmente executa o plano de instalação. Por segurança o plano
/// é sempre construído primeiro e exibido; a execução só roda com confirmação.
/// Equivalente a <c>installer.py</c>.
/// </summary>
public sealed class Installer : IRuntimeInstaller
{
    public InstallPlan BuildPlan(RuntimeResolution? runtime, DependencyReport? deps)
    {
        var plan = new InstallPlan();

        // Runtime ausente → instalar primeiro (pip depende de python presente).
        if (runtime is not null && !runtime.Available && !string.IsNullOrWhiteSpace(runtime.InstallHint))
        {
            plan.Steps.Add(new InstallStep(
                What: $"runtime {runtime.Language}",
                Command: runtime.InstallHint,
                IsRuntime: true));
        }

        if (deps is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Dependency dep in deps.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dep.InstallCommand) || !seen.Add(dep.Name))
                {
                    continue;
                }
                plan.Steps.Add(new InstallStep(What: dep.Name, Command: dep.InstallCommand, IsRuntime: false));
            }
        }

        return plan;
    }

    public async Task<IReadOnlyList<string>> ExecuteAsync(
        InstallPlan plan,
        bool confirm,
        CancellationToken cancellationToken = default)
    {
        if (plan.Empty)
        {
            return new List<string> { "(nada a instalar)" };
        }

        if (confirm)
        {
            Console.WriteLine("Plano de instalação:");
            foreach (InstallStep step in plan.Steps)
            {
                Console.WriteLine($"  - {step.What}: {step.Command}");
            }
            Console.Write("Executar agora? [s/N] ");
            string? answer = Console.ReadLine();
            if (!IsAffirmative(answer))
            {
                return new List<string> { "(instalação cancelada pelo usuário)" };
            }
        }

        var results = new List<string>();
        foreach (InstallStep step in plan.Steps)
        {
            Console.WriteLine($">>> {step.Command}");
            results.Add($"{step.Command}: {await RunInstallCommandAsync(step.Command, cancellationToken)}");
        }

        return results;
    }

    // ------------------------------------------------------------------ //
    private static async Task<string> RunInstallCommandAsync(string command, CancellationToken cancellationToken)
    {
        // Instalação usa o shell (pkg install / pip install / npm install).
        string shell = BinaryPath.FindOnPath("bash") ?? BinaryPath.FindOnPath("sh")
            ?? throw new InvalidOperationException("Nenhum shell (bash/sh) encontrado no PATH.");
        var psi = new ProcessStartInfo
        {
            FileName = shell,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 ? "OK" : $"FALHOU (exit {process.ExitCode})";
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* já encerrado */ }
            return "CANCELADO (timeout ou interrupção)";
        }
        catch (Exception ex)
        {
            return $"FALHOU ({ex.Message})";
        }
    }

    private static bool IsAffirmative(string? answer)
    {
        answer = answer?.Trim().ToLowerInvariant();
        return answer is "s" or "sim" or "y" or "yes";
    }
}
