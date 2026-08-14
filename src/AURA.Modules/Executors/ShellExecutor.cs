using AURA.Abstractions.Execution;

namespace AURA.Modules.Executors;

/// <summary>
/// Executor base: roda comandos diretamente via shell (sh -c).
/// </summary>
public sealed class ShellExecutor : ProcessExecutorBase
{
    public override string Name => "shell";

    public override bool IsAvailable() => File.Exists("/bin/sh") || File.Exists("/system/bin/sh");

    public override Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable())
            return Task.FromResult(ExecutionResult.Failed("Shell (/bin/sh) não encontrado no ambiente."));

        var fullCommand = request.Arguments.Count > 0
            ? $"{request.Command} {string.Join(' ', request.Arguments)}"
            : request.Command;

        return RunAsync("/bin/sh", new[] { "-c", fullCommand }, request, cancellationToken);
    }
}
