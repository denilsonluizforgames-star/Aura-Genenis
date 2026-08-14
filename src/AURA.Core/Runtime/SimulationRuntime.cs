using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AURA.Core.Logging;

namespace AURA.Core.Runtime
{
    /// <summary>
    /// The cell runtime (formerly "SimulationRuntime"). Each cell is backed
    /// by its own OS process, so a crash in one cell never takes down AURA or
    /// its neighbours. On Termux (no root) pause/resume is implemented with
    /// SIGSTOP/SIGCONT, which works without privileges.
    /// </summary>
    public sealed class SimulationRuntime : IDisposable
    {
        public const string DefaultCellsRoot = "~/AURA/cells";

        private const int SignalStop = 19;
        private const int SignalContinue = 18;

        private const int MaxRestartAttempts = 5;

        private readonly ConcurrentDictionary<string, Cell> _cells =
            new ConcurrentDictionary<string, Cell>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, Process> _processes =
            new ConcurrentDictionary<string, Process>(StringComparer.OrdinalIgnoreCase);

        private readonly ILogger _logger;
        private readonly ICellBackend _backend;
        private readonly string _cellsRoot;
        private readonly CellStore _store;
        private readonly bool _persist;
        private readonly object _logLock = new object();
        private readonly ConcurrentDictionary<string, StreamWriter> _logWriters =
            new ConcurrentDictionary<string, StreamWriter>(StringComparer.OrdinalIgnoreCase);

        public SimulationRuntime(ILogger logger)
            : this(logger, DefaultCellsRoot, new DirectoryCellBackend(), persist: true)
        {
        }

        public SimulationRuntime(ILogger logger, string cellsRoot, ICellBackend backend)
            : this(logger, cellsRoot, backend, persist: true)
        {
        }

        public SimulationRuntime(ILogger logger, string cellsRoot, ICellBackend backend, bool persist)
        {
            _logger = logger ?? new ConsoleLogger();
            _cellsRoot = ExpandHome(cellsRoot);
            _backend = backend ?? new DirectoryCellBackend();
            _persist = persist;
            Directory.CreateDirectory(_cellsRoot);

            _store = persist ? new CellStore(_logger, GetStorePath(_cellsRoot)) : null;
        }

        /// <summary>
        /// Caminho do índice de células. Preserva ~/AURA/cells.json quando o
        /// root é o default (comportamento histórico do Termux); para roots
        /// customizados (ex.: Android) grava dentro do próprio root.
        /// </summary>
        private static string GetStorePath(string cellsRoot)
        {
            if (cellsRoot == ExpandUserHome(DefaultCellsRoot))
            {
                return ExpandUserHome("~/AURA/cells.json");
            }

            return System.IO.Path.Combine(cellsRoot, "cells.json");
        }

        public string CellsRoot => _cellsRoot;

        public ICellBackend Backend => _backend;

        /// <summary>
        /// EventBus opcional. Quando definido, o runtime publica
        /// CellStateChangedEvent a cada transição de estado de célula.
        /// </summary>
        public AURA.Core.Events.EventBus Events { get; set; }

        public IReadOnlyCollection<Cell> Cells => _cells.Values.ToArray();

        public Cell GetCell(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("O id da célula não pode ser vazio.", nameof(id));
            }

            Cell cell;
            return _cells.TryGetValue(id, out cell) ? cell : null;
        }

        public void SetCellLimits(string id, ResourceLimits limits)
        {
            Cell cell = RequireCell(id);
            cell.Limits = limits ?? new ResourceLimits();
            Persist();
            _logger.Info("Limites definidos em '" + id + "': " +
                (cell.Limits.IsEmpty ? "(nenhum)" : DescribeLimits(cell.Limits)));
        }

