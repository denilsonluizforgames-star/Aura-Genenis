namespace AURA.Core.Logging
{
    /// <summary>
    /// Minimal logging abstraction used across all AURA modules.
    /// </summary>
    public interface ILogger
    {
        void Info(string message);

        void Warning(string message);

        void Error(string message);
    }
}
