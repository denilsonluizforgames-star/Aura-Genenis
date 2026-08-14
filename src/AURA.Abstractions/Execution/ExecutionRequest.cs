using System;
using System.Collections.Generic;

namespace AURA.Abstractions.Execution
{
    /// <summary>
    /// Descreve um comando a ser executado por um IToolExecutor. O significado
    /// de <see cref="Command"/> e <see cref="Arguments"/> varia por executor
    /// (ex.: no Git, Command é o subcomando "status" e Arguments são os
    /// argumentos dele; no Shell, Command é o comando completo).
    /// </summary>
    public sealed class ExecutionRequest
    {
        public string Command { get; set; } = string.Empty;

        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>Diretório de trabalho do processo. Null = diretório atual.</summary>
        public string WorkingDirectory { get; set; }

        /// <summary>Variáveis de ambiente adicionais aplicadas ao processo.</summary>
        public IDictionary<string, string>? EnvironmentVariables { get; set; }

        /// <summary>Timeout da execução. Null = sem timeout.</summary>
        public TimeSpan? Timeout { get; set; }
    }
}
