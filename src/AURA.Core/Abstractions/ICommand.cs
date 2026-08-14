namespace AURA.Core.Abstractions
{
    /// <summary>
    /// Represents a single executable command (used by the CLI and, later,
    /// by the automation/plugin systems).
    /// </summary>
    public interface ICommand
    {
        string Name { get; }

        string Description { get; }

        void Execute(string[] args);
    }
}
