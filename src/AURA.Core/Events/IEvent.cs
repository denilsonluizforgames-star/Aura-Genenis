using System;

namespace AURA.Core.Events
{
    /// <summary>
    /// Marker interface for events published on the EventBus.
    /// </summary>
    public interface IEvent
    {
        DateTime OccurredAt { get; }
    }
}
