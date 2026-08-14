using System.Threading;
using System.Threading.Tasks;

namespace AURA.Abstractions.Execution
{
    /// <summary>
    /// Contrato de um executor de ferramentas (shell, git, python, node, ...).
    /// Cada executor resolve o binário e monta os argumentos a partir de um
    /// ExecutionRequest, devolvendo sempre um ExecutionResult padronizado.
    /// </summary>
    public interface IToolExecutor
    {
        string Name { get; }

        /// <summary>Indica se o binário da ferramenta existe no ambiente.</summary>
        bool IsAvailable();

        Task<ExecutionResult> ExecuteAsync(
            ExecutionRequest request,
            CancellationToken cancellationToken = default);
    }
}
