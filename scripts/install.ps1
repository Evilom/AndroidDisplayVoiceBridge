[CmdletBinding()]
param(
    [string[]]$DeviceAddress = @(),
    [ValidateSet('redmi-g-pro-27u-2026', 'generic-android-display.safe')]
    [string]$Profile = 'redmi-g-pro-27u-2026',
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\AndroidDisplayVoiceBridge'),
    [bool]$AddToStartup = $true,
    [bool]$DownloadPlatformTools = $true,
    [switch]$ForceConfig
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$InstallDir = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($InstallDir))
$destinationExe = Join-Path $InstallDir 'AndroidDisplayVoiceBridge.exe'
$destinationConfig = Join-Path $InstallDir 'config.json'

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
        throw "不安全的安装目录：$full"
    }
}

function Find-SourceFile {
    param([string[]]$Candidates, [string]$Description)
    foreach ($candidate in $Candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "找不到$Description。请先运行 scripts\build.ps1。"
}

function Normalize-DeviceAddress {
    param([string]$Address)
    $value = $Address.Trim()
    if ($value -match '^\d{1,3}(\.\d{1,3}){3}$') { return "$value`:5555" }
    return $value
}

Assert-SafeInstallDirectory -Path $InstallDir

$sourceExeCandidates = @(
    (Join-Path $repoRoot 'artifacts\app\AndroidDisplayVoiceBridge.exe'),
    (Join-Path $repoRoot 'AndroidDisplayVoiceBridge.exe')
)
if (-not ($sourceExeCandidates | Where-Object { Test-Path -LiteralPath $_ })) {
    $buildScript = Join-Path $repoRoot 'scripts\build.ps1'
    if (Test-Path -LiteralPath $buildScript) { & $buildScript }
}
$sourceExe = Find-SourceFile -Candidates $sourceExeCandidates -Description '程序文件'

$profileCandidates = @(
    (Join-Path $repoRoot "config\profiles\$Profile.json"),
    (Join-Path $repoRoot "profiles\$Profile.json")
)
$profilePath = Find-SourceFile -Candidates $profileCandidates -Description '设备配置模板'

Get-Process -Name 'AndroidDisplayVoiceBridge' -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $destinationExe -Force

if ($ForceConfig -or -not (Test-Path -LiteralPath $destinationConfig)) {
    $profileObject = Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($DeviceAddress.Count -gt 0) {
        $template = $profileObject.devices[0]
        $newDevices = @()
        $index = 1
        foreach ($address in $DeviceAddress) {
            $device = $template | ConvertTo-Json -Depth 20 | ConvertFrom-Json
            $device.serial = Normalize-DeviceAddress -Address $address
            $device.enabled = $true
            $device.name = "$($template.name) #$index"
            $newDevices += $device
            $index++
        }
        $profileObject.devices = @($newDevices)
    }
    $profileObject.'$schema' = './config.schema.json'
    $profileObject | ConvertTo-Json -Depth 20 |
        Set-Content -LiteralPath $destinationConfig -Encoding UTF8
} elseif ($DeviceAddress.Count -gt 0) {
    Write-Warning '现有 config.json 已保留。若要按本次地址重建配置，请加 -ForceConfig。'
}

$schemaCandidates = @(
    (Join-Path $repoRoot 'config\config.schema.json'),
    (Join-Path $repoRoot 'config.schema.json')
)
$schemaPath = Find-SourceFile -Candidates $schemaCandidates -Description '配置 Schema'
Copy-Item -LiteralPath $schemaPath -Destination (Join-Path $InstallDir 'config.schema.json') -Force

$adbPath = Join-Path $InstallDir 'platform-tools\adb.exe'
if ($DownloadPlatformTools -and -not (Test-Path -LiteralPath $adbPath)) {
    $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) `
        ('AndroidDisplayVoiceBridge-' + [IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    try {
        $archivePath = Join-Path $temporaryRoot 'platform-tools.zip'
        $extractPath = Join-Path $temporaryRoot 'extract'
        Write-Host '正在从 Google 官方地址下载 Android SDK Platform-Tools...'
        Invoke-WebRequest -UseBasicParsing `
            -Uri 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' `
            -OutFile $archivePath
        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath -Force
        $platformToolsSource = Join-Path $extractPath 'platform-tools'
        $platformToolsTarget = Join-Path $InstallDir 'platform-tools'
        New-Item -ItemType Directory -Path $platformToolsTarget -Force | Out-Null
        Copy-Item -Path (Join-Path $platformToolsSource '*') `
            -Destination $platformToolsTarget -Recurse -Force
    } finally {
        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }
}

if (-not (Test-Path -LiteralPath $adbPath)) {
    Write-Warning '尚未发现 platform-tools\adb.exe。请手动安装 Google Platform-Tools，或修正 config.json 的 adbPath。'
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($AddToStartup) {
    $runCommand = '"' + $destinationExe + '" --config "' + $destinationConfig + '"'
    New-ItemProperty -Path $runKey -Name 'AndroidDisplayVoiceBridge' `
        -Value $runCommand -PropertyType String -Force | Out-Null
} else {
    Remove-ItemProperty -Path $runKey -Name 'AndroidDisplayVoiceBridge' `
        -ErrorAction SilentlyContinue
}

$installedConfig = Get-Content -LiteralPath $destinationConfig -Raw -Encoding UTF8 | ConvertFrom-Json
$hasEnabledDevice = @($installedConfig.devices | Where-Object { $_.enabled }).Count -gt 0
if ($hasEnabledDevice -and (Test-Path -LiteralPath $adbPath)) {
    $launchArguments = '--config "' + $destinationConfig.Replace('"', '\"') + '"'
    Start-Process -FilePath $destinationExe -ArgumentList $launchArguments
    Write-Host '安装完成，程序已经启动。'
} else {
    Write-Host "安装完成。请先编辑 $destinationConfig，填入探测结果并启用设备。"
}
