param(
    [string]$SdkRoot = $(if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { $env:ANDROID_HOME }),
    [string]$JavaHome = $env:JAVA_HOME,
    [string]$Keystore = (Join-Path $env:USERPROFILE '.android\adb-keeper-release.jks'),
    [string]$ApplicationId = 'io.github.androiddisplayvoicebridge.adbkeeper'
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$build = Join-Path $project 'build'
$classes = Join-Path $build 'classes'
$dex = Join-Path $build 'dex'
$out = Join-Path $project 'out'
$androidJar = Join-Path $SdkRoot 'platforms\android-34\android.jar'
$tools = Join-Path $SdkRoot 'build-tools\36.0.0'
$javac = Join-Path $JavaHome 'bin\javac.exe'

foreach ($required in @($androidJar, $javac, (Join-Path $tools 'aapt2.exe'))) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing build dependency: $required"
    }
}

Remove-Item -LiteralPath $build -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $classes, $dex, $out -Force | Out-Null

$sources = Get-ChildItem -LiteralPath (Join-Path $project 'src') -Recurse -Filter '*.java'
& $javac -encoding UTF-8 -source 8 -target 8 -bootclasspath $androidJar -d $classes $sources.FullName
if ($LASTEXITCODE -ne 0) { throw 'javac failed' }

$classFiles = Get-ChildItem -LiteralPath $classes -Recurse -Filter '*.class'
& (Join-Path $tools 'd8.bat') --lib $androidJar --min-api 23 --output $dex $classFiles.FullName
if ($LASTEXITCODE -ne 0) { throw 'd8 failed' }

$unsigned = Join-Path $build 'adb-keeper-unsigned.apk'
$packageArgs = @()
if ($ApplicationId -ne 'io.github.androiddisplayvoicebridge.adbkeeper') {
    $packageArgs = @('--rename-manifest-package', $ApplicationId)
}
& (Join-Path $tools 'aapt2.exe') link `
    -I $androidJar `
    --manifest (Join-Path $project 'AndroidManifest.xml') `
    --min-sdk-version 23 `
    --target-sdk-version 34 `
    --version-code 5 `
    --version-name '0.1.4' `
    @packageArgs `
    -o $unsigned
if ($LASTEXITCODE -ne 0) { throw 'aapt2 link failed' }

Push-Location $dex
try {
    & (Join-Path $tools 'aapt.exe') add $unsigned 'classes.dex'
    if ($LASTEXITCODE -ne 0) { throw 'aapt add failed' }
} finally {
    Pop-Location
}

$aligned = Join-Path $build 'adb-keeper-aligned.apk'
& (Join-Path $tools 'zipalign.exe') -f 4 $unsigned $aligned
if ($LASTEXITCODE -ne 0) { throw 'zipalign failed' }

if (-not (Test-Path -LiteralPath $Keystore)) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $Keystore) -Force | Out-Null
    & (Join-Path $JavaHome 'bin\keytool.exe') -genkeypair `
        -keystore $Keystore `
        -storepass 'android-display-voice-bridge' `
        -keypass 'android-display-voice-bridge' `
        -alias 'adb-keeper' `
        -keyalg RSA `
        -keysize 3072 `
        -validity 10000 `
        -dname 'CN=Android Display Voice Bridge, O=Community, C=CN'
    if ($LASTEXITCODE -ne 0) { throw 'keytool failed' }
}

$variant = if ($ApplicationId -eq 'io.github.androiddisplayvoicebridge.adbkeeper') { '' } else { '-MiTV' }
$apk = Join-Path $out "ADB-Keeper$variant-v0.1.4.apk"
& (Join-Path $tools 'apksigner.bat') sign `
    --ks $Keystore `
    --ks-key-alias 'adb-keeper' `
    --ks-pass 'pass:android-display-voice-bridge' `
    --key-pass 'pass:android-display-voice-bridge' `
    --out $apk `
    $aligned
if ($LASTEXITCODE -ne 0) { throw 'apksigner failed' }

& (Join-Path $tools 'apksigner.bat') verify --verbose $apk
if ($LASTEXITCODE -ne 0) { throw 'APK verification failed' }

Get-FileHash -LiteralPath $apk -Algorithm SHA256
Write-Host "Built $apk"
