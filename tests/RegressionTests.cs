using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WorkMode.Actions;
using WorkMode.Configuration;
using WorkMode.Models;
using WorkMode.UI;

internal static class RegressionTests
{
    private static int _assertions;

    [STAThread]
    private static int Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "WorkMode tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            TestCommands(directory);
            TestTerminalCommands(directory);
            TestConfiguration(directory);
            Application.EnableVisualStyles();
            Control.CheckForIllegalCrossThreadCalls = true;
            TestInterface(directory);
            Console.WriteLine("Passed " + _assertions + " assertions.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _assertions++;
    }

    private static void TestCommands(string directory)
    {
        var action = new CustomAction { Title = "Test", WorkingDirectory = directory, Command = "echo Hello\r\necho %CD%" };
        CommandResult result = CommandRunner.RunAsync(action).GetAwaiter().GetResult();
        Assert(result.ExitCode == 0 && result.Output.Contains("Hello"), "Successful command output");
        Assert(result.Output.Contains(directory), "Working folder applied");
        Assert(result.Error.Length == 0, "No unexpected stderr");

        string script = Path.Combine(directory, "script with spaces.bat");
        File.WriteAllText(script, "@echo off\r\necho Quoted script\r\nexit /b 0\r\n");
        action.Command = "\"" + script + "\"";
        result = CommandRunner.RunAsync(action).GetAwaiter().GetResult();
        Assert(result.ExitCode == 0 && result.Output.Contains("Quoted script"), "Quoted batch path");

        action.Command = "echo Failure details 1>&2\r\nexit /b 7";
        result = CommandRunner.RunAsync(action).GetAwaiter().GetResult();
        Assert(result.ExitCode == 7 && result.Error.Contains("Failure details"), "Exit code and stderr captured");
        action.Command = "workmode_nonexistent_command_72656";
        result = CommandRunner.RunAsync(action).GetAwaiter().GetResult();
        Assert(result.ExitCode != 0 && result.Error.Length > 0, "Invalid command reported");
        action.Command = "echo out & echo err 1>&2";
        result = CommandRunner.RunAsync(action).GetAwaiter().GetResult();
        Assert(result.ExitCode == 0 && result.Error.Contains("err"), "Stderr captured even on exit zero");

        // Both streams exceed their pipe buffers; reading them sequentially would deadlock.
        action.Command = "for /L %%i in (1,1,1500) do @echo Output %%i & @echo Error %%i 1>&2";
        result = CommandRunner.RunAsync(action).GetAwaiter().GetResult();
        Assert(result.Output.Length > 10000 && result.Error.Length > 10000, "Both output streams drained");

        string first = Path.Combine(directory, "first-console.bat");
        string second = Path.Combine(directory, "second-console.bat");
        File.WriteAllText(first, "@echo off\r\nping -n 4 127.0.0.1 >nul\r\necho %~1 > first-complete.txt\r\necho Console-only output\r\n");
        File.WriteAllText(second, "@echo off\r\nping -n 4 127.0.0.1 >nul\r\necho second > second-complete.txt\r\n");
        action.Command = "start \"First test console\" cmd /c \"call \"" + first + "\" \"argument with spaces\" <CON >CON 2>&1\"\r\n" +
            "start \"Second test console\" cmd /c \"\"" + second + "\"\"\r\necho Launches complete";
        Task<CommandResult> launched = CommandRunner.RunAsync(action);
        bool promptLaunch = launched.Wait(2000);
        if (!promptLaunch) launched.Wait(10000);
        Assert(launched.Result.ExitCode == 0, "Both console launches succeeded");
        Assert(launched.Result.Output.Contains("Launches complete"), "Final parent output captured before returning");
        Assert(!File.Exists(Path.Combine(directory, "first-complete.txt")), "First launched command still runs independently");
        var completion = Stopwatch.StartNew();
        while ((!File.Exists(Path.Combine(directory, "first-complete.txt")) ||
            !File.Exists(Path.Combine(directory, "second-complete.txt"))) && completion.ElapsedMilliseconds < 10000)
            Thread.Sleep(20);
        Assert(File.Exists(Path.Combine(directory, "first-complete.txt")) &&
            File.Exists(Path.Combine(directory, "second-complete.txt")), "Both independent consoles completed");
        Assert(File.ReadAllText(Path.Combine(directory, "first-complete.txt")).Trim() == "argument with spaces",
            "Console command preserves quoted paths and arguments with console I/O redirection");
        Assert(promptLaunch, "start launches both consoles without waiting for their commands to finish");

        action.WorkingDirectory = Path.Combine(directory, "missing");
        try
        {
            CommandRunner.RunAsync(action).GetAwaiter().GetResult();
            throw new Exception("Missing working folder was accepted.");
        }
        catch (DirectoryNotFoundException) { _assertions++; }
        action.Type = "future";
        try
        {
            CommandRunner.RunAsync(action).GetAwaiter().GetResult();
            throw new Exception("Unsupported action type was accepted.");
        }
        catch (NotSupportedException) { _assertions++; }
    }

