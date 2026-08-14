namespace AURA.Core.Abstractions
{
    /// <summary>
    /// Represents an intelligent agent that can be started and stopped by the
    /// AgentManager. Reserved for future AURA versions (1.1+).
    /// </summary>
    public interface IAgent
    {
        string Name { get; }

        void Start();

        void Stop();
    }
}
