using System;

namespace AURA.Core
{
    /// <summary>
    /// Provides static version and product identity information for AURA.
    /// </summary>
    public static class VersionInfo
    {
        public const string Name = "AURA Genesis Core";
        public const string Version = "1.0.0-mvp";

        public static string FullName
        {
            get { return Name + " " + Version; }
        }
    }
}
