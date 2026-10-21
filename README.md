# In Work Mode

## Download
[https://raw.githubusercontent.com/Zephni/In-Work-Mode/main/In%20Work%20Mode.exe](https://raw.githubusercontent.com/Zephni/In-Work-Mode/main/In%20Work%20Mode.exe)

## Tabs

- **Time Tracking** contains the existing workspaces, timers and notes.
- **Custom Actions** lets you add, edit, delete and run named commands. The default
  action type is **.bat command**. Enter batch commands, or a quoted batch-file
  path such as `"C:\Scripts\Start work.bat"`. Use `call` when invoking a batch
  file before additional commands.

An optional working folder controls where the command runs; leaving it blank
uses the application's folder. Commands run hidden with your current user
permissions, without interactive input. Only run commands you trust. Actions
are saved in `%AppData%\WorkMode\config.ini` alongside your workspaces.

While a command runs, action buttons are disabled to prevent duplicate runs.
A successful run shows a check icon for 3.5 seconds. Launch failures, non-zero
exit codes and standard-error output open a copyable error-details window.
For multi-line commands, use `exit /b` or `&&` to propagate intermediate failures;
batch scripts otherwise report the exit status of their last command.

### Opening independent command windows

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
