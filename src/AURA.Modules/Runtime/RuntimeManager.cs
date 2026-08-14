using AURA.Abstractions.Execution;
using AURA.Abstractions.Runtime;

namespace AURA.Modules.Runtime;

/// <summary>
/// Orquestra o pipeline completo do Runtime/Installer Inteligente:
/// identifica → resolve runtime → analisa deps → valida sintaxe → checa
/// compatibilidade → instala (se autorizado) → executa → gerencia resultado.
/// Equivalente a <c>manager.py</c>.
/// </summary>
public sealed class RuntimeManager : IRuntimeManager
{
    private readonly IRuntimeDetector _detector;
    private readonly IRuntimeResolver _resolver;
    private readonly IDependencyAnalyzer _analyzer;
    private readonly ISyntaxValidator _syntaxValidator;
    private readonly ICompatibilityChecker _compatChecker;
    private readonly IRuntimeInstaller _installer;

    public RuntimeManager(
        IRuntimeDetector? detector = null,
        IRuntimeResolver? resolver = null,
        IDependencyAnalyzer? analyzer = null,
        ISyntaxValidator? syntaxValidator = null,
        ICompatibilityChecker? compatChecker = null,
        IRuntimeInstaller? installer = null)
    {
        _detector = detector ?? new LanguageDetector();
        _resolver = resolver ?? new RuntimeResolver();
        _analyzer = analyzer ?? new DependencyAnalyzer();
        _syntaxValidator = syntaxValidator ?? new SyntaxValidator();
        _compatChecker = compatChecker ?? new CompatibilityChecker();
        _installer = installer ?? new Installer();
    }

    public async Task<PipelineReport> RunAsync(
        string filePath,
        IReadOnlyList<string>? args = null,
        TimeSpan? timeout = null,
        bool autoInstall = false,
        string? workdir = null,
        CancellationToken cancellationToken = default)
    {
        var report = new PipelineReport { File = filePath };

        // [1] Identificação
        report.Detection = _detector.Detect(filePath);
        report.Steps.Add("identificacao");
        if (!report.Detection.Known)
        {
            report.Ok = false;
            report.Log($"Não foi possível identificar a linguagem de '{filePath}'.");
            report.Finish();
            return report;
        }
        report.Log($"Detectado: {report.Detection.Language} " +
                   $"(confiança {report.Detection.Confidence:P0}, via {report.Detection.DetectedBy})");

        string language = report.Detection.Language;

        // Linguagens sem runtime executável (dados/documentos)
        if (RuntimeCatalog.NonRuntimeLanguages.Contains(language))
        {
            report.Ok = false;
            report.Log($"'{language}' não é um programa executável.");
            report.Finish();
            return report;
        }

        // [2] Resolução do runtime
        report.Runtime = _resolver.Resolve(language);
        report.Steps.Add("runtime");
        report.Log($"Runtime: {FirstNonEmpty(report.Runtime.Detail, report.Runtime.InstallHint, "—")}");

        // [3] Análise de dependências
        report.Deps = _analyzer.Analyze(filePath, language);
        report.Steps.Add("dependencias");
        if (report.Deps.Packages.Count > 0)
        {
            report.Log("Dependências: " + string.Join(", ", report.Deps.Packages.Select(d => d.Name)));
        }
        if (report.Deps.Runtimes.Count > 0)
        {
            report.Log("Runtimes exigidos: " + string.Join(", ", report.Deps.Runtimes.Select(d => d.Name)));
        }

        // [4] Validação de sintaxe (ANTES de instalar qualquer coisa)
        report.Syntax = _syntaxValidator.Validate(filePath, language, report.Runtime.Binary);
        report.Steps.Add("sintaxe");
        if (!report.Syntax.Valid)
        {
            report.Ok = false;
            report.Log($"Sintaxe inválida ({report.Syntax.Tool}):");
            foreach (string err in report.Syntax.Errors.Take(5))
            {
                report.Log($"  - {err}");
            }
            report.Finish();
            return report;
        }
        report.Log($"Sintaxe OK ({report.Syntax.Tool})");

        // [5] Compatibilidade
        report.Compat = _compatChecker.Check(report.Runtime, report.Deps);
        report.Steps.Add("compatibilidade");
        foreach (string msg in report.Compat.Messages)
        {
            report.Log("Compat: " + msg);
        }

        // [6] Plano de instalação (executa só se autorizado)
        report.Plan = _installer.BuildPlan(report.Runtime, report.Deps);
        report.Steps.Add("instalacao");
        if (report.Plan.Empty)
        {
            report.Log("Nada a instalar.");
        }
        else if (autoInstall)
        {
            report.Installed = true;
            IReadOnlyList<string> results = await _installer.ExecuteAsync(
                report.Plan, confirm: false, cancellationToken);
            report.Log("Instalação: " + string.Join("; ", results));
            // Re-resolver runtime após instalar
            report.Runtime = _resolver.Resolve(language);
            report.Compat = _compatChecker.Check(report.Runtime, report.Deps);
        }
        else
        {
            report.Log("Instalação pendente (use autoInstall=true para aplicar):");
            foreach (InstallStep step in report.Plan.Steps)
            {
                report.Log($"  - {step.What}: {step.Command}");
            }
        }

        // [7/8] Execução
        report.Outcome = await ExecuteAsync(report, args, timeout, workdir, cancellationToken);
        report.Executed = true;
        report.Steps.Add("execucao");
        report.Log($"Execução: {(report.Outcome.Success ? "OK" : "FALHOU")} " +
                   $"(exit {report.Outcome.ExitCode}, {report.Outcome.DurationSeconds:F2}s)");

        report.Ok = report.Outcome.Success;
        report.Finish();
        return report;
    }

