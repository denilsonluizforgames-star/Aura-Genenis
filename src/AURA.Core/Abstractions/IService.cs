namespace AURA.Core.Abstractions
{
    /// <summary>
    /// Represents a long-lived service managed by the ServiceContainer.
    /// </summary>
    public interface IService
    {
        void Initialize();
    }
}
