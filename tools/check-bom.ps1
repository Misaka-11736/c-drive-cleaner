$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$bad = @()

Get-ChildItem $root -Filter *.ps1 -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\dist\\|\\bin\\|\\obj\\|\\.git\\'
} | ForEach-Object {
    $bytes = [IO.File]::ReadAllBytes($_.FullName)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8)
    if (($text -match '[^\x00-\x7F]') -and -not $hasBom) {
        $bad += $_.FullName
    }
}

if ($bad.Count -gt 0) {
    $bad | ForEach-Object { Write-Host ('missing UTF-8 BOM: ' + $_) -ForegroundColor Red }
    exit 1
}

Write-Host 'PowerShell BOM check passed.'