    private static void TestTerminalCommands(string directory)
    {
        var action = new CustomAction
        {
            Title = "Interactive terminal", WorkingDirectory = directory, OpenInTerminal = true,
            Command = "echo %~f0>terminal-script.txt\r\n" +
                "powershell.exe -NoProfile -Command \"[IO.File]::WriteAllText('console-io.txt', [Console]::IsInputRedirected.ToString() + ',' + [Console]::IsOutputRedirected.ToString())\"\r\n" +
                "choice /c 12 /t 2 /d 2 /n\r\n" +
                "if errorlevel 2 goto selected\r\nexit /b 9\r\n" +
                ":selected\r\necho %CD%>terminal-complete.txt\r\nexit /b 7"
        };
        var watch = Stopwatch.StartNew();
        CommandResult result = CommandRunner.RunAsync(action).GetAwaiter().GetResult();
        Assert(result.TerminalProcessId.HasValue && result.ExitCode == 0 && result.Error.Length == 0,
            "Terminal launch returns success and an independent process");
        using (var terminal = Process.GetProcessById(result.TerminalProcessId.Value))
        {
            try
            {
                Assert(watch.ElapsedMilliseconds < 2000 && !File.Exists(Path.Combine(directory, "terminal-complete.txt")),
                    "Launching a terminal does not wait for its script");
                PumpUntil(() => File.Exists(Path.Combine(directory, "console-io.txt")), 10000, "console I/O check");
                string script = File.ReadAllText(Path.Combine(directory, "terminal-script.txt")).Trim();
                Assert(File.Exists(script), "Temporary batch survives while the script is running");
                Assert(File.ReadAllText(Path.Combine(directory, "console-io.txt")) == "False,False",
                    "Terminal input and output are attached to a real console");
                PumpUntil(() => File.Exists(Path.Combine(directory, "terminal-complete.txt")) && !File.Exists(script),
                    10000, "terminal script completion and cleanup");
                Assert(File.ReadAllText(Path.Combine(directory, "terminal-complete.txt")).Trim() == directory,
                    "Interactive choice, goto, and working folder work in the new terminal");
                Assert(!terminal.HasExited, "Terminal remains open after a non-zero script exit");
            }
            finally
            {
                if (!terminal.HasExited) terminal.Kill();
                terminal.WaitForExit();
            }
        }
    }

