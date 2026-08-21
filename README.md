# Android Display Voice Bridge

把 Android / HyperOS 智能显示器的语音遥控器，变成 Windows 的无线语音输入器。

按住遥控器麦克风键说话，显示器使用原生语音服务完成识别；松开后，最终文本被写入 Windows 当前光标。整个过程不占用电脑麦克风，也不依赖 Windows 语音输入快捷键。

> 当前为社区实验项目，不是小米、Redmi 或其他显示器厂商的官方软件。修改系统包行为前，请先阅读[兼容性与安全边界](#兼容性与安全边界)。

## 已验证状态

| 设备 | 系统 | 状态 | 配置 |
|---|---|---:|---|
| Redmi G Pro 27U 2026（`MiTV_MFFU1` / `beekeeper`） | HyperOS 3、Android 14、`OS3.0.102.0.UFFMAMN` | ✅ 双机实测 | [`redmi-g-pro-27u-2026.json`](config/profiles/redmi-g-pro-27u-2026.json) |
| 其他 Android / HyperOS 智能显示器或电视 | 取决于固件、遥控器和日志权限 | 🧪 需要探测 | [适配新设备](docs/adapt-new-device.md) |
| 普通“无系统”显示器、无 ADB 的封闭系统 | 不满足技术前提 | ❌ 本方案不可用 | [前置条件](docs/prerequisites.md) |

已验证参数只是该机型当前固件的实测结果。系统升级后，`event6`、日志 tag 或包名都可能变化，请按探测教程复核。

## 工作原理

```mermaid
flowchart LR
    A[蓝牙语音遥控器] -->|按键与语音| B[显示器原生语音服务]
    B -->|最终识别文本| C[Android logcat]
    A -->|DOWN / UP| D[Android input event]
    C -->|局域网 ADB| E[本项目 Windows 桥接程序]
    D -->|局域网 ADB| E
    E -->|SendInput Unicode| F[Windows 当前输入光标]
    E -. 可选 .->|识别结束后停止助手| B
```

程序只在遥控器麦克风键触发后的短时间窗口接受最终识别结果，防止无关日志被输入。红米 profile 会在取到最终文本后停止小爱继续回答；通用 profile 默认不会管理或强停任何系统包。

## 前置条件

- Windows 10 / 11，.NET Framework 4.8。
- 一台基于 Android / HyperOS、能开启 ADB 调试的智能显示器或电视。
- 带麦克风、已与显示器配对并能正常唤起原生语音助手的遥控器。
- 电脑和显示器处于同一个可信局域网；建议在路由器中为显示器做 DHCP 地址保留。
- 显示器自身能联网使用厂商的语音识别服务。
- Google 官方 Android SDK Platform-Tools。安装脚本可从官方地址下载。
- 能在显示器上确认电脑的 ADB RSA 授权提示。

不需要电脑麦克风、USB-C 视频线、微信输入法语音快捷键或额外 VPN。详细判断标准见[前置条件与兼容性](docs/prerequisites.md)。

## Redmi G Pro 27U 2026 快速开始

### 1. 开启开发者模式与 ADB

1. 在显示器中进入“设置 → 关于”。
2. 连续点击“产品型号”7 次。这个机型不一定显示传统 Android 的“版本号/Build number”。
3. 返回“设置 → 账号与安全”，将“ADB 调试”设为允许。
4. 在网络设置或路由器后台查看显示器 IP。

小米官方文档同样给出了“产品型号连续点击 7 次”和随后开启 ADB 的路径：[How to enable Developer Options on Mi TV/Mi Box](https://www.mi.com/global/support/article/KA-06513/)。菜单名称可能随地区和固件变化。

### 2. 获取程序并安装

普通用户建议从 GitHub Releases 下载 `AndroidDisplayVoiceBridge-v0.1.0.zip`，解压后在该目录打开 PowerShell：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\install.ps1 -Profile redmi-g-pro-27u-2026 -DeviceAddress @("192.168.1.101")
```

如果使用 `git clone` 得到源码仓库，则先构建一次：

```powershell
.\scripts\build.ps1
.\scripts\install.ps1 -Profile redmi-g-pro-27u-2026 -DeviceAddress @("192.168.1.101")
```

两台显示器：

```powershell
.\scripts\install.ps1 -Profile redmi-g-pro-27u-2026 `
  -DeviceAddress @("192.168.1.101", "192.168.1.102") -ForceConfig
```

把示例地址换成你自己的局域网 IP。首次连接时，请在每台显示器上允许 RSA 调试；若没有弹窗，先运行：

```powershell
.\scripts\connect.ps1 -DeviceAddress @("192.168.1.101", "192.168.1.102")
```

安装位置默认为 `%LOCALAPPDATA%\Programs\AndroidDisplayVoiceBridge`，并为当前 Windows 用户设置开机启动。安装脚本不会修改 Codex、Claude Code、输入法或其他应用配置。

### 3. 使用

1. 把光标放到记事本、编辑器、浏览器输入框或聊天窗口。
2. 按住遥控器麦克风键说话。
3. 松开按键，等待显示器完成识别；文本会写入当前光标。
4. 右击通知区域图标，可以暂停、重连、打开配置或查看日志。

建议先用普通权限的记事本测试。受 Windows UIPI 限制，普通权限运行的桥接程序不能向“以管理员身份运行”的窗口注入按键。

完整的实机步骤和回退方法见[Redmi G Pro 27U 2026 教程](docs/redmi-g-pro-27u-2026.md)。

## 适配其他国产智能显示器

不要直接照抄红米的 `event6`、`KEY_F5` 或 `com.xiaomi.voicecontrol`。先运行只读探测：

```powershell
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode summary
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode keys
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode logs
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode packages
```

依次确认：

1. 遥控器麦克风键对应哪个 `/dev/input/eventN` 和 `KEY_*`。
2. 哪个 logcat tag 包含“最终识别文本”。
3. 如何用正则只匹配 `final=true` 的那一条，并命名 `text` / `id` 捕获组。
4. 语音助手的包名和服务组件是否真的需要管理。

新型号首次测试必须保持以下安全设置：

```json
{
  "manageVoicePackage": false,
  "disableVoicePackageWhenInactive": false,
  "stopAssistantAfterFinal": false,
  "startServiceFallback": false
}
```

确认按键和文本链路后，再逐项打开厂商专用行为。完整流程见[适配新设备](docs/adapt-new-device.md)与[配置字段说明](docs/configuration.md)。

## 构建与发布

源码仓库故意使用 Windows 自带的 .NET Framework 编译器，不要求安装 Visual Studio、.NET SDK 或第三方 NuGet 包：

```powershell
.\tests\smoke-tests.ps1
.\scripts\package-release.ps1 -Version 0.1.0
```

产物：

- `artifacts/app/AndroidDisplayVoiceBridge.exe`
- `artifacts/release/AndroidDisplayVoiceBridge-v0.1.0.zip`
- `artifacts/release/AndroidDisplayVoiceBridge-v0.1.0.zip.sha256`

仓库不分发 `adb.exe`。安装脚本从 Google 的官方 HTTPS 地址获取 Platform-Tools；用户也可以自行下载并校验来源。Android 官方 ADB 文档：[Android Debug Bridge](https://developer.android.com/tools/adb)。

当前社区构建没有商业代码签名，Windows SmartScreen 可能提示“未知发布者”。从 Releases 下载时请核对同版本 `.sha256`，或直接从源码运行构建脚本；不要关闭全局安全功能。

## 兼容性与安全边界

- ADB 只应在可信局域网启用，绝不能把调试端口映射到公网。
- 厂商语音识别可能把音频发送到厂商云端；本项目只读取显示器本地暴露的最终文本日志，不上传音频或文本。
- 默认日志不记录识别正文；设置 `logRecognizedText: true` 才会记录内容。
- `force-stop` / `pm enable` / `pm disable-user` 会改变显示器上的语音助手行为。只有经过验证的 profile 才应启用这些操作。
- `keepAndroidAwakeWhilePowered` 可缓解部分固件待机后关闭 ADB，但会增加 Android 系统待机功耗；通用 profile 默认关闭。
- Redmi G Pro 27U 2026 固件若在完整重启后复位 ADB，可选装设备端 [ADB Keeper](companion/adb-keeper/README.md)。它需要一次性 ADB 授权，并会让 5555 长期存在于可信局域网。
- 本方法依赖厂商未屏蔽的系统日志。日志被裁剪、权限收紧或语音服务变更后，可能无法适配。
- 仅支持 Windows 文本注入；Linux/macOS 尚未实现。

更多信息见[隐私与安全](docs/privacy-security.md)、[架构说明](docs/architecture.md)和[故障排查](docs/troubleshooting.md)。

## 文档导航

- [前置条件与兼容性判断](docs/prerequisites.md)
- [Redmi G Pro 27U 2026 完整教程](docs/redmi-g-pro-27u-2026.md)
- [显示器端 ADB Keeper](companion/adb-keeper/README.md)
- [适配其他 Android 智能显示器](docs/adapt-new-device.md)
- [配置字段说明](docs/configuration.md)
- [故障排查](docs/troubleshooting.md)
- [架构说明](docs/architecture.md)
- [隐私与安全](docs/privacy-security.md)
- [GitHub 发布清单](docs/publishing.md)
- [参与贡献](CONTRIBUTING.md)

## License

[MIT](LICENSE)。设备和系统名称属于各自权利人。
