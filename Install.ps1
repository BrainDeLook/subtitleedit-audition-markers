param([string]$PluginDirectory = (Join-Path $env:APPDATA 'Subtitle Edit\Plugins'))
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot 'AuditionMarkers.dll'
if (-not (Test-Path -LiteralPath $dll)) {$dll = Join-Path $PSScriptRoot 'dist\AuditionMarkers.dll'}
if (-not (Test-Path -LiteralPath $dll)) {throw 'AuditionMarkers.dll was not found next to Install.ps1 or in dist.'}
New-Item -ItemType Directory -Path $PluginDirectory -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $PluginDirectory 'AuditionMarkers.dll') -Force
Write-Output "Installed in $PluginDirectory"
Write-Output 'Restart Subtitle Edit Classic. The command appears under Tools. The toolbar button requires a Classic build with plugin toolbar support.'
