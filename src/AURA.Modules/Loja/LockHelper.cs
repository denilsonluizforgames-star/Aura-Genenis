using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AURA.Modules.Loja
{
    public static class LockHelper
    {
        /// <summary>
        /// Try to acquire an exclusive file lock on the given path within the provided timeout.
        /// Returns an open FileStream with exclusive lock, or null if the lock couldn't be acquired
        /// before the timeout expired.
        /// </summary>
        public static FileStream? TryAcquireLock(string lockPath, TimeSpan timeout)
        {
            if (string.IsNullOrEmpty(lockPath)) throw new ArgumentNullException(nameof(lockPath));

            var dir = Path.GetDirectoryName(lockPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sw = Stopwatch.StartNew();
            int backoff = 50; // ms
            while (true)
            {
                try
                {
                    // create or open with exclusive lock
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (Exception)
                {
                    if (sw.Elapsed >= timeout)
                    {
                        return null;
                    }
                    Thread.Sleep(backoff);
                    // simple exponential backoff with cap
                    backoff = Math.Min(1000, backoff * 2);
                }
            }
        }
    }
}
