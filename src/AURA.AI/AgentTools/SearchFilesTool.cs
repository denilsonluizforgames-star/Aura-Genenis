using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AURA.Abstractions.Execution;

namespace AURA.AI
{
    /// <summary>
    /// Busca texto dentro do workspace (grep recursivo) para RAG local, sem
    /// filesystem paralelo. Como as outras file tools, respeita o sandbox de
    /// caminho (somente dentro do workspace); a execução do grep é delegada a
    /// um <see cref="IToolExecutor"/> (tipicamente <c>ShellExecutor</c>) —
    /// nenhum Process é criado aqui.
    /// </summary>
    public sealed class SearchFilesTool : WorkspaceAgentTool
    {
        private const int DefaultTimeoutSeconds = 30;
        private const int MaxOutputChars = 30000;
        private const int MaxResultLines = 200;

        private readonly IToolExecutor _executor;

        /// <param name="workspaceRoot">Raiz do workspace (sandbox de caminho).</param>
        /// <param name="executor">Executor de processo (tipicamente <c>ShellExecutor</c>). Obrigatório.</param>
        public SearchFilesTool(string workspaceRoot, IToolExecutor executor) : base(workspaceRoot)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public override AgentToolDefinition Definition => new AgentToolDefinition
        {
            Name = "search_files",
            Description = "Busca um texto/padrão dentro dos arquivos do workspace (grep recursivo, " +
                "case-insensitive). Retorna linhas no formato arquivo:linha:conteúdo. " +
                "Use para localizar onde um trecho de código, termo ou palavra aparece.",
            Parameters =
            {
                ["query"] = new AgentToolParameter
                {
                    Type = "string",
                    Description = "Texto literal a procurar (case-insensitive)."
                },
                ["path"] = new AgentToolParameter
                {
                    Type = "string",
                    Description = "Caminho relativo ao workspace para restringir a busca (opcional; padrão = raiz)."
                }
            },
            Required = { "query" }
        };

        public override async Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
        {
            string query;
            string path;
            using (JsonDocument doc = JsonDocument.Parse(argumentsJson))
            {
                JsonElement root = doc.RootElement;
                query = ReadString(root, "query") ?? string.Empty;
                path = ReadString(root, "path") ?? ".";
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return "ERRO: query vazia.";
            }

            string searchDir;
            try
            {
                searchDir = ResolvePath(path);
            }
            catch (Exception ex)
            {
                return "ERRO: " + ex.Message;
            }

            if (!Directory.Exists(searchDir))
            {
                return "ERRO: diretório não existe: " + path;
            }

            if (!_executor.IsAvailable())
            {
                return "ERRO: shell não encontrado neste dispositivo.";
            }

            string command =
                "grep -r -n -i -F -- " +
                ShellQuote(query) + " " +
                ShellQuote(searchDir) +
                " 2>/dev/null | head -n " + MaxResultLines;

            var request = new ExecutionRequest
            {
                Command = command,
                WorkingDirectory = WorkspaceRoot,
                Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds)
            };

            ExecutionResult result = await _executor.ExecuteAsync(request, ct).ConfigureAwait(false);
            return FormatForLlm(result);
        }

        /// <summary>
        /// Classifica pela semântica do grep: exit 1 = "nenhum resultado" é sucesso
        /// (resultado válido), apenas cancelamento/timeout é falha. Texto idêntico ao
        /// de <see cref="ExecuteAsync"/>.
        /// </summary>
        public override async Task<AgentToolResult> ExecuteStructuredAsync(
            string argumentsJson, CancellationToken ct = default)
        {
            string query;
            string path;
            using (JsonDocument doc = JsonDocument.Parse(argumentsJson))
            {
                JsonElement root = doc.RootElement;
                query = ReadString(root, "query") ?? string.Empty;
                path = ReadString(root, "path") ?? ".";
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return AgentToolResult.Error("ERRO: query vazia.");
            }

            string searchDir;
            try
            {
                searchDir = ResolvePath(path);
            }
            catch (Exception ex)
            {
                return AgentToolResult.Error("ERRO: " + ex.Message);
            }

            if (!Directory.Exists(searchDir))
            {
                return AgentToolResult.Error("ERRO: diretório não existe: " + path);
            }

            if (!_executor.IsAvailable())
            {
                return AgentToolResult.Error("ERRO: shell não encontrado neste dispositivo.");
            }

            string command =
                "grep -r -n -i -F -- " +
                ShellQuote(query) + " " +
                ShellQuote(searchDir) +
                " 2>/dev/null | head -n " + MaxResultLines;

            var request = new ExecutionRequest
            {
                Command = command,
                WorkingDirectory = WorkspaceRoot,
                Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds)
            };

            ExecutionResult result = await _executor.ExecuteAsync(request, ct).ConfigureAwait(false);
            bool cancelled = result != null &&
                !string.IsNullOrWhiteSpace(result.StandardError) &&
                result.StandardError.IndexOf("[AURA] Execução cancelada", StringComparison.Ordinal) >= 0;
            if (cancelled)
            {
                return AgentToolResult.Error(FormatForLlm(result));
            }

            return AgentToolResult.Ok(FormatForLlm(result));
        }

        /// <summary>
        /// Converte <see cref="ExecutionResult"/> no formato de string esperado
        /// pelo <see cref="AgentSession"/>. Trata o exit 1 do grep (nenhuma
        /// ocorrência) como resultado válido — não como erro.
        /// </summary>
        public string FormatForLlm(ExecutionResult result)
        {
            if (result == null)
            {
                return "ERRO: resultado de execução nulo.";
            }

            // Timeout / cancelamento do ProcessExecutorBase
            if (!string.IsNullOrWhiteSpace(result.StandardError) &&
                result.StandardError.IndexOf("[AURA] Execução cancelada", StringComparison.Ordinal) >= 0)
            {
                return "ERRO: busca cancelada (timeout de " + DefaultTimeoutSeconds + "s).";
            }

            // grep: exit 1 = nenhuma ocorrência (válido, não é erro). O pipe
            // "| head" mascara o exit do grep (pipeline = exit do head = 0),
            // então stdout vazio também é tratado como nenhum resultado.
            if (result.ExitCode == 1 ||
                (result.ExitCode == 0 && string.IsNullOrWhiteSpace(result.StandardOutput)))
            {
                return "exit=1\n(nenhum resultado encontrado para a busca.)";
            }

            var sb = new StringBuilder();
            sb.Append("exit=").Append(result.ExitCode).Append('\n');

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                sb.AppendLine(result.StandardOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                sb.Append("stderr: ").AppendLine(result.StandardError.TrimEnd());
            }

            string output = sb.ToString().TrimEnd();
            if (output.Length > MaxOutputChars)
            {
                output = output.Substring(0, MaxOutputChars) +
                         "\n... (saída truncada: " + sb.Length + " chars)";
            }

            return output;
        }

        private static string ShellQuote(string value)
        {
            return "'" + value.Replace("'", "'\\''") + "'";
        }
    }
}
