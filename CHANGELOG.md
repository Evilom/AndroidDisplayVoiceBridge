# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 的结构。

## [Unreleased]

### Added

- 可选的显示器端 ADB Keeper，在端口停止监听时重新启用网络 ADB。
- Redmi G Pro 27U 2026 / HyperOS 3 的 `com.example.autoreboot` 白名单兼容构建、双机重启验证和安装脚本。

### Security

- 文档明确说明 `WRITE_SECURE_SETTINGS`、长期开放 5555 和固件专用包身份的风险边界。

## [0.1.0] - 2026-08-21

### Added

- Windows 通知区域桥接程序，支持多台 Android 智能显示器。
- 网络 ADB 自动重连，以及可选的 Android 接电常驻策略。
- 遥控器按键 armed 窗口、最终 ASR 提取、dialog 与短时正文去重。
- 使用 `SendInput` 的 Unicode 文本注入，并修正 x64 `INPUT` union 布局。
- Redmi G Pro 27U 2026 / HyperOS 3 实测 profile。
- 通用安全 profile、设备探测、安装、卸载、构建和发布脚本。
- 中文教程、适配流程、故障排查、隐私与安全说明。