    public Task<PipelineReport> InspectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var report = new PipelineReport { File = filePath };

        report.Detection = _detector.Detect(filePath);
        report.Steps.Add("identificacao");
        if (!report.Detection.Known)
        {
            report.Finish();
            return Task.FromResult(report);
        }

        string language = report.Detection.Language;
        report.Runtime = _resolver.Resolve(language);
        report.Deps = _analyzer.Analyze(filePath, language);
        report.Syntax = _syntaxValidator.Validate(filePath, language, report.Runtime.Binary);
        report.Compat = _compatChecker.Check(report.Runtime, report.Deps);
        report.Plan = _installer.BuildPlan(report.Runtime, report.Deps);
        report.Steps.Add("inspecao");
        report.Ok = report.Compat.Ok && report.Syntax.Valid;
        report.Finish();
        return Task.FromResult(report);
    }

    // ------------------------------------------------------------------ //
    private static async Task<ExecutionOutcome> ExecuteAsync(
        PipelineReport report,
        IReadOnlyList<string>? args,
        TimeSpan? timeout,
        string? workdir,
        CancellationToken cancellationToken)
    {
        RuntimeResolution runtime = report.Runtime!;
        if (!runtime.Available)
        {
            return new ExecutionOutcome
            {
                Success = false,
                StandardError = $"Runtime '{runtime.Language}' indisponível. {runtime.Detail}",
            };
        }

        var executor = new RuntimeProcessExecutor(runtime);
        if (!executor.IsAvailable())
        {
            return new ExecutionOutcome
            {
                Success = false,
                StandardError = $"Binário '{runtime.Binary}' não encontrado no PATH.",
            };
        }

        (string fileName, List<string> commandArgs) = BuildCommand(runtime, report.File, args);

        var request = new ExecutionRequest
        {
            Command = fileName,
            Arguments = commandArgs,
            WorkingDirectory = workdir ?? Directory.GetCurrentDirectory(),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };

        ExecutionResult result = await executor.ExecuteAsync(request, cancellationToken);
        bool timedOut = !result.Success &&
                        result.ExitCode == -1 &&
                        result.StandardError.Contains("[AURA] Execução cancelada por timeout.");

        return ExecutionOutcome.From(result, timedOut, string.Join(' ', commandArgs.Prepend(fileName)));
    }

    /// <summary>Monta (binário, argumentos) por linguagem — sem shell.</summary>
    private static (string, List<string>) BuildCommand(
        RuntimeResolution runtime, string filePath, IReadOnlyList<string>? args)
    {
        var commandArgs = new List<string>();
        string binary = runtime.Binary ?? runtime.Language;

        switch (runtime.Language)
        {
            case "java":
                commandArgs.Add("-jar");
                break;
            case "go":
                commandArgs.Add("run");
                break;
        }

        commandArgs.Add(filePath);
        if (args is not null) commandArgs.AddRange(args);

        return (binary, commandArgs);
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
