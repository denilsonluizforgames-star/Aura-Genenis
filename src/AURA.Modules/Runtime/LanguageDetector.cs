using System.IO;
using System.Text.RegularExpressions;
using AURA.Abstractions.Runtime;

namespace AURA.Modules.Runtime;

/// <summary>
/// Detecta a linguagem de um arquivo por heurísticas encadeadas:
/// extensão → shebang → conteúdo → magic bytes. Equivalente a <c>detector.py</c>.
/// </summary>
public sealed class LanguageDetector : IRuntimeDetector
{
    private static readonly (Regex Pattern, string Language)[] ShebangPatterns =
    {
        (new Regex(@"python[0-9.]*"), "python"),
        (new Regex(@"bash"), "shell"),
        (new Regex(@"sh\b"), "shell"),
        (new Regex(@"zsh"), "shell"),
        (new Regex(@"node"), "node"),
        (new Regex(@"deno"), "node"),
        (new Regex(@"java"), "java"),
        (new Regex(@"go\b"), "go"),
        (new Regex(@"ruby"), "ruby"),
        (new Regex(@"php"), "php"),
        (new Regex(@"lua"), "lua"),
        (new Regex(@"dotnet"), "dotnet"),
    };

    private static readonly (Regex Pattern, string Language)[] ContentPatterns =
    {
        (new Regex(@"^\s*#!.*python", RegexOptions.Multiline), "python"),
        (new Regex(@"^\s*(import|from)\s+[a-zA-Z_]", RegexOptions.Multiline), "python"),
        (new Regex(@"^\s*def\s+\w+\s*\(", RegexOptions.Multiline), "python"),
        (new Regex(@"^\s*<\?php", RegexOptions.Multiline), "php"),
        (new Regex(@"^\s*(import\s+\w+\s+from|export\s+default)", RegexOptions.Multiline), "node"),
        (new Regex(@"^\s*package\s+main", RegexOptions.Multiline), "go"),
        (new Regex(@"^\s*func\s+main\s*\(", RegexOptions.Multiline), "go"),
        (new Regex(@"^\s*public\s+(class|static)", RegexOptions.Multiline), "java"),
        (new Regex(@"^\s*#include\s*<", RegexOptions.Multiline), "c"),
    };

    private static readonly (byte[] Magic, string Language)[] MagicPatterns =
    {
        (new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "zip"),
        (new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }, "class"),
        (new byte[] { 0x4D, 0x5A }, "pe"),
    };

    public Detection Detect(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new Detection { DetectedBy = "missing" };
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        var detection = new Detection { Extension = ext };

        // 1) Extensão
        if (RuntimeCatalog.Extensions.TryGetValue(ext, out string? byExtension))
        {
            detection.Language = byExtension;
            detection.Confidence = 0.85;
            detection.DetectedBy = "extension";
            detection.Hints.Add($"extensão {ext}");
        }

        // 2) Shebang (tem precedência sobre a extensão)
        string shebang = ReadFirstLine(filePath);
        foreach ((Regex pattern, string slang) in ShebangPatterns)
        {
            if (!pattern.IsMatch(shebang)) continue;

            if (detection.DetectedBy == "extension" && slang != detection.Language)
            {
                detection.Hints.Add($"shebang conflitante: {shebang}");
            }

            detection.Language = slang;
            detection.Confidence = Math.Max(detection.Confidence, 0.95);
            detection.DetectedBy = "shebang";
            detection.Hints.Add($"shebang: {shebang}");
            break;
        }

        // 3) Conteúdo (só se ainda não temos certeza)
        if (!detection.Known && IsTextFile(filePath))
        {
            string head = ReadHead(filePath);
            foreach ((Regex pattern, string clang) in ContentPatterns)
            {
                if (!pattern.IsMatch(head)) continue;

                detection.Language = clang;
                detection.Confidence = 0.7;
                detection.DetectedBy = "content";
                detection.Hints.Add($"assinatura de conteúdo: {clang}");
                break;
            }
        }

        // 4) Magic bytes (binários)
        if (!detection.Known)
        {
            byte[] magic = ReadMagic(filePath);
            foreach ((byte[] bytes, string mlang) in MagicPatterns)
            {
                if (!StartsWith(magic, bytes)) continue;

                detection.Language = mlang;
                detection.Confidence = 0.9;
                detection.DetectedBy = "magic";
                detection.Hints.Add($"magic bytes: {mlang}");
                break;
            }
        }

        return detection;
    }

    // ------------------------------------------------------------------ //
    private static string ReadFirstLine(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            return reader.ReadLine()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadHead(string path, int limit = 4096)
    {
        try
        {
            using var reader = new StreamReader(path);
            char[] buffer = new char[limit];
            int count = reader.Read(buffer, 0, limit);
            return new string(buffer, 0, count);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsTextFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            byte[] chunk = new byte[1024];
            int count = stream.Read(chunk, 0, chunk.Length);
            for (int i = 0; i < count; i++)
            {
                if (chunk[i] == 0) return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ReadMagic(string path, int length = 8)
    {
        try
        {
            using var stream = File.OpenRead(path);
            byte[] buffer = new byte[length];
            int count = stream.Read(buffer, 0, length);
            return buffer[..count];
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static bool StartsWith(byte[] source, byte[] prefix)
    {
        if (prefix.Length > source.Length) return false;
        for (int i = 0; i < prefix.Length; i++)
        {
            if (source[i] != prefix[i]) return false;
        }
        return true;
    }
}
