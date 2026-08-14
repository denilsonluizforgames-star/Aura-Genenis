using System.Text.Json.Serialization;

namespace AURA.Core.Runtime
{
    /// <summary>
    /// Per-cell resource limits. Applied without root by prefixing the cell
    /// command with `prlimit` (available on Termux and Linux), so a runaway
    /// cell cannot exhaust the device.
    /// </summary>
    public sealed class ResourceLimits
    {
        /// <summary>Address-space (memory) cap in MiB, maps to `--as`.</summary>
        public long? MemoryLimitMb { get; set; }

        /// <summary>CPU time cap in seconds, maps to `--cpu`.</summary>
        public long? CpuLimitSeconds { get; set; }

        /// <summary>Max open file descriptors, maps to `--nofile`.</summary>
        public long? MaxFiles { get; set; }

        /// <summary>Max processes/threads, maps to `--nproc`.</summary>
        public long? MaxProcesses { get; set; }

        [JsonIgnore]
        public bool IsEmpty =>
            !MemoryLimitMb.HasValue &&
            !CpuLimitSeconds.HasValue &&
            !MaxFiles.HasValue &&
            !MaxProcesses.HasValue;
    }
}
