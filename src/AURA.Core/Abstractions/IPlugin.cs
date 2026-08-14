namespace AURA.Core.Abstractions
{
    /// <summary>
    /// Represents an externally loaded plugin. Reserved for the future
    /// AURA.Plugins module.
    /// </summary>
    public interface IPlugin
    {
        string Id { get; }

        string Name { get; }

        void Load();

        void Unload();
    }
}
