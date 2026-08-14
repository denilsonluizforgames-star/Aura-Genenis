namespace AURA.SystemInfo
{
    /// <summary>
    /// Result of a system analysis: operating system, CPU, memory and disk.
    /// </summary>
    public class SystemDiagnosticsResult
    {
        public string OperatingSystem { get; set; }

        public string Architecture { get; set; }

        public int ProcessorCount { get; set; }

        public double TotalMemoryGb { get; set; }

        public double AvailableMemoryGb { get; set; }

        public string SystemDrive { get; set; }

        public double TotalDiskSpaceGb { get; set; }

        public double FreeDiskSpaceGb { get; set; }

        public bool MeetsMinimumRequirements { get; set; }
    }
}
