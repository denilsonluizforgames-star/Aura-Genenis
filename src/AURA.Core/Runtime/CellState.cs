namespace AURA.Core.Runtime
{
    /// <summary>
    /// Lifecycle states of an AURA cell. A cell is an isolated instance of an
    /// application that AURA supervises. States map to the "pause and resume"
    /// model: a crashed cell is deleted and recreated from its template.
    /// </summary>
    public enum CellState
    {
        /// <summary>Created on disk, not running.</summary>
        Created = 0,

        /// <summary>Process is running.</summary>
        Running = 1,

        /// <summary>Process suspended (SIGSTOP).</summary>
        Paused = 2,

        /// <summary>Process exited cleanly.</summary>
        Stopped = 3,

        /// <summary>Process crashed or was killed; awaiting recycle.</summary>
        Crashed = 4
    }
}
