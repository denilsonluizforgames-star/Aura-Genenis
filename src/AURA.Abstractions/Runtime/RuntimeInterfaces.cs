using System.Threading;
using System.Threading.Tasks;

namespace AURA.Abstractions.Runtime;

/// <summary>
/// Detecta a linguagem de um arquivo (extensão, shebang, conteúdo, magic bytes).
/// Passo 1 do pipeline — equivalente a <c>detector.py</c>.
/// </summary>
public interface IRuntimeDetector
{
    Detection Detect(string filePath);
}

/// <summary>
/// Resolve o runtime necessário para uma linguagem (PATH + versão mínima).
/// Passo 2 do pipeline — equivalente a <c>resolver.py</c>.
/// </summary>
public interface IRuntimeResolver
{
    RuntimeResolution Resolve(string language);
}

/// <summary>
/// Analisa dependências de um arquivo (imports, manifestos, binários shell).
/// Passo 3 do pipeline — equivalente a <c>deps.py</c>.
/// </summary>
public interface IDependencyAnalyzer
{
    DependencyReport Analyze(string filePath, string language);
}

/// <summary>
/// Valida a sintaxe de um arquivo sem executá-lo.
/// Passo 4 do pipeline — equivalente a <c>syntax.py</c>.
/// </summary>
public interface ISyntaxValidator
{
    SyntaxResult Validate(string filePath, string language, string? binary = null);
}

/// <summary>
/// Verifica se o ambiente está pronto (runtime + deps + binários auxiliares).
/// Passo 5 do pipeline — equivalente a <c>compatibility.py</c>.
/// </summary>
public interface ICompatibilityChecker
{
    CompatReport Check(RuntimeResolution runtime, DependencyReport deps, bool checkNetwork = false);
}

/// <summary>
/// Monta e opcionalmente executa o plano de instalação.
/// Passo 6 do pipeline — equivalente a <c>installer.py</c>.
/// </summary>
public interface IRuntimeInstaller
{
    InstallPlan BuildPlan(RuntimeResolution? runtime, DependencyReport? deps);

    Task<IReadOnlyList<string>> ExecuteAsync(
        InstallPlan plan,
        bool confirm,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orquestra o pipeline completo (identifica → resolve → analisa → valida →
/// compatibilidade → instala → executa → gerencia). Passos 1-9 — equivalente
/// a <c>manager.py</c>.
/// </summary>
public interface IRuntimeManager
{
    Task<PipelineReport> RunAsync(
        string filePath,
        IReadOnlyList<string>? args = null,
        TimeSpan? timeout = null,
        bool autoInstall = false,
        string? workdir = null,
        CancellationToken cancellationToken = default);

    Task<PipelineReport> InspectAsync(string filePath, CancellationToken cancellationToken = default);
}
