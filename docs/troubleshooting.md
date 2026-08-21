# 故障排查

先在通知区域右击程序图标打开日志。默认路径：

```text
%LOCALAPPDATA%\AndroidDisplayVoiceBridge\bridge.log
```

## 常见问题

| 现象 | 常见原因 | 处理 |
|---|---|---|
| `unauthorized` | 显示器尚未确认 RSA | 查看显示器弹窗；必要时撤销调试授权后重连 |
| `offline` / 反复断开 | IP 变化、端口变化、休眠或网络隔离 | 核对 IP；做 DHCP 保留；检查 AP 隔离 |
| 开关显示已开，但 5555 拒绝连接 | 固件待机后停止了 `adbd` | 关→开 ADB；确认功耗后启用 `keepAndroidAwakeWhilePowered` |
| 按遥控器没有 `microphone DOWN` | `inputDevice` / `keyToken` 错误 | 重新运行 `diagnose-device.ps1 -Mode keys` |
| 有 DOWN / UP，没有最终文本 | logcat tag 或正则错误；云 ASR 不可用 | 用 `-Mode logs` 重新取样；先验证原生助手能识别 |
| 有 `ignored unarmed final ASR` | 最终日志超过等待窗口，或按键事件没有被捕获 | 先修按键配置；必要时调大 timeout |
| 文本输入两次 | 日志有多个 final、两台设备收到同一事件 | 收紧正则；检查 serial；调整重复抑制窗口 |
| 文本进入了错误窗口 | 识别期间切换了焦点 | 松开遥控器后不要切换窗口；项目按完成时的当前光标输入 |
| 普通窗口可用，管理员窗口不可用 | Windows UIPI 权限隔离 | 让目标应用用普通权限运行；不要为了方便长期以管理员运行本程序 |
| 小爱仍继续回答 | `stopAssistantAfterFinal` 未启用或包名不对 | 只在确认包名后启用；查看 force-stop 退出码 |
| 下一次按键不再识别 | 强停后系统没有拉起服务 | 关闭 stop；或在实测组件后配置 service fallback |
| 中文乱码 | 旧版 ADB / 固件日志编码异常 | 更新 Google Platform-Tools；检查原始 logcat 是否已乱码 |
| “录音设备异常，语音输入中断” | 触发的是 Windows / 输入法语音，不是本项目 | 不使用系统听写快捷键；确认通知区域桥接程序和 ADB 监听运行 |

## Windows 提示未知发布者

当前社区构建没有商业代码签名，SmartScreen 可能拦截新下载的 EXE。这不等于应当关闭 SmartScreen。优先做法：

1. 只从项目正式 Releases 获取压缩包。
2. 用同版本 `.sha256` 文件核对下载内容。
3. 有疑虑时审阅源码并在本机运行 `scripts\build.ps1`，不要运行来源不明的二次打包版本。

## ADB 已连接，但程序一直重连

检查配置中的 serial 是否与 `adb devices -l` 完全一致，包括端口：

```powershell
adb devices -l
```

如果 Android 11+ 无线调试每次重启更换连接端口，必须使用当前“无线调试”页面中的连接地址。能够在路由器固定 IP，并不一定能固定随机 ADB 端口。

## 找不到开发者模式

- Redmi / 小米先尝试“关于 → 产品型号连续点击 7 次”，不是只找版本号。
- 其他 Android TV 常见入口是“关于 → Build 连续点击”。
- 企业版、酒店版、运营商版或定制固件可能彻底禁用 ADB。
- 若厂商没有提供入口，本项目不会尝试漏洞、root 或绕过系统安全策略。

## `event6` 升级后失效

`eventN` 是内核枚举结果，不是永久设备 ID。重新执行：

```powershell
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode keys
```

更新 `inputDevice`。同时确认按键名是否仍为原值。

## 正则看似正确，但始终不输入

检查这几个细节：

1. JSON 中 `\s` 必须写成 `\\s`。
2. 使用 `(?<text>...)` 捕获正文，而不是把前缀也捕获进去。
3. 只匹配 `isFinal=true`；partial 结果会不断变化。
4. 日志 filter 必须包含产生该行的 tag。
5. 按键触发后的等待窗口内必须收到 final 行。

可以把脱敏日志写到 `tests/fixtures/`，用 PowerShell 的 `[regex]::Match()` 单独验证。

## 恢复被禁用的语音包

只有已确认包名时才执行：

```powershell
adb -s 192.168.1.100:5555 shell pm enable --user 0 com.vendor.voice
```

不要复制示例包名到未知设备。若 profile 保持 `disableVoicePackageWhenInactive: false`，正常退出不会禁用语音包。

## 收集可公开的故障信息

提交 Issue 时提供：

- 项目版本、Windows 版本；
- 显示器厂商、完整型号、系统 build；
- `adb devices -l` 中脱敏后的状态；
- 按键对应 event / key；
- 一两行去掉语音正文、IP、设备 serial 的相关日志；
- 哪些安全开关已启用。

不要上传整个 logcat、家庭 IP、账号、令牌、ADB 密钥或真实语音内容。
