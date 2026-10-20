param(
    [string]$Out = "",
    [switch]$NoIcon
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 csc.exe" }

if (-not $Out) { $Out = Join-Path $root 'C盘清理助手.exe' }
$outDir = Split-Path -Parent $Out
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$files = @(Get-ChildItem (Join-Path $root 'src\*.cs') | ForEach-Object { $_.FullName })
if ($files.Count -eq 0) { throw "src 下没有源文件" }

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    "/out:$Out",
    "/win32manifest:$root\app.manifest",
    "/resource:$root\src\report-template.html,CCleaner.report-template.html",
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Management.dll',
    '/r:System.Web.Extensions.dll'
)

$icon = Join-Path $root 'app.ico'
if (-not $NoIcon -and (Test-Path $icon)) { $cscArgs += "/win32icon:$icon" }
$cscArgs += $files

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

Get-Item $Out | Select-Object Name, Length, LastWriteTime
