# 配置字段说明

运行时配置默认为 EXE 同目录的 `config.json`，也可使用：

```powershell
AndroidDisplayVoiceBridge.exe --config "D:\path\to\config.json"
```

修改配置前先退出通知区域程序，保存后重新启动。

## 顶层字段

| 字段 | 作用 | 建议 |
|---|---|---|
| `adbPath` | `adb.exe` 的绝对路径，或相对 EXE 的路径 | 安装版使用 `platform-tools\adb.exe` |
| `logDirectory` | 运行日志目录，支持环境变量 | 默认 `%LOCALAPPDATA%\AndroidDisplayVoiceBridge` |
| `behavior` | 全局输入与重连行为 | 见下表 |
| `devices` | 一台或多台显示器配置 | 每个 `serial` 必须唯一 |

## `behavior`

| 字段 | 默认 | 说明 |
|---|---:|---|
| `showNotifications` | `true` | 输入成功后显示 Windows 通知 |
| `logRecognizedText` | `false` | 是否把识别正文写入日志；涉及隐私，建议关闭 |
| `armTimeoutSeconds` | `30` | 按键按下后允许最终 ASR 的最长时间 |
| `finalTimeoutSeconds` | `15` | 按键松开后继续等待最终 ASR 的时间 |
| `reconnectDelayMilliseconds` | `2500` | ADB 流断开后的重试间隔 |
| `duplicateSuppressionMilliseconds` | `1500` | 相同正文的短时重复抑制窗口；`0` 表示关闭 |

## `devices[]`

| 字段 | 说明 |
|---|---|
| `name` | 日志中显示的友好名称 |
| `enabled` | 是否启动该设备的监听 |
| `serial` | ADB serial，例如 `192.168.1.100:5555` 或 USB serial |
| `autoConnect` | 网络 serial 断线后是否执行 `adb connect` |
| `keepAndroidAwakeWhilePowered` | 连接成功后设置 Android 接电常驻，减少待机关闭 `adbd`；可能增加待机功耗 |
| `inputDevice` | 遥控器按键所在节点，例如 `/dev/input/event6` |
| `keyToken` | `getevent -l` 输出的按键名，例如 `KEY_F5` |
| `logcatFilter` | 最小化的 logcat tag 过滤器，例如 `VoiceService:I` |
| `finalAsrPattern` | 只匹配最终识别结果的 .NET 正则 |
| `voicePackage` | 厂商语音助手包名；只读取链路可留空 |
| `manageVoicePackage` | 启动时确保语音包 enabled |
| `disableVoicePackageWhenInactive` | 暂停/退出时禁用语音包；默认和多数设备都应为 false |
| `stopAssistantAfterFinal` | 输入最终文本后 `force-stop` 语音包 |
| `startServiceFallback` | 按键没有拉起语音进程时，主动调用服务 fallback |
| `serviceComponent` | fallback 的 Android service 组件 |
| `keyDownAction` / `keyUpAction` | fallback 对应的 Android intent action |
| `remoteType` | fallback 传给服务的厂商遥控器类型整数 |

## 正则捕获组

首选命名组：

```regex
(?<text>...要输入的文本...).*?(?<id>...会话 ID...)
```

程序也兼容第 1 / 第 2 个普通捕获组，但命名组更不容易因后续加括号而错位。`id` 可选；缺失时程序用整行哈希临时去重。

## 多显示器示例

```json
{
  "devices": [
    {
      "name": "左侧显示器",
      "enabled": true,
      "serial": "192.168.1.101:5555"
    },
    {
      "name": "右侧显示器",
      "enabled": true,
      "serial": "192.168.1.102:5555"
    }
  ]
}
```

上面只展示差异字段，不是完整可运行配置；请复制完整 profile 后再增加设备对象。

## 配置验证

构建后可运行无界面的校验：

```powershell
$process = Start-Process .\artifacts\app\AndroidDisplayVoiceBridge.exe `
  -ArgumentList @('--validate-config', '.\config\profiles\redmi-g-pro-27u-2026.json') `
  -Wait -PassThru
$process.ExitCode
```

退出码 `0` 表示 JSON 与字段约束通过。它不代表目标设备在线，也不替代实机探测。
