using AURA.Abstractions.Execution;

namespace AURA.Modules.Executors;

/// <summary>
/// Executor para o Git. request.Command é o subcomando (ex: "status", "diff",
/// "commit"), e request.Arguments são os argumentos do subcomando
/// (ex: ["-m", "mensagem"]). Rodar via argumentos separados (sem shell)
/// evita problemas de escaping em mensagens de commit com aspas/acentos.
/// </summary>
public sealed class GitExecutor : ProcessExecutorBase
{
    public override string Name => "git";

    public override bool IsAvailable() => ResolveBinary("git") is not null;

    public override Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (ResolveBinary("git") is not { } binary)
            return Task.FromResult(ExecutionResult.Failed("git não encontrado no ambiente."));

        var args = new List<string> { request.Command };
        args.AddRange(request.Arguments);

        return RunAsync(binary, args, request, cancellationToken);
    }

    // Métodos de conveniência (CreateBranchAsync, CommitAsync, DiffAsync, etc.)
    // ficam para quando o pipeline SelfDev estiver mais próximo de ser implementado.
    // Por enquanto, ExecuteAsync genérico cobre qualquer subcomando git.
}
