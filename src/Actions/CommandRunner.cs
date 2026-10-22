using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WorkMode.Models;

namespace WorkMode.Actions
{
    internal sealed class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public int? TerminalProcessId { get; set; }
    }

    internal static class CommandRunner
    {
        public static async Task<CommandResult> RunAsync(CustomAction action, Func<bool> confirmPythonInstallation = null)
        {
            if (action.Type != CustomAction.BatchCommand && action.Type != CustomAction.PythonCommand &&
                action.Type != CustomAction.PowerShellCommand)
                throw new NotSupportedException("Unsupported action type: " + action.Type);
            if (string.IsNullOrWhiteSpace(action.Command))
                throw new ArgumentException("The command cannot be empty.");

            string directory = string.IsNullOrWhiteSpace(action.WorkingDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Environment.ExpandEnvironmentVariables(action.WorkingDirectory);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("Working folder not found: " + directory);

            string pythonExecutable = action.Type == CustomAction.PythonCommand
                ? await PythonRuntime.EnsureAvailableAsync(confirmPythonInstallation) : null;
            string extension = action.Type == CustomAction.BatchCommand ? ".bat" :
                action.Type == CustomAction.PythonCommand ? ".py" : ".ps1";
            string script = Path.Combine(Path.GetTempPath(), "WorkMode-" + Guid.NewGuid().ToString("N") + extension);
            bool terminalStarted = false;
            try
            {
                Encoding encoding = action.Type == CustomAction.BatchCommand
                    ? Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage)
                    : new UTF8Encoding(false);
                File.WriteAllText(script,
                    (action.Type == CustomAction.BatchCommand ? "@echo off\r\n" : string.Empty) + action.Command + "\r\n",
                    action.Type == CustomAction.PowerShellCommand ? new UTF8Encoding(true) : encoding);
                string executable;
                string arguments;
                if (action.Type == CustomAction.BatchCommand)
                {
                    executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                    arguments = "/d /s /c \"\"" + script + "\"\"";
                }
                else if (action.Type == CustomAction.PythonCommand)
                {
                    executable = pythonExecutable;
                    arguments = (Path.GetFileName(executable).Equals("py.exe", StringComparison.OrdinalIgnoreCase) ? "-3 " : "") +
                        "-X utf8 -u \"" + script + "\"";
                }
                else
                {
                    executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                        @"WindowsPowerShell\v1.0\powershell.exe");
                    // Hidden progress records are serialized to stderr by Windows PowerShell, even in text mode.
                    string command = (action.OpenInTerminal ? "" : "$ProgressPreference = 'SilentlyContinue'; ") +
                        "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); & '" +
                        script.Replace("'", "''") + "'; $succeeded = $?; " +
                        "if ($null -ne $LASTEXITCODE) { exit $LASTEXITCODE }; if (-not $succeeded) { exit 1 }; exit 0";
                    arguments = "-NoLogo -NoProfile -InputFormat Text -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand " +
                        Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
                }
                if (action.OpenInTerminal)
                {
                    using (var terminal = Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                        // A separate interpreter isolates `exit`; the console owns cleanup after the app closes.
                        Arguments = "/d /s " + (action.CloseTerminalAfterExecution ? "/c" : "/k") +
                            " \"\"" + executable + "\" " + arguments + " & del \"" + script + "\"\"",
                        WorkingDirectory = directory,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    }))
                    {
                        if (terminal == null) throw new InvalidOperationException("Could not open the command terminal.");
                        terminalStarted = true;
                        return new CommandResult
                        {
                            ExitCode = 0, Output = string.Empty, Error = string.Empty,
                            TerminalProcessId = terminal.Id
                        };
                    }
                }
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = arguments,
                        WorkingDirectory = directory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        StandardOutputEncoding = encoding,
                        StandardErrorEncoding = encoding
                    };
                    process.Start();
                    process.StandardInput.Close();
                    // Only wait for the batch process. `start` children can keep
                    // inherited output pipes open long after that process exits.
                    return await Task.Run(() => CaptureOutput(process, encoding)).ConfigureAwait(false);
                }
            }
            finally
            {
                if (!terminalStarted) File.Delete(script);
            }
        }

        private static CommandResult CaptureOutput(Process process, Encoding encoding)
        {
            using (var output = new MemoryStream())
            using (var error = new MemoryStream())
            {
                var buffer = new byte[8192];
                var outputPipe = (FileStream)process.StandardOutput.BaseStream;
                var errorPipe = (FileStream)process.StandardError.BaseStream;
                while (true)
                {
                    bool exited = process.HasExited;
                    ReadAvailable(outputPipe, output, buffer);
                    ReadAvailable(errorPipe, error, buffer);
                    if (exited) break;
                    Thread.Sleep(10);
                }
                return new CommandResult
                {
                    ExitCode = process.ExitCode,
                    Output = encoding.GetString(output.ToArray()),
                    Error = encoding.GetString(error.ToArray())
                };
            }
        }

        private static void ReadAvailable(FileStream pipe, Stream destination, byte[] buffer)
        {
            uint available;
            if (!Interop.NativeMethods.PeekNamedPipe(pipe.SafeFileHandle, IntPtr.Zero, 0,
                IntPtr.Zero, out available, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 109) return; // All writers closed the pipe.
                throw new Win32Exception(error);
            }
            while (available > 0)
            {
                uint read;
                if (!Interop.NativeMethods.ReadFile(pipe.SafeFileHandle, buffer,
                    Math.Min((uint)buffer.Length, available), out read, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (read == 0) throw new EndOfStreamException("The command output pipe closed unexpectedly.");
                destination.Write(buffer, 0, (int)read);
                available -= read;
            }
        }
    }
}
