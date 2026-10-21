$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$output = Join-Path ([IO.Path]::GetTempPath()) ("WorkMode-tests-" + [Guid]::NewGuid().ToString('N') + '.exe')
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse | ForEach-Object FullName)
$sources += Join-Path $PSScriptRoot 'tests\RegressionTests.cs'
$references = @(
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Core.dll',
    '/reference:System.IO.Compression.dll',
    '/reference:System.IO.Compression.FileSystem.dll',
    ('/reference:' + (Join-Path $PSScriptRoot '.packages\webview2\lib\net462\Microsoft.Web.WebView2.Core.dll')),
    ('/reference:' + (Join-Path $PSScriptRoot '.packages\webview2\lib\net462\Microsoft.Web.WebView2.WinForms.dll'))
)
try {
    & $compiler /nologo /target:exe /platform:x64 /main:RegressionTests "/out:$output" @references @sources
    if ($LASTEXITCODE -ne 0) { throw 'Regression test compilation failed. Run build.bat first if dependencies are missing.' }
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
}
finally {
    if (Test-Path $output) { Remove-Item -LiteralPath $output }
}
