using System;
using System.IO;

namespace AURA.Core.Runtime
{
    /// <summary>
    /// The default cell backend: a plain directory. Works everywhere,
    /// including Termux without root. A crashed cell is "deleted and
    /// recreated" by removing the directory and re-seeding it from a
    /// template directory (if one is configured).
    /// </summary>
    public sealed class DirectoryCellBackend : ICellBackend
    {
        public string Name => "directory";

        public void Create(Cell cell)
        {
            string root = cell.RootDirectory;
            Directory.CreateDirectory(root);

            if (!string.IsNullOrEmpty(cell.TemplatePath) && Directory.Exists(cell.TemplatePath))
            {
                CopyDirectory(cell.TemplatePath, root);
            }
        }

        public void Delete(Cell cell)
        {
            string root = cell.RootDirectory;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        public bool Exists(Cell cell)
        {
            return Directory.Exists(cell.RootDirectory);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.Replace(source, destination));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(source, destination), overwrite: true);
            }
        }
    }
}
