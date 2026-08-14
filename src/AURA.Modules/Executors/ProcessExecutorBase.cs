using System.Diagnostics;
using System.Text;
using AURA.Abstractions.Execution;

namespace AURA.Modules.Executors;

/// <summary>
/// Base compartilhada por todos os executores que rodam um processo externo
/// (Shell, Git, Dotnet, Python, Node, Cargo, Java). Centraliza a lógica de
/// disparo do processo, captura de stdout/stderr e timeout, para que cada
/// executor concreto só precise resolver o binário e montar os argumentos.
/// </summary>
public abstract class ProcessExecutorBase : IToolExecutor
{
    public abstract string Name { get; }
    public abstract bool IsAvailable();
    public abstract Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Executa um processo já resolvido (fileName + argumentos) e devolve o resultado padronizado.</summary>
    protected static async Task<ExecutionResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = request.WorkingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        if (request.EnvironmentVariables is not null)
        {
            foreach (var (key, value) in request.EnvironmentVariables)
                psi.Environment[key] = value;
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = request.Timeout is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;
            cts?.CancelAfter(request.Timeout!.Value);

            await process.WaitForExitAsync(cts?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* processo já encerrado */ }
            stopwatch.Stop();
            return new ExecutionResult
            {
                Success = false,
                ExitCode = -1,
                StandardOutput = stdout.ToString(),
                StandardError = stderr + "\n[AURA] Execução cancelada por timeout.",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ExecutionResult.Failed($"[AURA] Falha ao iniciar '{fileName}': {ex.Message}");
        }

        stopwatch.Stop();

        return new ExecutionResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            Duration = stopwatch.Elapsed
        };
    }

    /// <summary>Procura o primeiro binário disponível dentre os candidatos, olhando o PATH.</summary>
    protected static string? ResolveBinary(params string[] candidates)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var dirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var candidate in candidates)
        {
            foreach (var dir in dirs)
            {
                if (File.Exists(Path.Combine(dir, candidate)))
                    return candidate;
            }
        }

        return null;
    }
}