        private static string DescribeLimits(ResourceLimits l)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (l.MemoryLimitMb.HasValue) parts.Add("mem=" + l.MemoryLimitMb.Value + "M");
            if (l.CpuLimitSeconds.HasValue) parts.Add("cpu=" + l.CpuLimitSeconds.Value + "s");
            if (l.MaxFiles.HasValue) parts.Add("arquivos=" + l.MaxFiles.Value);
            if (l.MaxProcesses.HasValue) parts.Add("procs=" + l.MaxProcesses.Value);
            return string.Join(", ", parts);
        }

        public Cell CreateCell(string id, string appPath, string args = null,
            string templatePath = null, string workingDirectory = null, ResourceLimits? limits = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("O id da célula não pode ser vazio.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(appPath))
            {
                throw new ArgumentException("O caminho do aplicativo não pode ser vazio.", nameof(appPath));
            }

            if (_cells.ContainsKey(id))
            {
                throw new InvalidOperationException("Já existe uma célula com id '" + id + "'.");
            }

            var cell = new Cell
            {
                Id = id,
                AppPath = appPath,
                Args = args ?? string.Empty,
                TemplatePath = templatePath,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(appPath),
                Limits = limits,
                CellRoot = Path.Combine(_cellsRoot, id)
            };

            _backend.Create(cell);
            _cells[id] = cell;
            _logger.Info("Célula criada: " + cell.ToString());

            PublishCellState(cell, string.Empty);
            Persist();

            return cell;
        }

        public async Task<Cell> StartCellAsync(string id, bool recycleOnCrash = true)
        {
            Cell cell = RequireCell(id);
            if (cell.State == CellState.Running)
            {
                return cell;
            }

            string previous = cell.State.ToString();
            cell.State = CellState.Running;
            cell.LastStartedUtc = DateTime.UtcNow;
            cell.RestartCount++;
            cell.ProcessId = null;

            Process process = BuildProcess(cell);
            _processes[id] = process;

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                _processes.TryRemove(id, out _);
                cell.State = CellState.Crashed;
                cell.ProcessId = null;
                _logger.Error("Falha ao iniciar célula '" + id + "': " + ex.Message);
                PublishCellState(cell, previous);
                throw;
            }

            cell.ProcessId = process.Id;
            _logger.Info("Célula iniciada: " + cell.ToString());

            PublishCellState(cell, previous);
            Persist();
            _ = WatchCellAsync(cell, recycleOnCrash);

            return cell;
        }

        public void StopCell(string id)
        {
            Cell cell = RequireCell(id);
            Process process = GetRunningProcess(cell);

            if (process == null)
            {
                cell.State = CellState.Stopped;
                PublishCellState(cell, CellState.Crashed.ToString());
                return;
            }

            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
            catch (InvalidOperationException)
            {
                // Processo já saiu.
            }
            catch (Exception ex)
            {
                _logger.Warning("Falha ao encerrar célula '" + id + "': " + ex.Message);
            }

            cell.State = CellState.Stopped;
            cell.ProcessId = null;
            _processes.TryRemove(id, out _);
            _logger.Info("Célula parada: " + id);

            PublishCellState(cell, CellState.Running.ToString());
            Persist();
        }

        public void PauseCell(string id)
        {
            Cell cell = RequireCell(id);
            Process process = GetRunningProcess(cell);

            if (process == null)
            {
                _logger.Warning("Célula '" + id + "' não está rodando; não pode pausar.");
                return;
            }

            if (!OperatingSystem.IsWindows())
            {
                SendSignal(process.Id, SignalStop);
            }
            else
            {
                _logger.Warning("Pausa nativa não disponível no Windows neste runtime.");
            }

            cell.State = CellState.Paused;
            _logger.Info("Célula pausada: " + id);

            PublishCellState(cell, CellState.Running.ToString());
            Persist();
        }

        public void ResumeCell(string id)
        {
            Cell cell = RequireCell(id);
            if (cell.State != CellState.Paused)
            {
                return;
            }

            Process process = GetRunningProcess(cell);
            if (process == null)
            {
                cell.State = CellState.Stopped;
                return;
            }

            if (!OperatingSystem.IsWindows())
            {
                SendSignal(process.Id, SignalContinue);
            }
            else
            {
                _logger.Warning("Retomada nativa não disponível no Windows neste runtime.");
            }

            cell.State = CellState.Running;
            _logger.Info("Célula retomada: " + id);

            PublishCellState(cell, CellState.Paused.ToString());
            Persist();
        }

        public void DeleteCell(string id)
        {
            Cell cell = RequireCell(id);

            if (cell.State == CellState.Running || cell.State == CellState.Paused)
            {
                StopCell(id);
            }

            _backend.Delete(cell);
            _cells.TryRemove(id, out _);
            CloseLogWriter(cell.Id);
            _logger.Info("Célula excluída: " + id);

            if (Events != null)
            {
                Events.Publish(new AURA.Core.Events.CellStateChangedEvent
                {
                    CellId = cell.Id,
                    From = cell.State.ToString(),
                    To = "Deleted"
                });
            }

            Persist();
        }

        public string ReadCellLog(string id, int tailLines = 50)
        {
            Cell cell = RequireCell(id);

            if (string.IsNullOrEmpty(cell.LogFile) || !File.Exists(cell.LogFile))
                return "(sem log)";

            try
            {
                var lines = File.ReadLines(cell.LogFile).TakeLast(Math.Max(1, tailLines));
                return string.Join("\n", lines);
            }
            catch (IOException)
            {
                try
                {
                    var all = File.ReadAllLines(cell.LogFile);
                    int start = Math.Max(0, all.Length - Math.Max(1, tailLines));
                    return string.Join("\n", all.Skip(start));
                }
                catch
                {
                    return "(log indisponível)";
                }
            }
        }

        /// <summary>
        /// Aguarda o processo da célula terminar e devolve o exit code.
        /// Usado pelo CLI `run --wait` para rodar o programa em primeiro plano.
        /// </summary>
        public async Task<int?> WaitCellAsync(string id)
        {
            Cell cell = RequireCell(id);
            Process process = GetRunningProcess(cell);
            if (process == null)
            {
                return null;
            }

            await process.WaitForExitAsync();
            // Garante que a saída assíncrona (BeginOutputReadLine) seja drenada
            // antes de devolver — senão as últimas linhas podem ser perdidas.
            process.WaitForExit(2000);
            return process.ExitCode;
        }

        /// <summary>
        /// Loads persisted cells and reattaches to still-alive processes
        /// (orphans from a previous AURA run). Dead cells are marked Crashed
        /// so the recycle logic can rebuild them.
        /// </summary>
        public async Task LoadFromStoreAsync()
        {
            if (_store == null)
            {
                return;
            }

            System.Collections.Generic.List<Cell> persisted = _store.Load();
            if (persisted.Count == 0)
            {
                return;
            }

            _logger.Info("Restaurando " + persisted.Count + " célula(s) persistida(s)...");

            foreach (Cell cell in persisted)
            {
                if (_cells.ContainsKey(cell.Id))
                {
                    continue;
                }

                _cells[cell.Id] = cell;
                cell.CellRoot = Path.Combine(_cellsRoot, cell.Id);

                if (cell.State == CellState.Stopped)
                {
                    _logger.Info("Célula '" + cell.Id + "' restaurada em estado Parada.");
                    continue;
                }

                if (cell.ProcessId.HasValue && ProcessAlive(cell.ProcessId.Value))
                {
                    Process orphan = AdoptProcess(cell);
                    if (orphan != null)
                    {
                        cell.State = CellState.Running;
                        _processes[cell.Id] = orphan;
                        _logger.Info("Célula '" + cell.Id + "' recuperada (processo vivo pid=" + orphan.Id + ").");
                        _ = WatchCellAsync(cell, recycleOnCrash: true);
                        continue;
                    }
                }

                cell.State = CellState.Crashed;
                _logger.Warning("Célula '" + cell.Id + "' estava morta. Reciclando...");
                if (cell.RestartCount < MaxRestartAttempts)
                {
                    cell.RestartCount++;
                    await StartCellAsync(cell.Id, recycleOnCrash: true);
                }
            }

            Persist();
        }

        private static bool ProcessAlive(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private Process AdoptProcess(Cell cell)
        {
            try
            {
                Process process = Process.GetProcessById(cell.ProcessId.Value);
                process.OutputDataReceived += (s, e) => AppendLog(cell, e.Data);
                process.ErrorDataReceived += (s, e) => AppendLog(cell, e.Data);
                return process;
            }
            catch
            {
                return null;
            }
        }

        private void Persist()
        {
            if (_persist && _store != null)
            {
                _store.Save(this);
            }
        }

        private void PublishCellState(Cell cell, string from)
        {
            if (Events != null)
            {
                Events.Publish(new AURA.Core.Events.CellStateChangedEvent
                {
                    CellId = cell.Id,
                    From = from,
                    To = cell.State.ToString()
                });
            }
        }

        /// <summary>Manually writes the cell index to disk. Returns the store path.</summary>
        public string PersistNow()
        {
            if (_store == null)
            {
                throw new InvalidOperationException("Persistência desabilitada neste runtime.");
            }

            _store.Save(this);
            return _store.Path;
        }

        private Cell RequireCell(string id)
        {
            Cell cell = GetCell(id);
            if (cell == null)
            {
                throw new InvalidOperationException("Célula não encontrada: '" + id + "'.");
            }

            return cell;
        }

        private Process GetRunningProcess(Cell cell)
        {
            Process process;
            if (_processes.TryGetValue(cell.Id, out process))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        return process;
                    }
                }
                catch (InvalidOperationException)
                {
                }

                _processes.TryRemove(cell.Id, out _);
            }

            return null;
        }

        private Process BuildProcess(Cell cell)
        {
            string fileName = cell.AppPath;
            string arguments = cell.Args;

            if (cell.Limits != null && !cell.Limits.IsEmpty)
            {
                BuildPrlimitCommand(cell.Limits, ref fileName, ref arguments);
            }

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = cell.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                psi.Arguments = arguments;
            }

            var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (s, e) => AppendLog(cell, e.Data);
            process.ErrorDataReceived += (s, e) => AppendLog(cell, e.Data);

            return process;
        }

        /// <summary>
        /// Rewrites the command as `prlimit --as=.. --cpu=.. --nofile=.. --nproc=..
        /// -- <original>` so limits apply without root. prlimit runs the original
        /// command with the limits set; child processes inherit them.
        /// </summary>
        private void BuildPrlimitCommand(ResourceLimits limits, ref string fileName, ref string arguments)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (limits.MemoryLimitMb.HasValue)
            {
                parts.Add("--as=" + (limits.MemoryLimitMb.Value * 1024 * 1024));
            }

            if (limits.CpuLimitSeconds.HasValue)
            {
                parts.Add("--cpu=" + limits.CpuLimitSeconds.Value);
            }

            if (limits.MaxFiles.HasValue)
            {
                parts.Add("--nofile=" + limits.MaxFiles.Value);
            }

            if (limits.MaxProcesses.HasValue)
            {
                parts.Add("--nproc=" + limits.MaxProcesses.Value);
            }

            parts.Add("--");

            string target = "\"" + fileName + "\"";
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                target += " " + arguments;
            }

            fileName = "prlimit";
            arguments = string.Join(" ", parts) + " " + target;

            _logger.Info("Célula com limites de recursos: " + string.Join(", ", parts));
        }

        private void AppendLog(Cell cell, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            try
            {
                lock (_logLock)
                {
                    StreamWriter writer = GetLogWriter(cell);
                    writer.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + line);
                    writer.Flush();
                }
            }
            catch
            {
                // Log é best-effort.
            }
        }

        private StreamWriter GetLogWriter(Cell cell)
        {
            if (_logWriters.TryGetValue(cell.Id, out StreamWriter existing))
            {
                return existing;
            }

            Directory.CreateDirectory(cell.RootDirectory);
            var writer = new StreamWriter(cell.LogFile, append: true, Encoding.UTF8);
            _logWriters[cell.Id] = writer;
            return writer;
        }

        private void CloseLogWriter(string id)
        {
            lock (_logLock)
            {
                if (_logWriters.TryRemove(id, out StreamWriter writer))
                {
                    try
                    {
                        writer.Flush();
                    }
                    finally
                    {
                        writer.Dispose();
                    }
                }
            }
        }

        private async Task WatchCellAsync(Cell cell, bool recycleOnCrash)
        {
            Process process = GetRunningProcess(cell);

            if (process == null)
            {
                cell.State = CellState.Crashed;
                if (recycleOnCrash)
                {
                    Recycle(cell);
                }

                return;
            }

            int exitCode;
            try
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
                // Drena a leitura assíncrona das saídas. Sem isso, linhas ainda
                // pendentes nos streams podem ser entregues depois do processo
                // ter sido reportado como terminado.
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            catch (Exception ex)
            {
                _logger.Warning("Erro observando célula '" + cell.Id + "': " + ex.Message);
                exitCode = -1;
            }

            _processes.TryRemove(cell.Id, out _);

            bool paused = cell.State == CellState.Paused;
            bool stoppedIntentionally = cell.State == CellState.Stopped;
            cell.State = paused
                ? CellState.Paused
                : (stoppedIntentionally || exitCode == 0 ? CellState.Stopped : CellState.Crashed);

            if (cell.State == CellState.Crashed && recycleOnCrash)
            {
                Recycle(cell);
            }

            CloseLogWriter(cell.Id);
        }

        private void Recycle(Cell cell)
        {
            if (cell.RestartCount >= MaxRestartAttempts)
            {
                _logger.Error("Célula '" + cell.Id + "' excedeu " + MaxRestartAttempts +
                    " reinícios. Interrompendo reciclagem.");
                cell.State = CellState.Crashed;
                return;
            }

            _logger.Warning("Célula '" + cell.Id + "' caiu. Recriando a partir do template...");
            CloseLogWriter(cell.Id);
            _backend.Delete(cell);
            _backend.Create(cell);

            if (cell.State == CellState.Crashed)
            {
                _ = StartCellAsync(cell.Id, recycleOnCrash: true);
            }
        }

        private static void SendSignal(int pid, int signal)
        {
            try
            {
                if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
                {
                    kill(pid, signal);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Signal falhou: " + ex.Message);
            }
        }

        public void Dispose()
        {
            foreach (Cell cell in _cells.Values)
            {
                try
                {
                    StopCell(cell.Id);
                }
                catch
                {
                }
            }

            _cells.Clear();
            _processes.Clear();

            lock (_logLock)
            {
                foreach (StreamWriter writer in _logWriters.Values)
                {
                    try
                    {
                        writer.Flush();
                    }
                    finally
                    {
                        writer.Dispose();
                    }
                }

                _logWriters.Clear();
            }
        }

        public static string ExpandUserHome(string path)
        {
            if (path == "~/AURA/cells" || (path != null && path.StartsWith("~/", StringComparison.Ordinal)))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, path.Substring(2));
            }

            return path;
        }

        private static string ExpandHome(string path)
        {
            return ExpandUserHome(path);
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);
    }
}
