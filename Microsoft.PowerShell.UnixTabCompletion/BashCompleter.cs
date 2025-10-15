using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;

namespace Microsoft.PowerShell.UnixTabCompletion
{
    public class BashCompleter : CompleterBase
    {
        private readonly string _completionScript;

        private static readonly string s_resolveCompleterCommandTemplate = string.Join("; ", new []
        {
            "-lc \". {0} 2>/dev/null",
            "__load_completion {1} 2>/dev/null",
            "complete -p {1} 2>/dev/null | sed -E 's/^complete.*-F ([^ ]+).*$/\\1/'\""
        });

        private readonly Dictionary<string, string> _commandCompletionFunctions;

        private readonly string _bashPath;

        public BashCompleter(string bashPath, string completionScript)
        {
            ArgumentException.ThrowIfNullOrEmpty(bashPath);

            _bashPath = bashPath;
            _commandCompletionFunctions = [];
            _completionScript = string.IsNullOrEmpty(completionScript)
                ? "/usr/share/bash-completion/bash_completion"
                : completionScript;
        }

        public override IEnumerable<CompletionResult> CompleteCommand(
            string wordToComplete,
            CommandAst commandAst,
            int cursorPosition)
        {
            string command = commandAst.GetCommandName();
            string completerFunction = ResolveCommandCompleterFunction(command);

            if (string.IsNullOrEmpty(completerFunction))
            {
                yield break;
            }

            int cursorWordIndex = 0;
            var elements = commandAst.CommandElements;
            string previousWord = elements[0].Extent.Text;
            for (int i = 1; i < elements.Count; i++)
            {
                IScriptExtent elementExtent = elements[i].Extent;

                if (cursorPosition < elementExtent.EndColumnNumber)
                {
                    previousWord = elements[i - 1].Extent.Text;
                    cursorWordIndex = i;
                    break;
                }

                if (cursorPosition == elementExtent.EndColumnNumber)
                {
                    previousWord = elementExtent.Text;
                    cursorWordIndex = i + 1;
                    break;
                }

                if (cursorPosition < elementExtent.StartColumnNumber)
                {
                    previousWord = elements[i - 1].Extent.Text;
                    cursorWordIndex = i;
                    break;
                }

                if (i == elements.Count - 1 && cursorPosition > elementExtent.EndColumnNumber)
                {
                    previousWord = elementExtent.Text;
                    cursorWordIndex = i + 1;
                    break;
                }
            }

            string commandLine;
            string bashWordArray;
            string commandText = commandAst.Extent.Text;

            if (cursorWordIndex > 0)
            {
                commandLine = $"'{commandText}'";

                // Handle a case like '/mnt/c/Program Files'/<TAB> where the slash is outside the string
                IScriptExtent currentExtent = elements[cursorWordIndex].Extent;      // The presumed slash-prefixed string
                IScriptExtent previousExtent = elements[cursorWordIndex - 1].Extent; // The string argument
                if (currentExtent.Text.StartsWith('/') && currentExtent.StartColumnNumber == previousExtent.EndColumnNumber)
                {
                    commandLine = commandLine.Replace(previousExtent.Text + currentExtent.Text, wordToComplete);
                    bashWordArray = BuildCompWordsBashArrayString(commandText, replaceAt: cursorPosition, replacementWord: wordToComplete);
                }
                else
                {
                    bashWordArray = BuildCompWordsBashArrayString(commandText);
                }
            }
            else if (cursorPosition > commandText.Length)
            {
                cursorWordIndex++;
                commandLine = $"'{commandText} '";
                bashWordArray = $"('{commandText}' '')";
            }
            else
            {
                commandLine = $"'{commandText}'";
                bashWordArray = $"('{wordToComplete}')";
            }

            string completionCommand = BuildCompletionCommand(
                command,
                COMP_LINE: commandLine,
                COMP_WORDS: bashWordArray,
                COMP_CWORD: cursorWordIndex,
                COMP_POINT: cursorPosition,
                completerFunction,
                wordToComplete,
                previousWord);

            List<string> completionResults = [.. InvokeBashWithArguments(completionCommand)
                .Split('\n')
                .Distinct(StringComparer.Ordinal)];

            completionResults.Sort(StringComparer.Ordinal);

            string previousCompletion = null;
            foreach (string completionResult in completionResults)
            {
                if (string.IsNullOrEmpty(completionResult))
                {
                    continue;
                }

                // int equalsIndex = wordToComplete.IndexOf('=');
                int equalsIndex = wordToComplete.IndexOf(' ');

                string completionText;
                string listItemText;
                if (equalsIndex >= 0)
                {
                    completionText = string.Concat(wordToComplete.AsSpan(0, equalsIndex), completionResult);
                    listItemText = completionResult;
                }
                else
                {
                    completionText = completionResult;
                    listItemText = completionText;
                }

                if (completionText.Equals(previousCompletion))
                {
                    listItemText += " ";
                }

                previousCompletion = completionText;

                yield return new CompletionResult(
                    completionText,
                    listItemText,
                    CompletionResultType.ParameterName,
                    completionText);
            }
        }

