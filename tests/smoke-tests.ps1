[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $repoRoot 'scripts\build.ps1') -Clean

$exe = Join-Path $repoRoot 'artifacts\app\AndroidDisplayVoiceBridge.exe'
$selfTest = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru
if ($selfTest.ExitCode -ne 0) { throw "内置自检失败：$($selfTest.ExitCode)" }

$profiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'config\profiles') -Filter '*.json'
foreach ($profile in $profiles) {
    $validation = Start-Process -FilePath $exe `
        -ArgumentList @('--validate-config', $profile.FullName) -Wait -PassThru
    if ($validation.ExitCode -ne 0) {
        throw "配置验证失败：$($profile.FullName)"
    }
}

$redmiProfilePath = Join-Path $repoRoot 'config\profiles\redmi-g-pro-27u-2026.json'
$redmiProfile = Get-Content -LiteralPath $redmiProfilePath -Raw -Encoding UTF8 | ConvertFrom-Json
$fixture = (Get-Content -LiteralPath `
    (Join-Path $repoRoot 'tests\fixtures\xiaomi-final-asr.log') -Raw -Encoding UTF8).Trim()
$match = [regex]::Match($fixture, $redmiProfile.devices[0].finalAsrPattern)
if (-not $match.Success -or $match.Groups['text'].Value -ne '遥控器语音输入测试成功' -or
    $match.Groups['id'].Value -ne 'demo-001') {
    throw '红米 ASR 日志解析测试失败。'
}

$forbiddenPatterns = @(
    ('192\.168\.' + '110\.'),
    ('D:' + '\\Tools'),
    ('C:' + '\\Users\\' + '67224'),
    ('672' + '246')
)
$textFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -File |
    Where-Object { $_.FullName -notlike '*\artifacts\*' -and $_.Extension -in @('.cs', '.json', '.md', '.ps1', '.yml', '.yaml', '.log') }
foreach ($file in $textFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($pattern in $forbiddenPatterns) {
        if ($content -match $pattern) { throw "发现不应公开的本机信息：$($file.FullName)" }
    }
}

$parseErrors = @()
Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter '*.ps1' -File |
    Where-Object { $_.FullName -notlike '*\artifacts\*' } |
    ForEach-Object {
        $tokens = $null
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile(
            $_.FullName, [ref]$tokens, [ref]$errors)
        if ($errors) { $script:parseErrors += $errors }
    }
if ($parseErrors.Count -gt 0) {
    throw "PowerShell 语法检查失败：$($parseErrors[0].Message)"
}

Write-Host "Smoke tests OK: $($profiles.Count) profiles validated"