    private static void TestConfiguration(string directory)
    {
        string path = Path.Combine(directory, "config.ini");
        File.WriteAllText(path, "[Window]\r\nWidth=740\r\nHeight=420\r\n[Workspace]\r\nTitle=Existing\r\nSeconds=42\r\nNotes=Notes\r\n" +
            "[CustomAction]\r\nTitle=Legacy action\r\nCommand=echo Legacy\r\n");
        var config = AppConfig.Load(path);
        Assert(config.Workspaces.Count == 1 && config.Workspaces[0].ElapsedSeconds == 42, "Old configuration loaded");
        Assert(!config.CustomActions[0].OpenInTerminal, "Existing actions remain hidden by default");
        config.CustomActions.Clear();
        config.CustomActions.Add(new CustomAction
        {
            Title = "Build = test", Command = "echo C:\\new\\folder\r\necho [Workspace]\r\nexit /b 3",
            WorkingDirectory = directory, OpenInTerminal = true
        });
        config.CustomActions.Add(new CustomAction { Title = "Second", Type = "future", Command = "echo second" });
        config.SaveOrThrow();
        var loaded = AppConfig.Load(path);
        Assert(loaded.Workspaces.Count == 1 && loaded.Workspaces[0].Notes == "Notes", "Workspace preserved");
        Assert(loaded.WindowWidth == 740 && loaded.WindowHeight == 420, "Window settings preserved");
        Assert(loaded.CustomActions.Count == 2, "Multiple actions persisted including last section");
        Assert(loaded.CustomActions[0].Command == config.CustomActions[0].Command, "Multiline command and backslashes round trip");
        Assert(loaded.CustomActions[0].WorkingDirectory == directory, "Working folder round trip");
        Assert(loaded.CustomActions[0].OpenInTerminal && !loaded.CustomActions[1].OpenInTerminal,
            "Terminal and hidden modes round trip independently");
        Assert(loaded.CustomActions[1].Type == "future", "Unknown action type preserved");
        loaded.CustomActions.RemoveAt(0);
        loaded.CustomActions[0].Title = "Edited";
        loaded.SaveOrThrow();
        loaded = AppConfig.Load(path);
        Assert(loaded.CustomActions.Count == 1 && loaded.CustomActions[0].Title == "Edited", "Edits and deletes persisted");
        try
        {
            new AppConfig(directory).SaveOrThrow();
            throw new Exception("An unwritable configuration path was accepted.");
        }
        catch (UnauthorizedAccessException) { _assertions++; }
        catch (IOException) { _assertions++; }
    }

