param(
    [Parameter(Mandatory = $true)]
    [string[]]$Serial,
    [string]$ApkPath = (Join-Path $PSScriptRoot 'out\ADB-Keeper-MiTV-v0.1.4.apk'),
    [string]$AdbPath = 'adb.exe'
)

$ErrorActionPreference = 'Stop'
$packageId = 'com.example.autoreboot'
$activity = 'io.github.androiddisplayvoicebridge.adbkeeper.MainActivity'

if (-not (Test-Path -LiteralPath $ApkPath)) {
    throw "Missing APK: $ApkPath"
}

$adbCommand = Get-Command $AdbPath -ErrorAction SilentlyContinue
if (-not $adbCommand) {
    throw "ADB was not found. Install Android Platform Tools or pass -AdbPath."
}
$AdbPath = $adbCommand.Source

foreach ($device in $Serial) {
    Write-Host "Configuring $device"
    & $AdbPath connect $device | Out-Host
    if ((& $AdbPath -s $device get-state 2>$null) -ne 'device') {
        throw "ADB device is not authorized or online: $device"
    }

    & $AdbPath -s $device install -r $ApkPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "APK install failed on $device" }

    & $AdbPath -s $device shell pm grant $packageId `
        android.permission.WRITE_SECURE_SETTINGS
    if ($LASTEXITCODE -ne 0) { throw "WRITE_SECURE_SETTINGS grant failed on $device" }

    & $AdbPath -s $device shell cmd deviceidle whitelist "+$packageId" | Out-Host
    & $AdbPath -s $device shell cmd appops set $packageId RUN_IN_BACKGROUND allow
    & $AdbPath -s $device shell cmd appops set $packageId RUN_ANY_IN_BACKGROUND allow
    & $AdbPath -s $device shell am start -W -n "$packageId/$activity" | Out-Host

    $permission = & $AdbPath -s $device shell dumpsys package $packageId |
        Select-String 'WRITE_SECURE_SETTINGS: granted=true'
    if (-not $permission) { throw "Permission verification failed on $device" }

    Write-Host "$device configured. Reboot the display and allow up to two minutes for ADB recovery."
}
