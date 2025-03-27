using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.UnixTabCompletion;

internal static partial class UnixHelpers
{
    private static readonly List<string> s_nativeUtilDirs;
    private static readonly Dictionary<string, ShellType> s_shells;
    private static readonly Task<List<string>> s_findUtilsNamesTask;

    static UnixHelpers()
    {
        s_nativeUtilDirs = ["/usr/local/sbin", "/usr/local/bin", "/usr/sbin", "/usr/bin", "/sbin", "/bin"];
        s_shells = new()
        {
            ["zsh"] = ShellType.Zsh,
            ["bash"] = ShellType.Bash
        };

        s_findUtilsNamesTask = Task.Run(GetNativeUtilNames);
    }

    internal static List<string> NativeUtilNames => s_findUtilsNamesTask.Result;

    internal static bool TryFindShell(string shellName, out string shellPath, out ShellType shellType)
    {
        // No shell name provided
        if (string.IsNullOrEmpty(shellName))
        {
            shellPath = null;
            shellType = ShellType.None;
            return false;
        }

        // Look for absolute path to a shell
        if (Path.IsPathRooted(shellName)
            && s_shells.TryGetValue(Path.GetFileName(shellName), out shellType)
            && File.Exists(shellName))
        {
            shellPath = shellName;
            return true;
        }

        // Now assume the shell is just a command name, and confirm we recognize it
        if (!s_shells.TryGetValue(shellName, out shellType))
        {
            shellPath = null;
            return false;
        }

        return TryFindShellByName(shellName, out shellPath);
    }

    internal static bool TryFindDefaultShell(out string foundShell, out ShellType shellType)
    {
        foreach (KeyValuePair<string, ShellType> shell in s_shells)
        {
            if (TryFindShellByName(shell.Key, out foundShell))
            {
                shellType = shell.Value;
                return true;
            }
        }

        foundShell = null;
        shellType = ShellType.None;
        return false;
    }

    private static bool TryFindShellByName(string shellName, out string foundShellPath)
    {
        foreach (string utilDir in s_nativeUtilDirs)
        {
            string shellPath = Path.Combine(utilDir, shellName);
            if (File.Exists(shellPath))
            {
                foundShellPath = shellPath;
                return true;
            }
        }

        foundShellPath = null;
        return false;
    }

    private static List<string> GetNativeUtilNames()
    {
        var commandSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string utilDir in s_nativeUtilDirs)
        {
            if (!Directory.Exists(utilDir))
            {
                continue;
            }

            foreach (string utilPath in Directory.EnumerateFiles(utilDir))
            {
                if (IsExecutable(utilPath))
                {
                    commandSet.Add(Path.GetFileName(utilPath));
                }
            }
        }

        return [.. commandSet];
    }

    private static bool IsExecutable(string path)
    {
        const int X_OK = 0x01;
        return access(path, X_OK) != -1;
    }

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int access(string pathname, int mode);
}
