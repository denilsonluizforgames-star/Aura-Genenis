using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AURA.SystemInfo
{
    /// <summary>
    /// Collects basic system diagnostics (OS, architecture, CPU, RAM, disk)
    /// and checks whether the machine meets AURA's minimum requirements.
    /// Works on Windows, Linux and Termux/Android.
    /// </summary>
    public sealed class SystemAnalyzer
    {
        private const string Windows7Sp1Build = "6.1.7601";

        public SystemDiagnosticsResult Analyze()
        {
            var result = new SystemDiagnosticsResult
            {
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                MeetsMinimumRequirements = true
            };

            if (OperatingSystem.IsWindows())
            {
                result.MeetsMinimumRequirements = Environment.OSVersion.Version >= new Version(Windows7Sp1Build);
            }

            ReadMemory(result);
            ReadDisk(result);

            return result;
        }

        private static void ReadMemory(SystemDiagnosticsResult result)
        {
            result.TotalMemoryGb = 0;
            result.AvailableMemoryGb = 0;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    ReadWindowsMemory(result);
                }
                else
                {
                    ReadLinuxMemory(result);
                }
            }
            catch
            {
                // API indisponível - mantém 0.
            }
        }

        private static void ReadWindowsMemory(SystemDiagnosticsResult result)
        {
            var status = new MEMORYSTATUSEX();
            status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

            if (GlobalMemoryStatusEx(ref status))
            {
                result.TotalMemoryGb = BytesToGb(status.ullTotalPhys);
                result.AvailableMemoryGb = BytesToGb(status.ullAvailPhys);
            }
        }

        private static void ReadLinuxMemory(SystemDiagnosticsResult result)
        {
            string[] lines = File.ReadAllLines("/proc/meminfo");

            foreach (string line in lines)
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    result.TotalMemoryGb = BytesToGb(KibToBytes(ParseKilobytes(line)));
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    result.AvailableMemoryGb = BytesToGb(KibToBytes(ParseKilobytes(line)));
                }
            }
        }

        private static long ParseKilobytes(string line)
        {
            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? long.Parse(parts[1]) : 0;
        }

        private static double KibToBytes(long kib)
        {
            return kib * 1024.0;
        }

        private static void ReadDisk(SystemDiagnosticsResult result)
        {
            try
            {
                string root = Path.GetPathRoot(Directory.GetCurrentDirectory());
                result.SystemDrive = string.IsNullOrEmpty(root) ? Path.DirectorySeparatorChar.ToString() : root;

                var drive = new DriveInfo(result.SystemDrive);
                result.TotalDiskSpaceGb = BytesToGb(drive.TotalSize);
                result.FreeDiskSpaceGb = BytesToGb(drive.AvailableFreeSpace);
            }
            catch
            {
                result.SystemDrive = Path.DirectorySeparatorChar.ToString();
                result.TotalDiskSpaceGb = 0;
                result.FreeDiskSpaceGb = 0;
            }
        }

        private static double BytesToGb(double bytes)
        {
            return bytes / (1024.0 * 1024.0 * 1024.0);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