    private static T Find<T>(Control root, Func<T, bool> predicate) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            T match = child as T;
            if (match != null && predicate(match)) return match;
            match = Find(child, predicate);
            if (match != null) return match;
        }
        return null;
    }

    private static void PumpUntil(Func<bool> condition, int timeout, string state)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < timeout)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Assert(condition(), "Timed out waiting for " + state);
    }

    private static void TestInterface(string directory)
    {
        var config = new AppConfig(Path.Combine(directory, "ui.ini"));
        var workspace = new Workspace("Timer keeps running", 0);
        workspace.Start();
        config.Workspaces.Add(workspace);
        config.CustomActions.Add(new CustomAction
        {
            Title = "Quick action", Command = "echo Done", WorkingDirectory = directory
        });
        using (var form = new MainForm(config))
        {
            Exception failure = null;
            form.Shown += (sender, args) => form.BeginInvoke((Action)(() =>
            {
                try
                {
                    Button tracking = Find<Button>(form, b => b.Text == "Time Tracking");
                    Button actions = Find<Button>(form, b => b.Text == "Custom Actions");
                    CustomActionsControl page = Find<CustomActionsControl>(form, c => true);
                    Assert(tracking != null && actions != null && !page.Visible, "Time Tracking selected by default");
                    Assert(tracking.Image != null && actions.Image != null, "Both tab labels have icons");
                    actions.PerformClick();
                    Application.DoEvents();
                    Assert(page.Visible && workspace.IsRunning, "Switching tabs preserves running timer");
                    CustomActionRow row = Find<CustomActionRow>(page, c => true);
                    Button run = row.RunButton;
                    Assert(row.Height == 56 && Find<AddWorkspaceRow>(page, c => c.Text == "Add Custom Action") != null,
                        "Custom actions use workspace-sized cards and the shared add card");
                    int uiThread = Thread.CurrentThread.ManagedThreadId;
                    Find<Label>(row, c => c.Text == CustomAction.BackgroundCommandName).TextChanged += (s, e) => Assert(Thread.CurrentThread.ManagedThreadId == uiThread,
                        "Run feedback must be updated on the UI thread");
                    run.PerformClick();
                    Assert(page.IsRunning && !run.Enabled, "Running state disables duplicate execution");
                    PumpUntil(() => !page.IsRunning, 10000, "first command completion");
                    Assert(run.Image != null && row.StatusText == "Ran successfully" && run.Enabled, "Success icon and confirmation");
                    Thread.Sleep(500);
                    run.PerformClick();
                    PumpUntil(() => !page.IsRunning, 10000, "second command completion");
                    Assert(run.Image != null, "Immediate rerun has a success icon");
                    var feedbackTime = Stopwatch.StartNew();
                    PumpUntil(() => row.StatusText == CustomAction.BackgroundCommandName, 5000, "success icon to clear");
                    Assert(feedbackTime.ElapsedMilliseconds >= 3400, "Success feedback lasts 3.5 seconds");
                    Assert(run.Image != null && row.StatusText == CustomAction.BackgroundCommandName, "Success feedback resets to play icon after interval");
                    workspace.Sync();
                    Assert(workspace.ElapsedSeconds >= 3, "Timer advances while Custom Actions is active");

                    config.CustomActions[0].Command = "echo UI error details 1>&2\r\nexit /b 8";
                    RunExpectError(page, run, "Exit code: 8", "UI error details");
                    config.CustomActions[0].Command = "echo Warning on stderr 1>&2\r\nexit /b 0";
                    RunExpectError(page, run, "Exit code: 0", "Warning on stderr");
                    config.CustomActions[0].WorkingDirectory = Path.Combine(directory, "missing");
                    RunExpectError(page, run, "Working folder not found", "missing");
                    config.CustomActions[0].WorkingDirectory = directory;
                    config.CustomActions[0].Command = "echo Done";

                    using (var dialog = new EditActionForm(null))
                    {
                        dialog.Show(form);
                        Application.DoEvents();
                        ComboBox type = Find<ComboBox>(dialog, c => true);
                        Assert(type.SelectedIndex == 0 && type.Text == "Open in terminal",
                            "Open in terminal is the first and default option for new actions");
                        Assert(type.Items.Count == 2 && type.Items[1].ToString() == "Run in background",
                            "Background execution has a descriptive second option");
                        Assert(Find<CheckBox>(dialog, c => true) == null, "Mode is selected through one dropdown");
                        Assert(dialog.BackColor == Theme.Background, "Action editor uses the dark theme");
                        TextBox command = Find<TextBox>(dialog, c => c.Multiline);
                        TextBox name = Find<TextBox>(dialog, c => !c.Multiline && !c.Anchor.HasFlag(AnchorStyles.Bottom));
                        Assert(type.Font.Name == name.Font.Name && type.Font.Size == name.Font.Size &&
                            type.BackColor == name.BackColor && type.ForeColor == name.ForeColor,
                            "Dropdown matches standard input typography and colors");
                        Assert(type.DrawMode == DrawMode.OwnerDrawFixed && type.ItemHeight >= type.Font.Height + 6 &&
                            type.Height >= name.Height && type.Bottom < command.Top - 20,
                            "Dropdown has padded rows and does not overlap the command label");
                        type.DroppedDown = true;
                        Application.DoEvents();
                        type.DroppedDown = false;
                        Size commandSize = command.Size;
                        TextBox directoryBox = Find<TextBox>(dialog, c => !c.Multiline && c.Anchor.HasFlag(AnchorStyles.Bottom));
                        int folderTop = directoryBox.Top;
                        dialog.ClientSize = new Size(dialog.ClientSize.Width + 200, dialog.ClientSize.Height + 150);
                        Application.DoEvents();
                        Assert(dialog.FormBorderStyle == FormBorderStyle.Sizable && dialog.MaximizeBox,
                            "Action editor is resizable and maximizable");
                        Assert(command.Width == commandSize.Width + 200 && command.Height == commandSize.Height + 150,
                            "Command input grows in both dimensions");
                        Assert(directoryBox.Top == folderTop + 150 && command.Bottom < directoryBox.Top,
                            "Folder and buttons remain below the growing command input");
                        name.Text = "SSH menu";
                        command.Text = "echo Interactive";
                        Find<Button>(dialog, b => b.Text == "Save").PerformClick();
                        Assert(dialog.Action != null && dialog.Action.OpenInTerminal,
                            "Action editor saves the terminal option");
                        dialog.Close();
                    }
                    using (var dialog = new EditActionForm(new CustomAction { OpenInTerminal = true }))
                    {
                        Assert(Find<ComboBox>(dialog, c => true).SelectedIndex == 0, "Editing restores the terminal option");
                    }
                    using (var dialog = new EditActionForm(config.CustomActions[0]))
                    {
                        dialog.Show(form);
                        ComboBox type = Find<ComboBox>(dialog, c => true);
                        Assert(type.SelectedIndex == 1, "Editing preserves existing background actions");
                        type.SelectedIndex = 0;
                        Assert(Find<Label>(dialog, c => c.Text.StartsWith("Interactive input")) != null,
                            "Terminal selection shows the interactive hint");
                        type.SelectedIndex = 1;
                        Assert(Find<Label>(dialog, c => c.Text.StartsWith("Runs hidden")) != null,
                            "Background selection shows the hidden execution hint");
                        Find<Button>(dialog, b => b.Text == "Save").PerformClick();
                        Assert(dialog.Action != null && !dialog.Action.OpenInTerminal &&
                            dialog.Action.Type == CustomAction.BatchCommand,
                            "Background option saves the existing batch action format");
                    }
                    using (var dialog = new EditActionForm(new CustomAction { Type = "future" }))
                    {
                        ComboBox type = Find<ComboBox>(dialog, c => true);
                        Assert(type.SelectedIndex == 2 && type.Text == "Unsupported: future",
                            "Unknown action types are not silently converted to a supported mode");
                    }
                    using (var terminalRow = new CustomActionRow(new CustomAction { OpenInTerminal = true }))
                    {
                        Assert(terminalRow.StatusText == "Open in terminal", "Terminal actions use the same descriptive name on cards");
                        terminalRow.SetSuccess(true);
                        Assert(terminalRow.StatusText == "Opened in terminal", "Terminal feedback confirms launch only");
                    }

                    string screenshot = Environment.GetEnvironmentVariable("WORKMODE_TEST_SCREENSHOT");
                    if (!string.IsNullOrEmpty(screenshot))
                    {
                        run.PerformClick();
                        PumpUntil(() => !page.IsRunning, 10000, "screenshot command completion");
                        using (var bitmap = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                            bitmap.Save(screenshot);
                        }
                    }
                    form.Size = new Size(360, 220);
                    Application.DoEvents();
                    Assert(run.Width > 0 && page.BackColor == Theme.Background, "Compact dark layout");
                    tracking.PerformClick();
                    Assert(!page.Visible, "Time Tracking can be restored");
                    form.Close();
                }
                catch (Exception error)
                {
                    failure = error;
                    form.Dispose();
                    Application.ExitThread();
                }
            }));
            Application.Run(form);
            if (failure != null) throw new Exception("UI regression failed.", failure);
        }
        var saved = AppConfig.Load(Path.Combine(directory, "ui.ini"));
        Assert(saved.CustomActions.Count == 1 && saved.Workspaces.Count == 1, "Closing preserves actions and timers");
        TestClosingWhileRunning(directory);
    }

    private static void RunExpectError(CustomActionsControl page, Button run, string expectedCode, string expectedDetails)
    {
        string details = null;
        using (var closeDialog = new System.Windows.Forms.Timer { Interval = 50 })
        {
            closeDialog.Tick += (sender, args) =>
            {
                Form errorDialog = null;
                foreach (Form open in Application.OpenForms)
                    if (open.Text == "Quick action") errorDialog = open;
                if (errorDialog == null) return;
                TextBox output = Find<TextBox>(errorDialog, c => c.ReadOnly);
                details = output == null ? string.Empty : output.Text;
                errorDialog.Close();
            };
            closeDialog.Start();
            run.PerformClick();
            PumpUntil(() => !page.IsRunning, 10000, "error feedback");
        }
        Assert(details != null && details.Contains(expectedCode) && details.Contains(expectedDetails),
            "Error dialog includes actionable details: " + expectedDetails);
        Assert(Find<CustomActionRow>(page, c => true).StatusText == "Error running command" && run.Enabled,
            "Failure never displays success and permits retry");
    }

    private static void TestClosingWhileRunning(string directory)
    {
        var config = new AppConfig(Path.Combine(directory, "closing.ini"));
        config.CustomActions.Add(new CustomAction
        {
            Title = "Independent command", WorkingDirectory = directory,
            Command = "ping -n 3 127.0.0.1 >nul\r\necho Finished > after-close.txt"
        });
        using (var form = new MainForm(config))
        {
            form.Shown += (s, e) => form.BeginInvoke((Action)(() =>
            {
                Find<Button>(form, b => b.Text == "Custom Actions").PerformClick();
                Find<CustomActionRow>(form, c => true).RunButton.PerformClick();
                Assert(Find<CustomActionsControl>(form, c => true).IsRunning, "Command active before closing");
                form.Close();
            }));
            Application.Run(form);
            Assert(form.IsDisposed, "Application can close while commands are running");
            PumpUntil(() => File.Exists(Path.Combine(directory, "after-close.txt")), 10000,
                "independent command completion after closing");
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 250)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
        }
    }
}
