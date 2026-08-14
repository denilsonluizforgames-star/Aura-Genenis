using AURA.Abstractions.Execution;

namespace AURA.Modules.Executors;

/// <summary>
/// Executor para Node.js. request.Command é o script (ex: "index.js") ou
/// flag (ex: "-e"), request.Arguments são os argumentos seguintes.
/// </summary>
public sealed class NodeExecutor : ProcessExecutorBase
{
    public override string Name => "node";

    public override bool IsAvailable() => ResolveBinary("node") is not null;

    public override Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (ResolveBinary("node") is not { } binary)
            return Task.FromResult(ExecutionResult.Failed("Node.js não encontrado no ambiente."));

        var args = new List<string> { request.Command };
        args.AddRange(request.Arguments);

        return RunAsync(binary, args, request, cancellationToken);
    }
}
