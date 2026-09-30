@echo off
REM Builds "Work Mode.exe" using the C# compiler that ships with Windows (.NET Framework).
REM No SDK, no downloads, no runtime install required.

setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo Could not find the C# compiler ^(csc.exe^).
    echo Expected at: %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
    exit /b 1
)

set "ICON="
if exist "app.ico" set "ICON=/win32icon:app.ico"
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
where pwsh.exe >nul 2>&1 && set "POWERSHELL=pwsh.exe"

"%CSC%" /nologo /target:winexe /optimize+ /out:"Work Mode.exe" %ICON% ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    /recurse:src\*.cs

if %ERRORLEVEL% neq 0 (
    echo Build FAILED.
    exit /b %ERRORLEVEL%
)

"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0sign.ps1" -FilePath "%~dp0Work Mode.exe"
if %ERRORLEVEL% neq 0 (
    echo Signing FAILED.
    echo Run: powershell -ExecutionPolicy Bypass -File .\sign.ps1 -InstallCertificate
    exit /b %ERRORLEVEL%
)

echo Build and signing succeeded: Work Mode.exe
endlocal
