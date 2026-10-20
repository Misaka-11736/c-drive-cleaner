param(
    [string]$AppExe = "",
    [string]$Out = "",
    [string]$Version = "2.0.1"
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw '找不到 csc.exe' }

if (-not $AppExe) { $AppExe = Join-Path $root 'dist\C盘清理助手.exe' }
if (-not (Test-Path $AppExe)) {
    & (Join-Path $root 'build.ps1') -Out $AppExe
    if (-not (Test-Path $AppExe)) { throw '主程序编译失败' }
}

if (-not $Out) { $Out = Join-Path $root ('dist\c-drive-cleaner-' + $Version + '-setup.exe') }
$outDir = Split-Path -Parent $Out
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$files = @(
    (Join-Path $root 'src\Theme.cs'),
    (Join-Path $root 'src\Widgets.cs')
) + @(Get-ChildItem (Join-Path $root 'setup\*.cs') | ForEach-Object { $_.FullName })

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    "/out:$Out",
    "/win32icon:$root\app.ico",
    "/win32manifest:$root\app.manifest",
    "/resource:$AppExe,CCleanerSetup.payload.exe",
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll'
) + $files

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "安装程序编译失败，退出码 $LASTEXITCODE" }

Get-Item $Out | Select-Object Name, Length, LastWriteTime
