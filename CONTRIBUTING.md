# 参与贡献

欢迎提交新设备 profile、脱敏日志 fixture、文档改进和平台实现。

## 开始前

1. 阅读[前置条件](docs/prerequisites.md)和[适配新设备](docs/adapt-new-device.md)。
2. 不要绕过厂商安全机制、root 用户设备或利用未公开漏洞。
3. 不要提交真实 IP、设备 serial、账号、令牌、ADB 私钥、完整 logcat 或个人语音文本。

## 新设备 profile 的最低要求

- 文件名包含厂商、型号，必要时包含固件系列。
- PR 描述完整型号、Android 版本、系统 build 和实测日期。
- 明确标注“只读链路已测”还是 `force-stop` / fallback 也已测。
- 附上最小、脱敏的按键事件与最终 ASR 日志 fixture。
- 未验证的系统包开关保持 `false`。
- 至少重启设备一次，确认 input event 编号是否稳定。

## 本地检查

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\tests\smoke-tests.ps1
```

测试会编译程序、执行内置结构自检、验证全部 profile、测试红米 ASR fixture，并扫描不应公开的本机信息。

## 代码约定

- 保持 .NET Framework 4.8 与 Windows 自带 `csc.exe` 可编译，不无故增加依赖。
- ADB shell 参数必须来自结构化、经过白名单验证的配置字段。
- 新的破坏性或持久化设备操作必须默认关闭，并在文档中给出回退命令。
- 日志默认不得记录识别正文。
- 修复应附上能够复现失败的最小 fixture 或测试。

## Pull Request

一个 PR 尽量只解决一个问题。请说明测试设备、执行过的命令、预期行为和实际结果。设备兼容性结论应区分“实机验证”与“推测可适配”。
