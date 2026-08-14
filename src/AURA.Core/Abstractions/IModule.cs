namespace AURA.Core.Abstractions
{
    /// <summary>
    /// Represents one of the optional, user-selectable AURA capability
    /// modules (Windows Assistant, AI, Automation, Memory, Plugins, ...).
    /// This is intentionally a lightweight, descriptive contract for the
    /// Genesis Core MVP - actual module implementations arrive in later
    /// versions (see the "Próximas versões" roadmap).
    /// </summary>
    public interface IModule
    {
        string Id { get; }

        string DisplayName { get; }
    }
}
