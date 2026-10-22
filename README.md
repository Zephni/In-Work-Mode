# In Work Mode

## Download
[https://raw.githubusercontent.com/Zephni/In-Work-Mode/main/In%20Work%20Mode.exe](https://raw.githubusercontent.com/Zephni/In-Work-Mode/main/In%20Work%20Mode.exe)

## Tabs

- **Time Tracking** contains the existing workspaces, timers and notes.
- **Custom Actions** lets you add, edit, delete and run named scripts. Select a
  **Language** (Batch, Python 3, or PowerShell), then independently select a
  **Display** mode (**Open in terminal**, **Terminal (close after execution)**, or
  **Run in background**). New actions
  default to Batch in a visible terminal. Cards show both selections.

### Script languages

- **Batch** runs with Windows `cmd.exe`. Paste batch commands or a quoted
  batch-file path such as `"C:\Scripts\Start work.bat"`. Use `call` when invoking
  a batch file before additional commands.
- **Python 3** runs Python source, such as `print("Hello")`. The app checks for a
  working Python 3 interpreter using `py.exe` or `python.exe` on `PATH` (the Python
  launcher `py -3` takes priority). Store shortcuts without an installed interpreter
  do not count. Python is not bundled. If Python is missing, running a Python action
  offers to install Python 3.14 for your Windows user using Windows Package Manager
  (`winget`, supplied by Microsoft's App Installer). Installation requires internet
  access; confirming accepts the package and winget source agreements. A visible
  installation window opens, and action buttons remain disabled until it finishes.
  After successful installation, the app checks the updated user/machine `PATH`
  and continues with the original action without requiring an app restart.
  Declining leaves the action unrun. Installation failures appear in the error-details
  window. If winget is unavailable, install/update App Installer from the Microsoft
  Store, or install Python 3 manually with **Add python.exe to PATH** enabled.
  Existing working Python 3 installations are reused and are not upgraded.
  To run an existing file, use
  `import runpy; runpy.run_path(r"C:\Scripts\work.py", run_name="__main__")`.
- **PowerShell** uses Windows PowerShell, included with Windows. Paste PowerShell
  source, such as `Get-ChildItem`, or invoke a file with
  `& 'C:\Scripts\work.ps1'`. It runs without profiles and with execution policy
  bypass for that process only; it does not change the machine's policy.

All three languages support all display modes, multiline scripts and an optional
working folder. Python and PowerShell scripts use UTF-8. Hidden scripts have no
interactive input; use a visible terminal for `input()`, `Read-Host`, or prompts.

An optional working folder controls where the command runs; leaving it blank
uses the application's folder. New actions default to **Open in terminal** for an
interactive command window that stays open afterward. Select
**Terminal (close after execution)** for an interactive window that closes when
the script exits, including on errors. This works for all three languages. For a
Python menu, returning from `main()` via `raise SystemExit(main())` closes the
window in this mode; no terminal-closing code is needed in the script.
Select **Run in background** to run hidden without
interactive input. Existing actions retain their saved mode; older actions without
a mode remain hidden. All modes use your current user permissions. Only run commands you trust. Actions
are saved in `%AppData%\WorkMode\config.ini` alongside your workspaces.

While a background command runs, action buttons are disabled to prevent duplicate runs.
A successful run shows a check icon for 3.5 seconds. Launch failures, non-zero
exit codes and standard-error output open a copyable error-details window.
For multi-line batch commands, use `exit /b` or `&&` to propagate intermediate
failures; batch scripts otherwise report the exit status of their last command.
Python can return a failure with `sys.exit(code)`, and PowerShell with `exit code`.
For PowerShell scripts that must stop on non-terminating errors, set
`$ErrorActionPreference = 'Stop'`.

### Opening independent command windows

For a complete interactive batch script (such as an SSH server selection menu),
select **Batch** and **Open in terminal**, then paste the entire script into the script field.
There is no need to save a separate batch file or wrap the script in `start`.
The new console supports `choice`, `set /p`, and SSH password/host-key prompts.
It uses the selected working folder. **Open in terminal** stays open after the
script ends, including on errors. Use `exit /b` to end the script; type `exit`
afterward to close the console. Choose **Terminal (close after execution)** instead
to close automatically after the script ends. Errors may disappear immediately in
this mode; use **Open in terminal** when troubleshooting.

Terminal actions run independently and survive closing In Work Mode. The check
icon and **Opened in terminal** feedback confirm only that the console launched,
not that the script succeeded. Output and script errors appear in that console
instead of the app's error dialog. You can launch another action immediately.

The app creates a temporary `.bat`, `.py`, or `.ps1` file automatically. Hidden
actions delete it on completion. For visible actions, the terminal deletes it when
the script finishes, even if the app has closed or the script uses `exit`. Closing
the terminal forcibly before the script ends can leave that temporary file in
your Windows temp folder.

For hidden actions that launch multiple separate consoles, you can still use
`start` explicitly:

Batch commands run sequentially. A long-running command such as `ssh -N` never
finishes, so later lines will not run. Use Windows' `start` command to launch
separate consoles without waiting:

```bat
start "First tunnel" cmd /k "ssh -N -L 3307:localhost:3306 user@first-server <CON >CON 2>&1"
start "Second tunnel" cmd /k "ssh -N -L 3308:localhost:3306 user@second-server <CON >CON 2>&1"
```

The quoted text after `start` is the window title. `cmd /k` keeps that console
open so authentication prompts, connection errors and later disconnections are
visible. Close each console (or press Ctrl+C) to stop its command. These windows
are independent of In Work Mode and stay open if you close the application.
In Work Mode no longer prevents closing while an action runs, and does not
terminate launched processes.

`<CON >CON 2>&1` routes the launched command's input, output and errors to its
own console instead of inheriting the app's capture pipes. This allows SSH
password/host-key prompts and connection errors to be shown in that window.

For commands launched with `start`, the check icon confirms the launch script
completed, not that the service or tunnel is ready. Errors from those independent
commands appear in their own consoles, not the app's error dialog.

Custom actions use the same rounded cards, icon buttons and dashed add card as
Time Tracking. Both tabs have icons. The add/edit action dialog can be resized
or maximized; its command field expands with it.

## Build and test

Run `build.bat` to build and sign the application. After building, run
`powershell -ExecutionPolicy Bypass -File .\test.ps1` for command execution,
configuration round-trip and Windows Forms regression tests. Tests use temporary
configuration files and do not modify your saved workspaces or actions.
