$ErrorActionPreference = 'Stop'

$staging = Join-Path $PSScriptRoot '.packages\release\In Work Mode'
$archive = Join-Path $PSScriptRoot 'In Work Mode.zip'

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'In Work Mode.exe') $staging

Remove-Item $archive -Force -ErrorAction SilentlyContinue
Compress-Archive -Path $staging -DestinationPath $archive -CompressionLevel Optimal