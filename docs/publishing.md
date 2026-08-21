# GitHub 发布清单

## 1. 发布前检查

```powershell
.\tests\smoke-tests.ps1
git diff --cached --check
git status --short
```

确认：

- 没有家庭 IP、设备 serial、账号、密码、令牌、ADB 私钥或真实语音正文；
- `config.json`、`platform-tools/`、EXE、ZIP 和日志没有被提交；
- README 中的已验证型号、固件和日期仍然准确；
- `SECURITY.md` 符合你准备采用的漏洞报告方式；
- 你接受 MIT License。

## 2. 创建 GitHub 仓库

建议名称：`AndroidDisplayVoiceBridge`

建议描述：

```text
把 Android / HyperOS 智能显示器的语音遥控器变成 Windows 无线语音输入器
```

建议 topics：

```text
android-tv adb windows speech-to-text voice-remote redmi hyperos vibe-coding
```

在 GitHub 网页新建空的 Public 仓库时，不要再次自动生成 README、`.gitignore` 或 License，避免第一次 push 产生无意义的合并。

## 3. 提交与推送

本地仓库已经使用 `main` 分支并暂存公开文件，但没有替你创建 commit。审阅后运行：

```powershell
git commit -m "feat: initial public release"
git remote add origin https://github.com/<YOUR_GITHUB_NAME>/AndroidDisplayVoiceBridge.git
git push -u origin main
```

如果使用并已登录 GitHub CLI，也可以在 commit 后运行：

```powershell
gh repo create AndroidDisplayVoiceBridge --public --source . --remote origin --push
```

不要同时使用网页 remote 命令和 `gh repo create` 重复添加 `origin`。

## 4. 创建 v0.1.0 Release

```powershell
.\scripts\package-release.ps1 -Version 0.1.0
```

上传这两个文件：

```text
artifacts/release/AndroidDisplayVoiceBridge-v0.1.0.zip
artifacts/release/AndroidDisplayVoiceBridge-v0.1.0.zip.sha256
```

GitHub CLI 示例：

```powershell
gh release create v0.1.0 `
  .\artifacts\release\AndroidDisplayVoiceBridge-v0.1.0.zip `
  .\artifacts\release\AndroidDisplayVoiceBridge-v0.1.0.zip.sha256 `
  --title "Android Display Voice Bridge v0.1.0" `
  --notes-file .\CHANGELOG.md
```

发布说明应再次提醒：

- 当前社区 EXE 未做商业代码签名；
- 先阅读前置条件，普通显示器不兼容；
- Redmi profile 是实机验证，其他型号必须探测；
- ADB 只在可信 LAN 使用，不得暴露公网。

## 5. GitHub 仓库设置

建议启用：

- Issues；
- Discussions（收集不同型号参数）；
- Security → Private vulnerability reporting；
- Actions（仓库已有 Windows 构建 workflow）；
- 分支保护：要求构建检查通过后再合并。

首发后可以建一个“设备兼容性汇总”Discussion，要求网友只提交脱敏数据，并把通过验证的结果逐步合并为 profile。
