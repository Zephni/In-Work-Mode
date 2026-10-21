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
    }

    internal static class CommandRunner
    {
        public static async Task<CommandResult> RunAsync(CustomAction action)
        {
            if (action.Type != CustomAction.BatchCommand)
                throw new NotSupportedException("Unsupported action type: " + action.Type);
            if (string.IsNullOrWhiteSpace(action.Command))
                throw new ArgumentException("The command cannot be empty.");

            string directory = string.IsNullOrWhiteSpace(action.WorkingDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Environment.ExpandEnvironmentVariables(action.WorkingDirectory);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("Working folder not found: " + directory);

            string script = Path.Combine(Path.GetTempPath(), "WorkMode-" + Guid.NewGuid().ToString("N") + ".bat");
            try
            {
                // A script preserves cmd quoting, pipes and multiple command lines.
                Encoding encoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                File.WriteAllText(script, "@echo off\r\n" + action.Command + "\r\n", encoding);
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                        Arguments = "/d /s /c \"\"" + script + "\"\"",
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
                File.Delete(script);
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
