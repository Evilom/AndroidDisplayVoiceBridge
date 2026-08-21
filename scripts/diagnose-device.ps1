[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DeviceAddress,
    [ValidateSet('summary', 'keys', 'logs', 'packages')]
    [string]$Mode = 'summary',
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
    throw '找不到 adb.exe。请先安装 Google Android SDK Platform-Tools。'
}

$adb = Resolve-AdbExecutable -ConfiguredPath $AdbPath
$serial = $DeviceAddress.Trim()
if ($serial -match '^\d{1,3}(\.\d{1,3}){3}$') { $serial = "$serial`:5555" }

& $adb connect $serial | Out-Host
$state = (& $adb -s $serial get-state 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $state -ne 'device') {
    throw "设备未授权或不在线：$serial。请查看显示器上的 RSA 授权提示。"
}

switch ($Mode) {
    'summary' {
        $properties = @(
            'ro.product.manufacturer',
            'ro.product.model',
            'ro.product.name',
            'ro.build.version.release',
            'ro.build.version.incremental',
            'ro.build.fingerprint'
        )
        foreach ($property in $properties) {
            $value = (& $adb -s $serial shell getprop $property | Out-String).Trim()
            "{0}={1}" -f $property, $value
        }
        ''
        '输入设备概览（下一步用 -Mode keys 确认遥控器按键）：'
        & $adb -s $serial shell getevent -pl 2>&1 |
            Select-String -Pattern '^add device|^  name:|KEY_|event[0-9]'
    }
    'keys' {
        '现在按一下遥控器麦克风键；看到事件后按 Ctrl+C 结束。'
        '记录 /dev/input/eventN、EV_KEY 后的 KEY_*，以及 DOWN/UP。'
        & $adb -s $serial shell getevent -lt
    }
    'logs' {
        '先按 Ctrl+C 结束其他 logcat，再按住遥控器说一句完整的话。'
        '从输出中找最终识别文本、final=true、dialogId，以及对应日志 tag。'
        & $adb -s $serial logcat -v time 2>&1 |
            Select-String -Pattern 'asr|speech|recogn|voice|final|dialog|nlp' -CaseSensitive:$false
    }
    'packages' {
        & $adb -s $serial shell pm list packages 2>&1 |
            Select-String -Pattern 'voice|speech|assistant|xiaomi|miot|ifly|baidu' -CaseSensitive:$false
    }
}
