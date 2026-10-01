$ErrorActionPreference = 'Stop'

$webView2 = Join-Path $PSScriptRoot '.packages\webview2'
$vditor = Join-Path $PSScriptRoot '.packages\vditor\package'
$embedded = Join-Path $PSScriptRoot '.packages\embedded'
$runtime = Join-Path $embedded 'runtime'
$editor = Join-Path $runtime 'editor'
$archive = Join-Path $embedded 'runtime.zip'

Remove-Item $embedded -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $editor 'vditor\dist') | Out-Null

Copy-Item (Join-Path $webView2 'lib\net462\Microsoft.Web.WebView2.Core.dll') $runtime
Copy-Item (Join-Path $webView2 'lib\net462\Microsoft.Web.WebView2.WinForms.dll') $runtime
Copy-Item (Join-Path $webView2 'runtimes\win-x64\native\WebView2Loader.dll') $runtime
Copy-Item (Join-Path $PSScriptRoot 'src\UI\NotesEditor.html') (Join-Path $editor 'index.html')
Copy-Item (Join-Path $vditor 'dist\*') (Join-Path $editor 'vditor\dist') -Recurse -Force
Copy-Item (Join-Path $vditor 'LICENSE') (Join-Path $editor 'vditor\LICENSE') -Force

Compress-Archive -Path (Join-Path $runtime '*') -DestinationPath $archive -CompressionLevel Optimal