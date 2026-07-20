@echo off
REM Builds WorkMode.exe using the C# compiler that ships with Windows (.NET Framework).
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

"%CSC%" /nologo /target:winexe /optimize+ /out:WorkMode.exe %ICON% ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    /recurse:src\*.cs

if %ERRORLEVEL% neq 0 (
    echo Build FAILED.
    exit /b %ERRORLEVEL%
)

echo Build succeeded: WorkMode.exe
endlocal
