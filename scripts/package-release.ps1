[CmdletBinding()]
param(
    [string]$Version = '0.1.0'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'build.ps1') -Clean

$releaseRoot = Join-Path $repoRoot 'artifacts\release'
$packageName = "AndroidDisplayVoiceBridge-v$Version"
$staging = Join-Path $releaseRoot $packageName
$archive = Join-Path $releaseRoot "$packageName.zip"

$safePrefix = [IO.Path]::GetFullPath($releaseRoot).TrimEnd('\') + '\'
if (-not ([IO.Path]::GetFullPath($staging)).StartsWith(
    $safePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to package outside artifacts\release.'
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Copy-Item -Path (Join-Path $repoRoot 'artifacts\app\*') -Destination $staging -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $staging 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\install.ps1') `
    -Destination (Join-Path $staging 'scripts\install.ps1') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\uninstall.ps1') `
    -Destination (Join-Path $staging 'scripts\uninstall.ps1') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\connect.ps1') `
    -Destination (Join-Path $staging 'scripts\connect.ps1') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\diagnose-device.ps1') `
    -Destination (Join-Path $staging 'scripts\diagnose-device.ps1') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $staging -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $staging -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Destination $staging -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'SECURITY.md') -Destination $staging -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') `
    -Destination (Join-Path $staging 'docs') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'config') `
    -Destination (Join-Path $staging 'config') -Recurse -Force

Compress-Archive -LiteralPath $staging -DestinationPath $archive -CompressionLevel Optimal
$checksumPath = "$archive.sha256"
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Encoding ASCII `
    -Value "$checksum  $([IO.Path]::GetFileName($archive))"
Write-Host "Release package: $archive"
Write-Host "SHA-256 file: $checksumPath"
