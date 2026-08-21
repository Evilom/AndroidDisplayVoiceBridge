# 隐私与安全

## 语音去了哪里

遥控器音频由显示器及其厂商语音服务处理。是否上传云端、保存多久、在哪个地区处理，取决于显示器厂商的系统与账号设置。本项目不截取遥控器原始音频，也不提供离线 ASR。

桥接程序读取显示器 logcat 中已经产生的最终识别文本，并在本机调用 Windows `SendInput`。项目代码本身没有上传接口、遥测 SDK 或自动更新器。

## 本机日志

默认日志记录设备友好名称、ADB 状态、事件状态、dialog ID 和字符数，不记录识别正文。以下配置会显式改变这一点：

```json
"logRecognizedText": true
```

调试结束后应恢复 `false`，分享日志前再次脱敏。Windows 通知可能显示识别预览；在共享屏幕或直播环境可设置 `showNotifications: false`。

## ADB 风险

获得授权的 ADB 客户端能执行的操作远多于本项目实际使用的命令。务必：

- 只在可信家庭 / 工作室 LAN 开启；
- 不做公网端口转发；
- 不在公共 Wi-Fi 使用；
- 只在自己的显示器上确认 RSA 指纹；
- 不分享 `%USERPROFILE%\.android\adbkey*`；
- 不再使用时撤销调试授权或关闭 ADB。

Google 对 ADB 及无线调试的说明见[官方文档](https://developer.android.com/tools/adb)。

## 系统包操作

红米实测 profile 可执行：

- `pm enable --user 0 <package>`
- `am force-stop <package>`
- `am startservice ...`

只有 `disableVoicePackageWhenInactive: true` 才会在暂停/退出时执行 `pm disable-user`。该值默认关闭。

新 profile 不得在没有实机确认的情况下开启包管理。项目不需要 root，不会卸载系统应用，不会清除应用数据。

## 报告安全问题

不要在公开 Issue 中发布可利用细节、ADB 私钥或个人数据。请按仓库的 [SECURITY.md](../SECURITY.md) 先私下联系维护者；仓库创建者应在发布前把其中的联系占位符换成自己的安全邮箱或 GitHub Security Advisory 流程。
