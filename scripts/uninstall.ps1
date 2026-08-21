[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\AndroidDisplayVoiceBridge'),
    [switch]$RemoveConfig,
    [switch]$RemoveLogs
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$InstallDir = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($InstallDir))

function Assert-SafeInstallDirectory {
    param([string]$Path)
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $forbidden = @(
        [IO.Path]::GetPathRoot($full).TrimEnd('\'),
        [Environment]::GetFolderPath('UserProfile').TrimEnd('\'),
        $env:LOCALAPPDATA.TrimEnd('\'),
        $env:ProgramFiles.TrimEnd('\')
    )
    if ($full.Length -lt 12 -or $forbidden -contains $full) {
        throw "不安全的卸载目录：$full"
    }
}

Assert-SafeInstallDirectory -Path $InstallDir
Get-Process -Name 'AndroidDisplayVoiceBridge' -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -Path $runKey -Name 'AndroidDisplayVoiceBridge' `
    -ErrorAction SilentlyContinue

$knownFiles = @(
    (Join-Path $InstallDir 'AndroidDisplayVoiceBridge.exe'),
    (Join-Path $InstallDir 'config.schema.json')
)
foreach ($file in $knownFiles) {
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
}

$platformTools = Join-Path $InstallDir 'platform-tools'
if (Test-Path -LiteralPath $platformTools) {
    Remove-Item -LiteralPath $platformTools -Recurse -Force
}

if ($RemoveConfig) {
    $configuration = Join-Path $InstallDir 'config.json'
    if (Test-Path -LiteralPath $configuration) { Remove-Item -LiteralPath $configuration -Force }
}

if ($RemoveLogs) {
    $logDirectory = Join-Path $env:LOCALAPPDATA 'AndroidDisplayVoiceBridge'
    if (Test-Path -LiteralPath $logDirectory) {
        Remove-Item -LiteralPath $logDirectory -Recurse -Force
    }
}

Write-Host '卸载完成。除非显式指定 -RemoveConfig，否则 config.json 已保留。'
