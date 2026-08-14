using AURA.Abstractions.Execution;
using AURA.Abstractions.Runtime;
using AURA.Modules.Executors;

namespace AURA.Modules.Runtime;

/// <summary>
/// Executa o programa com o runtime resolvido, reutilizando a base
/// <see cref="ProcessExecutorBase"/> (timeout, captura de stdout/stderr,
/// sem shell). Equivalente a <c>executor.py</c>.
/// </summary>
public sealed class RuntimeProcessExecutor : ProcessExecutorBase
{
    private readonly RuntimeResolution _runtime;
    private readonly string _language;

    public RuntimeProcessExecutor(RuntimeResolution runtime)
    {
        _runtime = runtime;
        _language = runtime.Language;
    }

    public override string Name => "runtime-" + _language;

    public override bool IsAvailable() => _runtime.Available;

    public override Task<ExecutionResult> ExecuteAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(request.Command, request.Arguments, request, cancellationToken);
    }
}
