# Redmi G Pro 27U 2026 完整教程

## 实测环境

| 项目 | 值 |
|---|---|
| 产品 | Redmi G Pro 27U 2026 智能显示器 |
| Android model | `MiTV_MFFU1` |
| Android product | `beekeeper` |
| 系统 | HyperOS 3 / Android 14 |
| 已测 build | `OS3.0.102.0.UFFMAMN` |
| 麦克风按键事件 | `/dev/input/event6` → `KEY_F5` DOWN / UP |
| 语音包 | `com.xiaomi.voicecontrol` |
| ASR 日志 tag | `=vc=en=arp=`，Info 级别 |
| 服务 fallback | `.VoiceControlService`，remote type `8` |

以上数据来自实机，不代表所有批次或未来固件永久相同。每次系统大版本升级后，建议重新跑一次 `summary`、`keys` 和 `logs` 探测。

## 1. 显示器准备

1. 确保语音遥控器已经配对，原生小爱语音能正常识别你说的话。
2. 进入“设置 → 关于”，连续点击“产品型号”7 次，看到开发者模式提示。
3. 进入“设置 → 账号与安全”，允许 ADB 调试。
4. 从显示器网络设置或路由器后台记录 IP。
5. 在路由器中为该 MAC 地址做 DHCP 保留。

如果你找不到传统 Android 的 Build number，这是正常现象；该机型的入口是“产品型号”。小米官方说明可见[此支持文章](https://www.mi.com/global/support/article/KA-06513/)。

## 2. 首次授权

在电脑 PowerShell 中运行：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\connect.ps1 -DeviceAddress @("192.168.1.101")
```

显示器弹出 RSA 调试授权时，核对后选择允许；个人可信电脑可勾选始终允许。两台显示器需要分别确认。

成功输出类似：

```text
192.168.1.101:5555    device product:beekeeper model:MiTV_MFFU1
```

`unauthorized` 表示还没有确认授权，`offline` 表示连接尚未建立。不要继续安装，先按[故障排查](troubleshooting.md)解决。

## 3. 单台安装

从 GitHub Release ZIP 解压后：

```powershell
.\scripts\install.ps1 -Profile redmi-g-pro-27u-2026 `
  -DeviceAddress @("192.168.1.101")
```

从源码仓库安装时先构建：

```powershell
.\scripts\build.ps1
.\scripts\install.ps1 -Profile redmi-g-pro-27u-2026 `
  -DeviceAddress @("192.168.1.101")
```

安装脚本会：

- 编译 Windows 桥接程序；
- 从 Google 官方地址下载 Platform-Tools（本机尚无 ADB 时）；
- 由红米 profile 生成本机 `config.json`；
- 写入当前用户的开机启动项；
- 启动通知区域程序。

## 4. 双显示器安装

```powershell
.\scripts\install.ps1 -Profile redmi-g-pro-27u-2026 `
  -DeviceAddress @("192.168.1.101", "192.168.1.102") -ForceConfig
```

每台显示器会有独立的按键监听、ASR 日志监听和去重状态。一只遥控器只应与一台显示器配对；如果两台同时收到同一个遥控器事件，可能重复输入。

`-ForceConfig` 会按本次 IP 重建安装目录中的配置。已经手工定制配置时先备份，再使用这个参数。

## 5. 验证

1. 打开普通权限的记事本。
2. 单击正文区域，确保光标闪烁。
3. 按住任意一只遥控器的麦克风键，说“遥控器语音输入测试成功”。
4. 松开按键并等待最终识别。

预期结果：

- 文本出现在记事本当前光标；
- 通知区域短暂显示识别结果；
- 小爱不会继续进入问答或执行命令；
- 下一次按键仍能重新启动识别。

日志默认位于：

```text
%LOCALAPPDATA%\AndroidDisplayVoiceBridge\bridge.log
```

默认只记录字符数，不记录识别正文。

## 6. 红米 profile 为什么有五个高级开关

```json
{
  "keepAndroidAwakeWhilePowered": true,
  "manageVoicePackage": true,
  "disableVoicePackageWhenInactive": false,
  "stopAssistantAfterFinal": true,
  "startServiceFallback": true
}
```

- `keepAndroidAwakeWhilePowered`：每次 ADB 重连后设置 `stay_on_while_plugged_in=7`，避免 HyperOS 深度待机后保留开关状态却停止 `adbd`。它可能增加待机功耗，但不强制 Windows 保持亮屏。
- `manageVoicePackage`：程序启动时确保小米语音包处于 enabled 状态。
- `stopAssistantAfterFinal`：拿到最终文字后立刻 `force-stop`，避免小爱继续回答。
- `startServiceFallback`：如果下一次按键没有自动拉起语音进程，桥接程序发送该机型实测的 KEYDOWN / KEYUP service action。
- `disableVoicePackageWhenInactive`：默认关闭。退出桥接程序后，小爱恢复正常行为，而不是被长期禁用。

不要把这五项原样复制给其他品牌或型号。

如需恢复 Android 默认待机策略：

```powershell
adb -s 192.168.1.101:5555 shell settings put global stay_on_while_plugged_in 0
```

## 7. 完整重启后 ADB 自动关闭

固件 `OS3.0.102.0.UFFMAMN` 会在启动早期主动停止普通侧载应用，并在网络 ADB
短暂出现后再次关闭它。`keepAndroidAwakeWhilePowered` 只能缓解待机，不能解决完整
重启。

仓库提供可选的设备端 [ADB Keeper](../companion/adb-keeper/README.md)。该固件预留了
未安装的 `com.example.autoreboot` 自启动兼容身份；MiTV 构建使用该身份后可以收到
开机广播，在 HyperOS 二次复位后重新写入 `adb_enabled=1`。两台 Redmi G Pro 27U
2026 已完成真实重启验证，恢复通常需要 1–2 分钟。

前提与风险：

- 第一次安装前仍需手动打开 ADB，并接受电脑的 RSA 授权。
- 必须用 ADB 一次性授予 `WRITE_SECURE_SETTINGS`；它不会显示普通权限弹窗。
- 5555 会长期暴露在显示器所在局域网，只能用于可信家庭网络，禁止路由器端口转发。
- 其他固件未必拥有相同白名单，不能直接假设兼容。

安装已构建的 MiTV APK：

```powershell
.\companion\adb-keeper\install-mitv.ps1 `
  -Serial 192.168.1.101:5555,192.168.1.102:5555 `
  -ApkPath .\companion\adb-keeper\out\ADB-Keeper-MiTV-v0.1.4.apk
```

## 8. 手工修改配置

配置文件：

```text
%LOCALAPPDATA%\Programs\AndroidDisplayVoiceBridge\config.json
```

退出通知区域程序后编辑，保存，再重新启动 EXE。IP 变化时只需要改 `devices[].serial`。若显示器升级后按键失效，重新探测 `inputDevice` 和 `keyToken`。

## 9. 回退与卸载

只退出程序：右击通知区域图标 → 退出。由于 profile 默认不在退出时 disable 包，小爱应恢复原生行为。

如果语音包曾被手工禁用，可恢复：

```powershell
adb -s 192.168.1.101:5555 shell pm enable --user 0 com.xiaomi.voicecontrol
```

卸载程序但保留配置：

```powershell
.\scripts\uninstall.ps1
```

同时删除配置和日志：

```powershell
.\scripts\uninstall.ps1 -RemoveConfig -RemoveLogs
```

卸载不会关闭显示器上的 ADB。若不再调试，请到显示器设置中手动关闭。

移除显示器端 ADB Keeper：

```powershell
adb -s 192.168.1.101:5555 uninstall com.example.autoreboot
```
