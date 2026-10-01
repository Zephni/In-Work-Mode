@echo off
REM Builds "In Work Mode" using the C# compiler that ships with Windows.

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

"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-dependencies.ps1"
if %ERRORLEVEL% neq 0 (
    echo Dependency restore FAILED.
    exit /b %ERRORLEVEL%
)

"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0prepare-embedded-runtime.ps1"
if %ERRORLEVEL% neq 0 (
    echo Embedded runtime preparation FAILED.
    exit /b %ERRORLEVEL%
)

"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /out:"In Work Mode.exe" %ICON% ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.IO.Compression.dll ^
    /reference:System.IO.Compression.FileSystem.dll ^
    /reference:.packages\webview2\lib\net462\Microsoft.Web.WebView2.Core.dll ^
    /reference:.packages\webview2\lib\net462\Microsoft.Web.WebView2.WinForms.dll ^
    /resource:.packages\embedded\runtime.zip,WorkMode.Resources.Runtime.zip ^
    /recurse:src\*.cs

if %ERRORLEVEL% neq 0 (
    echo Build FAILED.
    exit /b %ERRORLEVEL%
)

"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0sign.ps1" -FilePath "%~dp0In Work Mode.exe"
if %ERRORLEVEL% neq 0 (
    echo Signing FAILED.
    echo Run: powershell -ExecutionPolicy Bypass -File .\sign.ps1 -InstallCertificate
    exit /b %ERRORLEVEL%
)

echo Build succeeded: In Work Mode.exe
endlocal
