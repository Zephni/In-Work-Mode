$ErrorActionPreference = 'Stop'

$packages = Join-Path $PSScriptRoot '.packages'
$webView2 = Join-Path $packages 'webview2'
$vditor = Join-Path $packages 'vditor\package'

New-Item -ItemType Directory -Force $packages | Out-Null

if (-not (Test-Path (Join-Path $webView2 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'))) {
    $archive = Join-Path $packages 'webview2.zip'
    Invoke-WebRequest `
        'https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/1.0.4258.31' `
        -OutFile $archive
    Expand-Archive -Path $archive -DestinationPath $webView2 -Force
    Remove-Item $archive
}

if (-not (Test-Path (Join-Path $vditor 'dist\index.min.js'))) {
    $archive = Join-Path $packages 'vditor.tgz'
    $destination = Join-Path $packages 'vditor'
    New-Item -ItemType Directory -Force $destination | Out-Null
    Invoke-WebRequest `
        'https://registry.npmjs.org/vditor/-/vditor-4.0.0.tgz' `
        -OutFile $archive
    & tar.exe -xzf $archive -C $destination
    if ($LASTEXITCODE -ne 0) { throw 'Could not extract the Vditor package.' }
    Remove-Item $archive
}