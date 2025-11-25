using System.Collections.Generic;
using System.Reflection;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using System;
using System.Diagnostics;

namespace Microsoft.PowerShell.UnixTabCompletion;

public static class Completion
{
    private static Task s_initTask;
    private static ScriptBlock s_completionScript;
    private static Dictionary<string, ScriptBlock> s_completerTable;

    internal static IEnumerable<string> RegisteredCommands { get; set; }
    internal static CompleterBase Completer { get; set; }

    internal static ScriptBlock CompletionScript
    {
        get
        {
            WaitForInitializationDone();
            return s_completionScript;
        }
    }

    internal static Dictionary<string, ScriptBlock> CompleterTable
    {
        get
        {
            WaitForInitializationDone();
            return s_completerTable;
        }
    }

    internal static void Initialize(Runspace runspace)
    {
        s_initTask = Task.Factory.StartNew(StartTask, runspace);
    }

    private static void WaitForInitializationDone()
    {
        if (s_initTask is null)
        {
            throw new InvalidOperationException($"Not initialized. Call '{nameof(Initialize)}' first.");
        }

        s_initTask.Wait();
    }

    private static void StartTask(object state)
    {
        string script = $"""
            param($wordToComplete, $commandAst, $cursorPosition)
            [{typeof(Completion).FullName}]::{nameof(CompleteCommand)}($wordToComplete, $commandAst, $cursorPosition)
            """;
        s_completionScript = ScriptBlock.Create(script);

        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        PropertyInfo ctxProperty = typeof(Runspace).GetProperty("ExecutionContext", flags);
        PropertyInfo nacProperty = ctxProperty.PropertyType.GetProperty("NativeArgumentCompleters", flags);

        var runspace = (Runspace)state;
        var context = ctxProperty.GetValue(runspace);
        var table = nacProperty.GetValue(context);

        if (table is null)
        {
            table = new Dictionary<string, ScriptBlock>(StringComparer.OrdinalIgnoreCase);
            nacProperty.SetValue(context, table);
        }

        s_completerTable = (Dictionary<string, ScriptBlock>)table;
    }

    public static IEnumerable<CompletionResult> CompleteCommand(string wordToComplete, CommandAst commandAst, int cursorPosition) =>
        Completer?.CompleteCommand(wordToComplete, commandAst, cursorPosition);
}

public enum ShellType
{
    None = 0,
    Zsh,
    Bash,
}

public class InitAndCleanup : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    private const string SHELL_PREFERENCE_VARNAME = "COMPLETION_SHELL_PREFERENCE";
    private const string BASH_COMPLETION_VARNAME = "BASH_COMPLETION";

    public void OnImport()
    {
        Completion.Initialize(Runspace.DefaultRunspace);

        string preferredShell = Environment.GetEnvironmentVariable(SHELL_PREFERENCE_VARNAME);
        string completionScript = Environment.GetEnvironmentVariable(BASH_COMPLETION_VARNAME);

        if ((string.IsNullOrEmpty(preferredShell) || !UnixHelpers.TryFindShell(preferredShell, out string shellPath, out ShellType shellType))
            && !UnixHelpers.TryFindDefaultShell(out shellPath, out shellType))
        {
            WriteError("Unable to find shell to provide unix utility completions");
            return;
        }

        CompleterBase completer = shellType switch
        {
            ShellType.Bash => new BashCompleter(shellPath, completionScript),
            ShellType.Zsh => new ZshCompleter(shellPath),
            _ => throw new UnreachableException()
        };

        var commandsToRegister = completer.FindCompletableCommands();

        Completion.Completer = completer;
        Completion.RegisteredCommands = commandsToRegister;

        ScriptBlock sb = Completion.CompletionScript;
        Dictionary<string, ScriptBlock> table = Completion.CompleterTable;

        foreach (string command in commandsToRegister)
        {
            table[command] = sb;
        }
    }

    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        Dictionary<string, ScriptBlock> table = Completion.CompleterTable;
        foreach (string command in Completion.RegisteredCommands)
        {
            table.Remove(command);
        }
    }

    private static void WriteError(string errorMessage)
    {
        using var pwsh = System.Management.Automation.PowerShell.Create();
        pwsh.AddCommand("Write-Error")
            .AddParameter("Message", errorMessage)
            .Invoke();
    }
}
