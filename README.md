# QQ 头像工具 · Windows 11

根据原易语言头像上传工程的协议制作的 C# / WinForms 移植版。当前发布版本为 **v1.0.1**，附 Windows 微信 4.1.15.13 的适配研究记录。

## 下载与运行

[下载 QQAvatarTool.exe](./bin/QQAvatarTool.exe)（打开文件页后点击 **Download raw file**）。无需易语言、Python、Node.js 或原工程第三方 DLL；运行环境为 Windows 的 .NET Framework 4.x。

程序启动不会连接服务器、扫描 QQ 或更换头像。上传需要手动点击。

## 功能与状态

| 功能 | 状态 |
| --- | --- |
| 生成透明 PNG、调整静态图片不透明度 | 已实现并完成离线测试 |
| GIF 转 APNG | 已实现，保留帧和时长；预览显示首帧 |
| QQNT 头像上传 | 使用已登录、已配置的本地 NapCat HTTP 服务；不需要旧 UKey |
| 旧 QQ 普通 / 动态头像、小世界图层、群头像 / 封面、名片背景 | 保留原协议；需要有效旧 UKey、相应权限及仍可用的服务器接口 |
| 原版 QQNT 直接提取 UKey | 未实现 |
| 微信透明、半透明、动态头像上传 | 尚未完成适配；本仓库没有微信上传功能 |

QQNT 上传已收到使用者的成功反馈，但 NapCat 也可能在头像实际更新后返回失败。遇到报错应先在 QQ 中核对结果，避免重复上传。透明度和动画是否在不同客户端中保留、播放仍需实际确认。

## QQNT / NapCat 使用

1. 启动并登录 [NapCat](https://github.com/NapNeko/NapCatQQ)，在网络配置中开启 **HTTP 服务器**。
2. 在工具中点击 **QQNT 桥接设置**，填写 HTTP 地址，例如 `http://127.0.0.1:3000/`，以及该 HTTP 服务的 Token。WebUI 的 `6099` 端口不是上传 API 地址。
3. 点击“应用到本次运行”，工具会切换到 **QQNT 普通头像（本地 NapCat）**。
4. 输入 NapCat 当前登录的 QQ 号，选择图片，点击上传。
5. 在 QQ 中检查实际头像。这个模式无需填写 UKey。

上传前会通过 `get_login_info` 核对账号，然后调用 `set_qq_avatar`。v1.0.1 使用带图片扩展名的本机临时文件，完成响应后清理，避免 Base64 被接口错误信息重复回传而造成超大响应。桥接仅允许 IPv4 回环地址 `127.0.0.1`，Token 仅保存在本次运行的内存中。

旧模式中的 UKey 是原工具的 104 字节票据（208 个十六进制字符），不能用网页 Cookie、skey 或 NapCat Token 代替。详细旧模式操作和协议说明见 [QQ 分析与使用说明](docs/qq-analysis.md)。

## 从源码编译

在 Windows PowerShell 中运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\src\build.ps1
```

输出为 `bin/QQAvatarTool.exe`。脚本调用 Windows .NET Framework 自带的 `csc.exe`，不联网下载依赖。

运行离线自测：

```powershell
$testReport = Join-Path $env:TEMP 'qq-avatar-self-test.txt'
$testProcess = Start-Process -FilePath .\bin\QQAvatarTool.exe -ArgumentList @('--self-test', ('"' + $testReport + '"')) -Wait -PassThru
Get-Content -LiteralPath $testReport
if ($testProcess.ExitCode -ne 0) { throw '自测失败' }
```

离线自测覆盖 protobuf/TCP 帧、图片透明度、GIF/APNG、NapCat 模拟 HTTP 服务、账号核对、临时文件清理、大错误响应及错误脱敏。它不会连接真实 QQ 服务或使用真实账号票据。

## 项目内容

- `src/`：v1.0.1 C# 源码、构建脚本和测试图片。
- `bin/`：可执行文件及 SHA-256 校验值。
- `docs/`：QQ 协议分析、v1.0.1 修复说明、验证记录和微信研究结果。

原易语言工程中的 EC 模块、Calculation.dll、截图，以及下载用于研究的 NapCat / 微信 Hook 项目不随本仓库分发。未完成的 v1.0.2 修改也未合入。

## 来源

原工程作者署名：千雨。协议来源为其易语言头像上传工程；相关说明可参见 [QQAvatarTools](https://github.com/Kuroba-Sayuki/QQAvatarTools)。NapCat 接口使用及其他研究来源见 [来源说明](THIRD_PARTY_NOTICES.md)。

本仓库尚未指定统一许可证，不将原工程的“源码公开”自行视为 MIT 等授权。
