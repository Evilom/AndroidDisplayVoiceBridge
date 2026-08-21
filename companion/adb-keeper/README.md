# ADB Keeper companion

ADB Keeper is an optional on-display companion for Android/HyperOS displays whose
firmware turns network ADB off after a reboot. It checks the display's local TCP
port 5555 after boot and every 15 minutes. It only toggles the Android ADB setting
when the port is not listening, so an active connection is not periodically reset.

This companion is not a security bypass. The display must have ADB enabled once,
the APK must be installed through an already-authorized ADB connection, and the
privileged settings permission must be granted explicitly from that connection.
Android's normal RSA host authorization remains in effect.

## Install and authorize

```powershell
$adb = 'adb.exe'
$serial = 'DISPLAY_IP:5555'
$apk = '.\companion\adb-keeper\out\ADB-Keeper-v0.1.4.apk'

& $adb -s $serial install -r $apk
& $adb -s $serial shell pm grant `
  io.github.androiddisplayvoicebridge.adbkeeper `
  android.permission.WRITE_SECURE_SETTINGS
& $adb -s $serial shell am start -n `
  io.github.androiddisplayvoicebridge.adbkeeper/.MainActivity
```

The app intentionally has no launcher/TV-home icon. Some display firmwares block
unknown sideloaded launcher apps during boot. Keeping the companion headless avoids
that vendor launcher policy while its explicit diagnostic activity remains available
through ADB.

In addition to Android's standard boot broadcasts, the receiver recognizes Xiaomi
TV's staged `mitv.action.STR_BOOT_COMPLETED` and `STARTAPPS_PRIORITY_*` broadcasts.
Other brands can add their equivalent actions without changing the repair logic.

### Xiaomi/Redmi TV compatibility identity

Redmi G Pro 27U 2026 firmware `OS3.0.102.0.UFFMAMN` includes the unused compatibility
identity `com.example.autoreboot` in its boot-autostart allowlist. A device-specific
build can use it without weakening the allowlist for every sideloaded application:

```powershell
.\build.ps1 -ApplicationId com.example.autoreboot
```

This produces `ADB-Keeper-MiTV-v0.1.4.apk`. Confirm that the identity is not already
installed (`adb shell pm path com.example.autoreboot`) before using this variant. Its
component classes intentionally remain in the project's GitHub namespace.

Do not use `com.example.autorun1` on this firmware. Although it also appears in the
autostart allowlist, the package installer explicitly blacklists that identity.

Install one or more displays with [`install-mitv.ps1`](install-mitv.ps1). The
script uses `adb.exe` from `PATH`; use `-AdbPath` if Platform Tools is elsewhere:

```powershell
.\install-mitv.ps1 `
  -Serial 192.168.1.101:5555,192.168.1.102:5555 `
  -ApkPath .\out\ADB-Keeper-MiTV-v0.1.4.apk
```

On the tested firmware, network ADB briefly appears during boot, is reset by HyperOS,
and is restored by the companion roughly 1–2 minutes after power-on. This identity is
a firmware-specific compatibility mechanism, not a universal Xiaomi package name;
verify the device whitelist and perform a reboot test before documenting another model
as supported.

For a controlled recovery test, schedule a check before turning ADB off. The
receiver requires Android's `DUMP` permission, which the ADB shell has but normal
third-party apps do not:

```powershell
& $adb -s $serial shell am broadcast `
  -a io.github.androiddisplayvoicebridge.adbkeeper.SCHEDULE `
  --el delay_ms 30000 `
  -p io.github.androiddisplayvoicebridge.adbkeeper
& $adb -s $serial shell settings put global adb_enabled 0
```

If `pm grant` is rejected, that firmware does not allow this non-root approach.
Uninstalling the APK removes the keeper; it does not remove the display's own ADB
or developer settings.

## Security note

Keeping TCP ADB available exposes port 5555 on the display's LAN interface. Use
this only on a trusted private network, do not forward port 5555 on the router,
and remove authorizations for computers you no longer control.

## Build

`build.ps1` uses `JAVA_HOME`, `ANDROID_SDK_ROOT` (or `ANDROID_HOME`), Android
platform 34, and Build Tools 36.0.0. You can also pass `-JavaHome`, `-SdkRoot`,
and `-Keystore` explicitly. The signing keystore is deliberately stored outside
the repository. Keep it backed up: Android requires the same signing identity for
future in-place upgrades.
