param([string]$ClassicDirectory = 'C:\Program Files\Subtitle Edit Classic', [switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$core = Join-Path $ClassicDirectory 'libse.dll'
if (-not (Test-Path -LiteralPath $core)) {throw 'Pass -ClassicDirectory pointing to an installed Subtitle Edit Classic.'}
& $compiler /nologo /target:library /optimize+ ('/out:'+(Join-Path $out 'AuditionMarkers.dll')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll ('/reference:'+$core) (Join-Path $PSScriptRoot 'src/AuditionMarkers.cs')
if ($LASTEXITCODE -ne 0) {throw 'Plugin compilation failed'}
if ($Test) {
 $testOut = Join-Path $PSScriptRoot 'test-output'
 New-Item -ItemType Directory -Path $testOut -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $out 'AuditionMarkers.dll') -Destination $testOut -Force
 Get-ChildItem -LiteralPath $ClassicDirectory -Filter '*.dll' | ForEach-Object {try {[Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; Copy-Item -LiteralPath $_.FullName -Destination $testOut -Force} catch [BadImageFormatException] {}}
 & $compiler /nologo /target:exe ('/out:'+(Join-Path $testOut 'Tests.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ('/reference:'+$core) ('/reference:'+(Join-Path $out 'AuditionMarkers.dll')) (Join-Path $PSScriptRoot 'tests/Tests.cs')
 if ($LASTEXITCODE -ne 0) {throw 'Test compilation failed'}
 & (Join-Path $testOut 'Tests.exe') $testOut
 if ($LASTEXITCODE -ne 0) {throw 'Plugin tests failed'}
}
Write-Output ('Built '+(Join-Path $out 'AuditionMarkers.dll'))
