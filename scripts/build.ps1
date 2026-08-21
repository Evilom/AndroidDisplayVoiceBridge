[CmdletBinding()]
param(
    [switch]$Clean
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath = Join-Path $repoRoot 'src\AndroidDisplayVoiceBridge.cs'
$artifactDir = Join-Path $repoRoot 'artifacts\app'
$outputPath = Join-Path $artifactDir 'AndroidDisplayVoiceBridge.exe'

function Assert-ChildPath {
    param([string]$Parent, [string]$Child)
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $childPath = [IO.Path]::GetFullPath($Child)
    if (-not $childPath.StartsWith($parentPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $childPath"
    }
}

Assert-ChildPath -Parent $repoRoot -Child $artifactDir
if ($Clean -and (Test-Path -LiteralPath $artifactDir)) {
    Remove-Item -LiteralPath $artifactDir -Recurse -Force
}
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw '找不到 .NET Framework C# 编译器。请在“启用或关闭 Windows 功能”中启用 .NET Framework 4.x。'
}

$compilerArguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/warn:4',
    "/out:$outputPath",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Web.Extensions.dll',
    '/reference:System.Windows.Forms.dll',
    $sourcePath
)

& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，csc.exe 退出码：$LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'config\config.example.json') `
    -Destination (Join-Path $artifactDir 'config.json') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'config\config.schema.json') `
    -Destination (Join-Path $artifactDir 'config.schema.json') -Force
$profileOutput = Join-Path $artifactDir 'profiles'
if (Test-Path -LiteralPath $profileOutput) {
    Remove-Item -LiteralPath $profileOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $profileOutput -Force | Out-Null
Copy-Item -Path (Join-Path $repoRoot 'config\profiles\*') `
    -Destination $profileOutput -Recurse -Force

$selfTest = Start-Process -FilePath $outputPath -ArgumentList '--self-test' -Wait -PassThru
if ($selfTest.ExitCode -ne 0) {
    throw "程序内置自检失败，退出码：$($selfTest.ExitCode)"
}

Write-Host "Build OK: $outputPath"
