param(
    [string]$Version = "2.0.0"
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist -Force | Out-Null

$appExe = Join-Path $dist 'C盘清理助手.exe'
$setupExe = Join-Path $dist ('c-drive-cleaner-' + $Version + '-setup.exe')
$portableZip = Join-Path $dist ('c-drive-cleaner-' + $Version + '-portable.zip')

Write-Host '1/4 编译主程序...' -ForegroundColor Cyan
& (Join-Path $root 'build.ps1') -Out $appExe | Out-Null

Write-Host '2/4 编译安装程序...' -ForegroundColor Cyan
& (Join-Path $root 'build-installer.ps1') -AppExe $appExe -Out $setupExe -Version $Version | Out-Null

Write-Host '3/4 打包便携版...' -ForegroundColor Cyan
$stage = Join-Path $env:TEMP ('cc-portable-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item $appExe (Join-Path $stage 'C盘清理助手.exe')
Copy-Item (Join-Path $root 'docs\使用说明.md') (Join-Path $stage '使用说明.md')
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')

$seven = 'D:\Program Files\7-Zip\7z.exe'
if (-not (Test-Path $seven)) { $seven = 'C:\Program Files\7-Zip\7z.exe' }
if (Test-Path $seven) {
    & $seven a -tzip -mx=9 $portableZip (Join-Path $stage '*') | Out-Null
} else {
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $portableZip -CompressionLevel Optimal
}
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '4/4 生成 SHA256...' -ForegroundColor Cyan
$lines = @()
foreach ($f in @($setupExe, $portableZip, $appExe)) {
    $h = (Get-FileHash $f -Algorithm SHA256).Hash.ToLower()
    $lines += ($h + '  ' + (Split-Path $f -Leaf))
}
[System.IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'), $lines, (New-Object System.Text.UTF8Encoding($false)))

Get-ChildItem $dist -File | Sort-Object Name |
    Select-Object Name, @{n = '大小'; e = { '{0:N0} KB' -f ($_.Length / 1KB) } } |
    Format-Table -AutoSize
