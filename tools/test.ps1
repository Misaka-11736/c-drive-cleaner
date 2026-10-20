# Builds the cleaner core plus the sandboxed deletion test and runs it.
# Exits non-zero if the test fails, so CI can gate on it.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }

$candidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)
$csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'csc.exe not found. .NET Framework 4.x is required.' }

$out = Join-Path $env:TEMP ('cc-clean-test-' + [guid]::NewGuid().ToString('N') + '.exe')

$files = @('Models.cs', 'Targets.cs', 'Util.cs', 'Cleaner.cs') |
    ForEach-Object { Join-Path $root ('src\' + $_) }
$files += Join-Path $root 'tools\CleanTest.cs'

& $csc /nologo /target:exe /platform:anycpu /codepage:65001 /out:$out `
    /r:System.dll /r:System.Core.dll /r:System.Management.dll @files

if ($LASTEXITCODE -ne 0) { throw 'test harness failed to compile' }

& $out
$code = $LASTEXITCODE
Remove-Item $out -Force -ErrorAction SilentlyContinue

if ($code -ne 0) { throw "safety tests failed (exit $code)" }
Write-Host 'All safety tests passed.' -ForegroundColor Green
exit 0
