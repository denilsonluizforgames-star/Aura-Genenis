using AURA.Abstractions.Execution;

namespace AURA.Modules.Executors;

/// <summary>
/// Executor para Python. Tenta resolver "python3" primeiro (padrão no Termux),
/// caindo para "python" se necessário. request.Command é o script/módulo/flag
/// (ex: "script.py" ou "-c"), request.Arguments são os argumentos seguintes.
/// </summary>
public sealed class PythonExecutor : ProcessExecutorBase
{
    public override string Name => "python";

    public override bool IsAvailable() => ResolveBinary("python3", "python") is not null;

    public override Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (ResolveBinary("python3", "python") is not { } binary)
            return Task.FromResult(ExecutionResult.Failed("Python não encontrado (tentado: python3, python)."));

        var args = new List<string> { request.Command };
        args.AddRange(request.Arguments);

        return RunAsync(binary, args, request, cancellationToken);
    }
}
