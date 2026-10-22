using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WorkMode.Actions
{
    internal static class PythonRuntime
    {
        internal const string PackageId = "Python.Python.3.14";

        public static Task<string> EnsureAvailableAsync(Func<bool> confirmInstallation)
        {
            return EnsureAvailableAsync(confirmInstallation, FindExecutable, InstallAsync);
        }

        internal static async Task<string> EnsureAvailableAsync(Func<bool> confirmInstallation,
            Func<string> findExecutable, Func<Task> install)
        {
            string executable = await Task.Run(findExecutable);
            if (executable != null) return executable;
            if (confirmInstallation == null)
                throw new FileNotFoundException("Python 3 was not found. Install Python 3 or run this action from the app to install it.");
            if (!confirmInstallation())
                throw new OperationCanceledException("Python installation cancelled.");

            await install();
            executable = await Task.Run(findExecutable);
            if (executable == null)
                throw new FileNotFoundException("Python installation finished, but a working Python 3 interpreter was not found. " +
                    "Install Python 3 from https://www.python.org/downloads/windows/ with 'Add python.exe to PATH' enabled, then retry.");
            return executable;
        }

        public static string FindExecutable()
        {
            // The running app retains its old PATH after an installer updates the user's PATH.
            return FindExecutable(Environment.GetEnvironmentVariable("PATH") + ";" +
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) + ";" +
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine));
        }

        internal static string FindExecutable(string searchPath)
        {
            foreach (string candidate in FindOnPath(searchPath, new[] { "py.exe", "python.exe" }))
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = (Path.GetFileName(candidate).Equals("py.exe", StringComparison.OrdinalIgnoreCase) ? "-3 " : "") +
                            "-I -S -X utf8 -c \"import sys; print(sys.executable if sys.version_info[0] == 3 else '')\"",
                        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    };
                    process.Start();
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(10000))
                    {
                        process.Kill();
                        process.WaitForExit();
                        throw new TimeoutException("Timed out checking Python: " + candidate);
                    }
                    Task.WaitAll(output, error);
                    string executable = output.Result.Trim();
                    if (process.ExitCode == 0 && Path.IsPathRooted(executable) && File.Exists(executable))
                        return executable;
                }
            }
            return null;
        }

        private static IEnumerable<string> FindOnPath(string searchPath, string[] names)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
                foreach (string entry in (searchPath ?? string.Empty).Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    string candidate = Path.Combine(Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"')), name);
                    if (seen.Add(candidate) && File.Exists(candidate)) yield return candidate;
                }
        }

        private static Task InstallAsync()
        {
            string searchPath = Environment.GetEnvironmentVariable("PATH") + ";" +
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\WindowsApps");
            foreach (string executable in FindOnPath(searchPath, new[] { "winget.exe" }))
                return InstallAsync(executable);
            throw new FileNotFoundException("Windows Package Manager (winget) was not found. " +
                "Install or update App Installer from the Microsoft Store, or install Python 3 from " +
                "https://www.python.org/downloads/windows/ with 'Add python.exe to PATH' enabled, then retry.");
        }

        internal static Task InstallAsync(string executable)
        {
            return Task.Run(() =>
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "install --id " + PackageId + " --exact --source winget --scope user " +
                        "--accept-package-agreements --accept-source-agreements --disable-interactivity",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                }))
                {
                    if (process == null) throw new InvalidOperationException("Could not start the Python installer.");
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException("Python installation failed or was cancelled. Windows Package Manager exit code: " +
                            process.ExitCode + " (0x" + process.ExitCode.ToString("X8") + "). " +
                            "Check your internet connection and App Installer, or install Python 3 from " +
                            "https://www.python.org/downloads/windows/ with 'Add python.exe to PATH' enabled, then retry.");
                }
            });
        }
    }
}
