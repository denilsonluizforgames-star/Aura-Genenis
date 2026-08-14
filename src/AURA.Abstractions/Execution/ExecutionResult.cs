using System;
using System.Text;

namespace AURA.Abstractions.Execution
{
    /// <summary>
    /// Resultado padronizado de uma execução de IToolExecutor.
    /// </summary>
    public sealed class ExecutionResult
    {
        public bool Success { get; set; }

        public int ExitCode { get; set; }

        public string StandardOutput { get; set; } = string.Empty;

        public string StandardError { get; set; } = string.Empty;

        public TimeSpan Duration { get; set; }

        public static ExecutionResult Failed(string message)
        {
            return new ExecutionResult
            {
                Success = false,
                ExitCode = -1,
                StandardError = message
            };
        }

        public string CombineOutput()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(StandardOutput))
            {
                builder.AppendLine(StandardOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(StandardError))
            {
                builder.AppendLine(StandardError.TrimEnd());
            }

            return builder.ToString().TrimEnd();
        }
    }
}
