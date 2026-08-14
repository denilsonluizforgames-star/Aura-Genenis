using AURA.Core.Runtime;

namespace AURA.Core.Launchers
{
    /// <summary>
    /// A launcher knows how to run one kind of file (.py, .jar, .dll, ...)
    /// inside an AURA cell. AURA picks the launcher based on the file so the
    /// user only needs to pick the program.
    /// </summary>
    public interface ILauncher
    {
        /// <summary>File extensions this launcher supports, e.g. ".py".</summary>
        string[] SupportedExtensions { get; }

        /// <summary>True when the launcher can handle the given file.</summary>
        bool Supports(string filePath);

        /// <summary>Builds the command line used to start the file in a cell.</summary>
        CellCommand BuildCommand(string filePath, string arguments);
    }
}
