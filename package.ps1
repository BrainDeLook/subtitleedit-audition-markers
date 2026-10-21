$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'
$files = @('Install.ps1','Install.cmd','README.md','README.ru.md','LICENSE.txt','CHANGELOG.md') | ForEach-Object {Join-Path $PSScriptRoot $_}
$files += Join-Path $dist 'AuditionMarkers.dll'
Compress-Archive -LiteralPath $files -DestinationPath (Join-Path $dist 'AuditionMarkers-1.0.0.zip') -Force
Get-FileHash -LiteralPath (Join-Path $dist 'AuditionMarkers-1.0.0.zip') -Algorithm SHA256
