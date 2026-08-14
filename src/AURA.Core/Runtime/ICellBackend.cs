namespace AURA.Core.Runtime
{
    /// <summary>
    /// Abstraction over where a cell's root filesystem lives. On Termux today
    /// a cell is a plain directory (no root needed). On real Linux a backend
    /// backed by a qcow2 image + KVM can be plugged in without touching the
    /// rest of the runtime.
    /// </summary>
    public interface ICellBackend
    {
        /// <summary>Name of the backend, e.g. "directory" or "qcow2".</summary>
        string Name { get; }

        /// <summary>Creates the backing store for a new cell.</summary>
        void Create(Cell cell);

        /// <summary>Deletes the backing store and all its contents.</summary>
        void Delete(Cell cell);

        /// <summary>True if the backing store exists on disk.</summary>
        bool Exists(Cell cell);
    }
}
