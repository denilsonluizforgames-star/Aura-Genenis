using System;

namespace AURA.Core.Logging
{
    /// <summary>
    /// Writes log messages to the console with color coding by severity.
    /// Safe to use even when no console is attached (e.g. WinForms apps),
    /// since writes are swallowed if the console is unavailable.
    /// </summary>
    public sealed class ConsoleLogger : ILogger
    {
        public void Info(string message)
        {
            Write(ConsoleColor.White, "[INFO ] " + message);
        }

        public void Warning(string message)
        {
            Write(ConsoleColor.Yellow, "[WARN ] " + message);
        }

        public void Error(string message)
        {
            Write(ConsoleColor.Red, "[ERROR] " + message);
        }

        private static void Write(ConsoleColor color, string line)
        {
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(line);
                Console.ResetColor();
            }
            catch
            {
                // No console attached (e.g. running as a WinForms app) - ignore.
            }
        }
    }
}
