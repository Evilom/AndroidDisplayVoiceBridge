# 前置条件与兼容性判断

## 先判断你的“显示器”是哪一类

| 类型 | 典型特征 | 本方案 |
|---|---|---:|
| Android / HyperOS 智能显示器或电视 | 有应用、账号、语音助手和系统设置，可开启 ADB | 可能兼容，需探测 |
| Android 系统，但 ADB 或 logcat 被厂商锁定 | 有语音助手，无法授权调试或看不到最终文本 | 通常不可用 |
| 普通显示器 | 只有 HDMI / DP / USB-C 菜单，没有 Android 系统 | 不兼容 |
| 厂商私有系统 | 无 Android ADB，或遥控器只与专用盒子通信 | 本实现不兼容 |

品牌、价格和“国产”标签都不能决定兼容性。关键是下面四条链路必须同时成立：

1. Windows 能通过 ADB 连接显示器。
2. 遥控器麦克风键能在 Android input 子系统中观察到 DOWN / UP。
3. 显示器能用自己的语音服务完成识别。
4. 最终识别文本能从当前用户可读的 logcat 中稳定提取。

## 必需硬件与软件

- Windows 10 / 11 x64；推荐使用普通用户权限运行。
- .NET Framework 4.8。Windows 11 通常已包含；若编译脚本提示找不到 `csc.exe`，请启用 Windows 的 .NET Framework 4.x 功能。
- Google Android SDK Platform-Tools，不要从来历不明的网盘下载 ADB。
- 智能显示器和电脑连接同一个可信 LAN。
- 遥控器已经与显示器配对，原生语音功能在显示器上可用。
- 有权限在显示器上开启开发者模式、ADB，并确认 RSA 指纹。
- 显示器能访问厂商语音识别所需网络。

不需要：

- Windows 麦克风或虚拟声卡；
- 微信输入法、系统听写快捷键；
- USB-C 音频回传；
- 为本项目单独配置 VPN。

## 开启开发者模式

### 小米 / Redmi 的实测路径

1. 打开“设置 → 关于”。
2. 连续点击“产品型号”7 次。
3. 返回“设置 → 账号与安全”。
4. 将“ADB 调试”设为允许。

这一路径来自[小米官方支持文章](https://www.mi.com/global/support/article/KA-06513/)。某些中文固件会把菜单翻译成“关于本机”“账户与安全”或“允许 ADB 调试”。如果系统信息页没有“版本号”，不要卡在这里；小米设备使用的入口可能正是“产品型号”。

小米开发者站的旧版电视调试指南也给出了“设置 → 账号与安全 → 允许 ADB 调试”的路径：[小米电视应用调试](https://dev.mi.com/docs/mitv/user_guide/)。旧文档只用来理解入口，具体 UI 以当前固件为准。

### 其他 Android TV / 智能显示器

Android TV 的通用做法是进入“设置 → 设备 → 关于”，重复点击 Build，随后在开发者选项中打开 USB / 网络调试。Google 的官方电视开发文档可作参考：[Create and run a TV app](https://developer.android.com/training/tv/get-started/create)。厂商可能重命名、隐藏或彻底关闭该入口。

## ADB 连接方式

### 传统 TCP 调试端口

许多电视和智能屏使用固定的 5555 端口：

```powershell
adb connect 192.168.1.100:5555
adb devices -l
```

显示 `unauthorized` 时查看显示器屏幕，确认 RSA 指纹；显示 `offline` 时断开重连：

```powershell
adb disconnect 192.168.1.100:5555
adb connect 192.168.1.100:5555
```

### Android 11+ 无线调试配对

部分设备使用“无线调试”页面显示的配对端口与连接端口，它们不一定是 5555：

```powershell
adb pair 192.168.1.100:37001
adb connect 192.168.1.100:42137
```

把 `adb connect` 使用的地址写入 `devices[].serial`。Google 官方说明见[通过 Wi-Fi 连接设备](https://developer.android.com/tools/adb#wireless)。电视厂商可能只实现传统 5555 模式。

## 网络与安全准备

- 在路由器里给每台显示器做 DHCP 地址保留，避免重启后 IP 变化。
- 将电脑和显示器放在家庭或工作室可信网络，不要使用商场、酒店等公共 Wi-Fi。
- 不要在路由器中转发 5555 或无线调试端口，也不要把 ADB 暴露到公网。
- 不再使用时可以关闭显示器 ADB；若项目设置了开机启动，先退出或卸载桥接程序。

## 五分钟兼容性预检

连接成功后执行：

```powershell
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode summary
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode keys
```

按遥控器麦克风键时，如果完全没有任何 `EV_KEY` 变化，可能是：

- 按键被蓝牙固件或专用服务直接消费；
- 选错了设备地址；
- 遥控器没有和这台显示器配对；
- 厂商内核没有向 `getevent` 暴露该事件。

看到按键事件后，再进行[日志与 profile 适配](adapt-new-device.md)。
