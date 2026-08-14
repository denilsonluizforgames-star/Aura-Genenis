using System;
using System.IO;

namespace AURA.Core.Logging
{
    /// <summary>
    /// Appends log messages to a rolling text file, used mainly by the GUI
    /// where there is no console output visible to the user.
    /// </summary>
    public sealed class FileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly object _sync = new object();

        public FileLogger(string filePath)
        {
            _filePath = filePath;

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void Info(string message)
        {
            Append("INFO", message);
        }

        public void Warning(string message)
        {
            Append("WARN", message);
        }

        public void Error(string message)
        {
            Append("ERROR", message);
        }

        private void Append(string level, string message)
        {
            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}", DateTime.Now, level, message);

            lock (_sync)
            {
                try
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine);
                }
                catch
                {
                    // Logging must never crash the application.
                }
            }
        }
    }
}
