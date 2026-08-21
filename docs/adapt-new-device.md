# 适配其他 Android 智能显示器

这份流程适用于基于 Android / HyperOS 的电视、智慧屏或带系统的显示器。所有探测先保持只读；只有确认包名和行为后，才允许桥接程序管理系统语音包。

## 0. 从安全模板开始

复制：

```text
config/profiles/generic-android-display.safe.json
```

新文件建议命名为：

```text
config/profiles/<brand>-<model>-<firmware>.json
```

探测期间保持 `enabled: false`，以及所有包管理开关为 `false`。

## 1. 连接并记录设备身份

```powershell
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode summary
```

至少记录：

- `ro.product.manufacturer`
- `ro.product.model`
- `ro.product.name`
- `ro.build.version.release`
- `ro.build.version.incremental`
- `ro.build.fingerprint`

公开 profile 时可以公开型号和 build，但不要提交家庭 IP、ADB RSA 私钥、账号、完整日志或个人语音正文。

## 2. 找到遥控器按键事件

```powershell
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode keys
```

先不碰遥控器，观察基线；然后只按一次麦克风键。典型输出：

```text
/dev/input/event6: EV_KEY KEY_F5 DOWN
/dev/input/event6: EV_KEY KEY_F5 UP
```

把事件节点写入 `inputDevice`，按键名写入 `keyToken`。其他设备可能是 `KEY_VOICECOMMAND`、`KEY_ASSISTANT`、`KEY_SEARCH` 或别的值，不要猜。

若同时出现多个 event 节点，分别限制节点测试：

```powershell
adb -s 192.168.1.100:5555 shell getevent -lt /dev/input/event3
```

事件编号可能在重启或接入新 USB 设备后变化。稳定 profile 应在目标固件上重启验证至少一次。

## 3. 找到最终 ASR 文本

```powershell
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode logs
```

按住遥控器说一句不会包含隐私的测试短语，例如“遥控器语音测试一二三”。重点寻找：

- 日志 tag；
- 临时 partial 文本和最终 final 文本的区别；
- `isFinal=true`、`finalResult` 或等价标记；
- `dialogId`、session id 或 request id。

然后把 `logcatFilter` 收窄到最少的 tag，避免长期读取全量日志。不要在正式 profile 中使用宽泛的 `*:V`。

`finalAsrPattern` 使用 .NET 正则，推荐两个命名捕获组：

```regex
...(?<text>最终文本)...(?<id>会话标识)...
```

- `text` 必须只包含要输入的正文。
- `id` 用于去重；日志没有会话 ID 时可以省略，程序会用整行生成临时标识。
- 正则必须只命中最终结果，不能命中 partial 结果。

JSON 中的反斜杠需要写成双反斜杠，例如 `\s` 写成 `\\s`。

## 4. 找到语音包，但先不要操作

```powershell
.\scripts\diagnose-device.ps1 -DeviceAddress 192.168.1.100 -Mode packages
```

候选包名不等于已验证包名。可以只读确认进程和组件：

```powershell
adb -s 192.168.1.100:5555 shell pidof com.vendor.voice
adb -s 192.168.1.100:5555 shell dumpsys package com.vendor.voice
```

不要对不确定的包执行 `disable-user`、清除数据或卸载。尤其不要根据包名里含有 `voice` 就下结论。

## 5. 先验证纯读取链路

设置：

```json
{
  "enabled": true,
  "keepAndroidAwakeWhilePowered": false,
  "manageVoicePackage": false,
  "disableVoicePackageWhenInactive": false,
  "stopAssistantAfterFinal": false,
  "startServiceFallback": false
}
```

启动桥接程序，在记事本中测试：

1. 按键 DOWN 时日志出现 `ASR armed`。
2. 松开后出现 `waiting for final ASR`。
3. 最终文本只输入一次。
4. 不按遥控器时，其他 logcat 文本不会写入 Windows。

只有这四点都通过，才能考虑助手抑制。

若设备只在待机后关闭 ADB，确认面板息屏行为与功耗可以接受后，再尝试 `keepAndroidAwakeWhilePowered: true`。它会修改 Android 的接电常驻设置，不应作为所有设备的默认值。

## 6. 逐项验证厂商专用行为

### `stopAssistantAfterFinal`

它会执行：

```text
adb shell am force-stop <voicePackage>
```

先确认强停后，下一次物理按键能由系统正常拉起助手。若不能，不要启用。

### `manageVoicePackage`

程序启动时会执行 `pm enable --user 0`。仅当你确认目标包确实是独立语音助手包时启用。

### `disableVoicePackageWhenInactive`

暂停或退出时执行 `pm disable-user --user 0`，侵入性更强。绝大多数 profile 应保持 `false`。

### `startServiceFallback`

需要准确的 `serviceComponent`、按下/松开 action 和 `remoteType`。这些值必须来自当前机型的组件与日志验证，不能从红米 profile 猜测。配置错误时 Android 会拒绝命令，严重时可能干扰原生语音交互。

## 7. 双机与多机

复制 `devices` 中的对象，为每台设备填写唯一 `name` 和 `serial`。不要让两个条目指向同一 serial。每台设备可以使用不同的 input 节点、日志正则和语音包设置。

## 8. 提交社区 profile

提交 PR 前：

```powershell
.\tests\smoke-tests.ps1
```

PR 中说明：

- 厂商、完整型号、Android / 系统版本与 build；
- 单机还是双机测试；
- 按键事件与最终 ASR 的脱敏日志样例；
- 哪些高级开关实际测试过；
- 固件升级后是否复测。

不要提交真实局域网 IP、设备序列号、账号、令牌、ADB 私钥、完整 logcat 或日常语音内容。