        private string ResolveCommandCompleterFunction(string commandName)
        {
            ArgumentException.ThrowIfNullOrEmpty(commandName);

            if (_commandCompletionFunctions.TryGetValue(commandName, out string completerFunction))
            {
                return completerFunction;
            }

            string resolveCompleterInvocation = string.Format(s_resolveCompleterCommandTemplate, _completionScript, commandName);
            completerFunction = InvokeBashWithArguments(resolveCompleterInvocation).Trim();
            _commandCompletionFunctions[commandName] = completerFunction;

            return completerFunction;
        }

        private string InvokeBashWithArguments(string argumentString)
        {
            using var bashProc = new Process();

            bashProc.StartInfo.FileName = _bashPath;
            bashProc.StartInfo.Arguments = argumentString;
            bashProc.StartInfo.UseShellExecute = false;
            bashProc.StartInfo.RedirectStandardOutput = true;
            bashProc.Start();

            return bashProc.StandardOutput.ReadToEnd();
        }

        private static string EscapeCompletionResult(string completionResult)
        {
            completionResult = completionResult.Trim();

            if (!completionResult.Contains(' '))
            {
                return completionResult;
            }

            return "'" + completionResult.Replace("'", "''") + "'";
        }


        private static string BuildCompWordsBashArrayString(
            string line,
            int replaceAt = -1,
            string replacementWord = null)
        {
            // Build a bash array of line components, like "('ls' '-a')"

            string[] lineElements = line.Split();

            int approximateLength = 0;
            foreach (string element in lineElements)
            {
                approximateLength += lineElements.Length + 2;
            }

            var sb = new StringBuilder(approximateLength);

            sb.Append('(')
                .Append('\'')
                .Append(lineElements[0].Replace("'", "\\'"))
                .Append('\'');

            if (replaceAt < 1)
            {
                for (int i = 1; i < lineElements.Length; i++)
                {
                    sb.Append(' ')
                        .Append('\'')
                        .Append(lineElements[i].Replace("'", "\\'"))
                        .Append('\'');
                }
            }
            else
            {
                for (int i = 1; i < lineElements.Length; i++)
                {
                    if (i == replaceAt - 1)
                    {
                        continue;
                    }

                    if (i == replaceAt)
                    {
                        sb.Append(' ').Append(replacementWord);
                        continue;
                    }

                    sb.Append(' ')
                        .Append('\'')
                        .Append(lineElements[i].Replace("'", "\\'"))
                        .Append('\'');
                }
            }

            sb.Append(')');

            return sb.ToString();
        }

        private string BuildCompletionCommand(
            string command,
            string COMP_LINE,
            string COMP_WORDS,
            int COMP_CWORD,
            int COMP_POINT,
            string completionFunction,
            string wordToComplete,
            string previousWord)
        {
            return new StringBuilder(512)
                .Append("-lc \". ")
                .Append(_completionScript)
                .Append(" 2>/dev/null; ")
                .Append("__load_completion ").Append(command).Append(" 2>/dev/null; ")
                .Append("COMP_LINE=").Append(COMP_LINE).Append("; ")
                .Append("COMP_WORDS=").Append(COMP_WORDS).Append("; ")
                .Append("COMP_CWORD=").Append(COMP_CWORD).Append("; ")
                .Append("COMP_POINT=").Append(COMP_POINT).Append("; ")
                .Append("bind 'set completion-ignore-case on' 2>/dev/null; ")
                .Append(completionFunction)
                    .Append(" '").Append(command).Append('\'')
                    .Append(" '").Append(wordToComplete).Append('\'')
                    .Append(" '").Append(previousWord).Append("' 2>/dev/null; ")
                .Append("IFS=$'\\n'; ")
                .Append("echo \"\"\"${COMPREPLY[*]}\"\"\"\"")
                .ToString();
        }
    }
}
