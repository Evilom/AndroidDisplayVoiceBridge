[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$DeviceAddress,
    [string]$AdbPath = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Resolve-AdbExecutable {
    param([string]$ConfiguredPath)
    $candidates = @()
    if ($ConfiguredPath) { $candidates += [Environment]::ExpandEnvironmentVariables($ConfiguredPath) }
    $candidates += (Join-Path $env:LOCALAPPDATA 'Programs\AndroidDisplayVoiceBridge\platform-tools\adb.exe')
    $candidates += (Join-Path $repoRoot 'artifacts\app\platform-tools\adb.exe')
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return [IO.Path]::GetFullPath($candidate) }
    }
    $command = Get-Command adb.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw '找不到 adb.exe。请先运行 install.ps1，或通过 -AdbPath 指定 Google Platform-Tools。'
}

function Normalize-DeviceAddress {
    param([string]$Address)
    $value = $Address.Trim()
    if ($value -match '^\d{1,3}(\.\d{1,3}){3}$') { return "$value`:5555" }
    return $value
}

$adb = Resolve-AdbExecutable -ConfiguredPath $AdbPath
foreach ($address in $DeviceAddress) {
    $serial = Normalize-DeviceAddress -Address $address
    Write-Host "连接 $serial"
    & $adb connect $serial
    if ($LASTEXITCODE -ne 0) { throw "adb connect $serial 失败" }
}

& $adb devices -l
