param([string]$ApplicationDirectory = 'C:\Program Files\Subtitle Edit Classic')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ApplicationDirectory).TrimEnd('\')
$target = Join-Path $root 'SubtitleEdit.exe'
$source = Join-Path $PSScriptRoot 'SubtitleEdit.exe'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'update.json') -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath $target) -or -not (Test-Path -LiteralPath (Join-Path $root 'libse.dll'))) {
    throw 'Install the normal Classic 4.0.28 installer first, or specify its directory.'
}
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $manifest.targetSha256) {throw 'Toolbar update checksum mismatch.'}
$current = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if ($current -eq $manifest.targetSha256) {Write-Output 'Toolbar support is already installed.'; exit 0}
if ($current -ne $manifest.sourceSha256) {throw 'This update is only for the exact Classic 4.0.28.4 stable EXE. The installed file was left unchanged.'}
$needsAdmin = $root.StartsWith([Environment]::GetFolderPath('ProgramFiles')+'\',[StringComparison]::OrdinalIgnoreCase)
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$admin = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($needsAdmin -and -not $admin) {
    $command = "& '"+$PSCommandPath.Replace("'","''")+"' -ApplicationDirectory '"+$root.Replace("'","''")+"'"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $process = Start-Process -FilePath "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -Verb RunAs -WindowStyle Hidden -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-EncodedCommand',$encoded) -Wait -PassThru
    exit $process.ExitCode
}
$running = Get-Process -Name SubtitleEdit -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $target}
if ($running) {throw 'Close Subtitle Edit Classic, then run this update again. No process was terminated.'}
$backup = Join-Path $root 'SubtitleEdit.exe.before-plugin-toolbar'
if (Test-Path -LiteralPath $backup) {throw 'A toolbar rollback file already exists. Keep or restore it before retrying.'}
$temp = Join-Path $root ('SubtitleEdit.plugin-update-'+[Guid]::NewGuid().ToString('N')+'.tmp')
try {
    Copy-Item -LiteralPath $source -Destination $temp
    [IO.File]::Replace($temp,$target,$backup)
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $manifest.targetSha256) {throw 'Installed EXE verification failed; rollback copy retained.'}
} finally {
    # Only our own single temporary file; no recursive deletion.
    if (Test-Path -LiteralPath $temp) {Remove-Item -LiteralPath $temp -Force}
}
Write-Output 'Classic 4.0.28.5 toolbar support installed. Settings and dependencies were retained.'
Write-Output ('Rollback EXE: '+$backup)
