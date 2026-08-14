using System.IO;
using System.Text.RegularExpressions;
using AURA.Abstractions.Runtime;

namespace AURA.Modules.Runtime;

/// <summary>
/// Analisa as dependências de um arquivo: imports Python (stdlib ignorada),
/// manifestos (requirements.txt/pyproject.toml/package.json/pom.xml) e
/// binários invocados em scripts shell. Equivalente a <c>deps.py</c>.
/// </summary>
public sealed class DependencyAnalyzer : IDependencyAnalyzer
{
    // Módulos da stdlib Python — não são dependências externas.
    private static readonly HashSet<string> PythonStdlib = new(StringComparer.Ordinal)
    {
        "abc", "argparse", "ast", "asyncio", "base64", "binascii", "bisect",
        "builtins", "collections", "concurrent", "configparser", "contextlib",
        "copy", "csv", "ctypes", "dataclasses", "datetime", "decimal", "difflib",
        "dis", "email", "enum", "errno", "fractions", "functools", "gc", "getopt",
        "glob", "gzip", "hashlib", "heapq", "hmac", "html", "http", "importlib",
        "inspect", "io", "ipaddress", "itertools", "json", "jsonpickle", "keyword",
        "logging", "math", "mimetypes", "multiprocessing", "numbers", "operator",
        "os", "pathlib", "pickle", "platform", "plistlib", "pprint", "profile",
        "pstats", "queue", "random", "re", "readline", "reprlib", "resource",
        "secrets", "select", "selectors", "shelve", "shlex", "shutil", "signal",
        "site", "socket", "socketserver", "sqlite3", "ssl", "stat", "statistics",
        "string", "stringprep", "struct", "subprocess", "sys", "sysconfig",
        "syslog", "tarfile", "tempfile", "textwrap", "threading", "time", "timeit",
        "tkinter", "token", "tokenize", "tomllib", "trace", "traceback", "tracemalloc",
        "types", "typing", "unicodedata", "unittest", "urllib", "uuid", "venv",
        "warnings", "wave", "weakref", "webbrowser", "xml", "xmlrpc", "zipfile",
        "zipimport", "zlib",
    };

    // Pacotes pip cujo import name difere do package name.
    private static readonly IReadOnlyDictionary<string, string> ImportToPackage =
        new Dictionary<string, string>
        {
            ["PIL"] = "pillow", ["yaml"] = "pyyaml", ["bs4"] = "beautifulsoup4",
            ["dotenv"] = "python-dotenv",
        };

    private static readonly Regex ImportRegex = new(
        @"^\s*(?:from\s+([\w\.]+)\s+import|\bimport\s+([\w\.]+))",
        RegexOptions.Multiline);

    // Comandos em posição de comando: início de linha ou após ; && || | & ( ).
    private static readonly Regex ShellCommandRegex = new(
        @"(?:^|;|&&|\|\||\||&|\()\s*((?:sudo\s+)?[a-z][a-z0-9_-]*)\b",
        RegexOptions.Multiline);

    private static readonly HashSet<string> ShellWhitelist = new(StringComparer.Ordinal)
    {
        "if", "then", "else", "elif", "fi", "do", "done", "for", "while", "case",
        "esac", "in", "function", "local", "export", "return", "exit", "echo",
        "printf", "read", "test", "[", "]", "cd", "pwd", "ls", "cat", "mkdir",
        "rm", "cp", "mv", "touch", "chmod", "chown", "grep", "sed", "awk", "cut",
        "head", "tail", "sort", "uniq", "wc", "sleep", "date", "true", "false",
        "set", "shift", "source", "eval", "exec", "trap", "ulimit", "umask",
        "which", "type", "command", "let", "find", "xargs", "basename", "dirname",
        "getent", "id", "whoami", "uname", "curl", "wget", "unzip", "zip", "tar",
        "gzip", "git", "python", "python3", "pip", "pip3", "node", "npm", "bash",
        "sh", "jq", "tee", "tr", "seq", "x", "kill", "killall", "ps", "top",
        "df", "du", "free", "stat", "dd", "ln", "mount", "umount", "sync",
        "wait", "jobs", "fg", "bg", "alias", "unset", "declare", "typeset",
        "readonly", "hash", "enable", "help", "select", "coproc",
    };

    public DependencyReport Analyze(string filePath, string language)
    {
        var report = new DependencyReport { Language = language };
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return report;
        }

        switch (language)
        {
            case "python": AnalyzePython(filePath, report); break;
            case "node": AnalyzeNode(filePath, report); break;
            case "shell": AnalyzeShell(filePath, report); break;
            case "java": AnalyzeJava(filePath, report); break;
        }

        return report;
    }

    // ------------------------------------------------------------------ //
    private void AnalyzePython(string filePath, DependencyReport report)
    {
        string text = Read(filePath);
        if (string.IsNullOrEmpty(text)) return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ImportRegex.Matches(text))
        {
            // Para "from X import": grupo 1 = X; para "import X": grupo 2 = X.
            string module = (match.Groups[1].Value + match.Groups[2].Value)
                .Split('.')[0];
            if (string.IsNullOrEmpty(module) ||
                PythonStdlib.Contains(module) ||
                !seen.Add(module))
            {
                continue;
            }

            string package = ImportToPackage.TryGetValue(module, out string? mapped)
                ? mapped
                : module;

            report.Dependencies.Add(new Dependency
            {
                Name = package,
                Kind = "import",
                RequiredBy = module,
                InstallCommand = $"pip install {package}",
            });
        }

        // Manifestos no diretório do arquivo
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";
        foreach (string manifest in new[] { "requirements.txt", "pyproject.toml" })
        {
            string path = Path.Combine(baseDir, manifest);
            if (File.Exists(path))
            {
                report.Dependencies.Add(new Dependency
                {
                    Name = manifest,
                    Kind = "manifest",
                    RequiredBy = path,
                    InstallCommand = $"pip install -r {manifest}",
                });
            }
        }
    }

    private void AnalyzeNode(string filePath, DependencyReport report)
    {
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";
        string pkgPath = Path.Combine(baseDir, "package.json");
        if (File.Exists(pkgPath))
        {
            report.Dependencies.Add(new Dependency
            {
                Name = "package.json",
                Kind = "manifest",
                RequiredBy = pkgPath,
                InstallCommand = "npm install",
            });
        }
    }

    private void AnalyzeShell(string filePath, DependencyReport report)
    {
        string text = Read(filePath);
        if (string.IsNullOrEmpty(text)) return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ShellCommandRegex.Matches(text))
        {
            string command = match.Groups[1].Value;
            if (command.StartsWith("sudo ", StringComparison.Ordinal))
            {
                command = command[5..];
            }
            if (ShellWhitelist.Contains(command) || !seen.Add(command))
            {
                continue;
            }

            report.Dependencies.Add(new Dependency
            {
                Name = command,
                Kind = "binary",
                RequiredBy = "comando invocado",
                InstallCommand = $"pkg install {command}",
            });
        }
    }

    private void AnalyzeJava(string filePath, DependencyReport report)
    {
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".";
        string pom = Path.Combine(baseDir, "pom.xml");
        if (File.Exists(pom))
        {
            report.Dependencies.Add(new Dependency
            {
                Name = "pom.xml",
                Kind = "manifest",
                RequiredBy = pom,
                InstallCommand = "mvn dependency:resolve",
            });
        }
    }

    private static string Read(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return string.Empty;
        }
    }
}
