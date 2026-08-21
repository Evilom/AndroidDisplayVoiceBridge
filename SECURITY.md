# Security Policy

## Supported versions

项目仍处于 `0.x` 实验阶段。安全修复只保证进入最新版本，请先在最新版复现。

## Reporting a vulnerability

如果仓库已启用 GitHub Private Vulnerability Reporting，请使用仓库 Security 页面中的“Report a vulnerability”，不要公开创建包含利用细节的 Issue。

若尚未启用，请只创建一个不含技术细节和个人数据的 Issue，请求维护者提供私下联系渠道。仓库所有者发布前应在 GitHub 设置中启用 Private vulnerability reporting。

报告请包含：

- 受影响版本；
- 影响范围与最小复现条件；
- 是否涉及 ADB 命令注入、日志泄露、权限提升或安装脚本供应链；
- 建议的修复方向（如有）。

不要附带 ADB 私钥、真实局域网地址、账号、访问令牌或未经脱敏的语音日志。

## Security design notes

- 项目不需要 root。
- 配置中的 serial、包名、input 节点和 action 经过白名单校验。
- Google Platform-Tools 只从官方 HTTPS 地址下载；仓库本身不提交 ADB 二进制。
- 通用 profile 默认不管理显示器系统包。
- 识别正文默认不写入日志。
