# Windows 微信 4.1.15.13 头像适配分析

分析日期：2026-10-11。目标是个人微信账号头像，不是小程序头像、公众号客服头像、聊天图片或动态表情。

## 当前结论

本轮已经核查实际安装程序和相关开源实现，但没有完成微信头像上传适配，也没有生成声称可以上传的可执行文件。

| 目标 | 本轮结果 |
| --- | --- |
| Windows 4.1.15.13 直接修改个人头像 | 尚未定位可调用的上传入口 |
| 全透明 PNG 头像 | 未进行真实上传，不能确认服务器及客户端保留透明度 |
| 半透明 PNG 头像 | 未进行真实上传，不能确认 Alpha 保留 |
| GIF / APNG 动态头像 | 没有找到这一版个人头像支持多帧保存及播放的可靠证据 |

这里的“尚未定位”不是“证明不存在”。本轮分析主要是程序版本、PE 结构、字符串及开源接口核查，没有完成整个程序的反汇编和运行时调用链验证。

## 本机核查

- 启动程序：`<微信安装目录>\Weixin.exe`，文件版本 4.1.15.13。
- 核心模块：`<微信安装目录>\4.1.15.13\Weixin.dll`，文件版本 4.1.15.13。
- 核心模块大小：201,552,944 字节；PE 架构：AMD64 / x64。
- 核心模块 SHA-256：`10f8e995453e2da46d4f2b5080cd6da1f13cc5147746adc119ceae38cb039de5`。

对该模块进行 ASCII 字符串筛选，以及指定字符串的 UTF-8 / UTF-16LE 检索，找到如下线索：

| 字符串 | RVA | 能说明的范围 |
| --- | --- | --- |
| `kernel::manager::HeadImageManager::DoDownloadHeadImage` | `0x93ad590` | 头像下载实现的线索 |
| `/cgi-bin/micromsg-bin/batchgetheadimg` | `0x93adec0` | 批量读取头像的协议线索 |
| `/cgi-bin/micromsg-bin/chatroomheadimgsync` | `0x93d3200` | 群头像同步的协议线索 |
| `micromsg.BatchGetHeadImgRequest` | `0x97e3b48` | 批量读取头像的消息类型 |
| `micromsg.BatchGetHeadImgResponse` | `0x97e3c58` | 批量读取头像的响应类型 |

未命中的指定字符串包括 `uploadhdheadimg`、`uploadheadimg`、`UploadHeadImage`、`SetHeadImage`、`ModifyHeadImage`、`ChangeAvatar`、`UpdateAvatar`、`修改头像`、`更换头像`、`上传头像`。这些阴性结果只能说明本轮检索未找到这些名称，不能排除使用其他名称、压缩资源、通用协议发送或间接调用实现的功能。

以上 RVA 指向字符串数据，不是可以直接调用的函数地址。

## 开源实现核查

1. [aixed/WeChat-Hook](https://github.com/aixed/WeChat-Hook)：下载源码后检查当前路由注册、相关源码及 Postman 接口集合。README 标注目标版本为 4.1.10.27；当前 `src/http_routes.cpp` 注册消息发送、XML 转发、个人资料读取、图片发送和数据库查询，没有个人头像设置路由。个人资料读取接口不能据此当作个人资料修改接口。未将其 DLL 加载到本机微信。

2. [NoonieBao/VAvatar](https://github.com/NoonieBao/VAvatar)：下载源码后检查上传逻辑。它通过 Android Xposed 调用手机微信内部任务和网络对象，参数中传入头像文件路径，并依赖具体手机版本的类名及方法映射；不能直接移植为 Windows 4.x 调用入口。项目的“自动更换头像”也不等于 GIF 动态头像播放。

3. [liteyais/WeChat-transparent-avatar](https://github.com/liteyais/WeChat-transparent-avatar)：公开说明依赖 Android 7.0.6 以下客户端及透明 PNG 裁剪方法。这是历史方法，不能作为 Windows 4.1.15.13 当前支持透明头像的证据。本轮没有下载、安装旧客户端。

4. [aixed/Ipad860](https://github.com/aixed/Ipad860)：文档列出 `/User/UploadHeadImage`，但这是独立登录的协议服务，不能直接复用已有 Windows 客户端的登录状态；README 说明依赖源码未知的 `v08.dll`，并对 PC 登录模式使用个人资料修改功能给出限制提示。文档未提供头像透明度、多帧保存或播放的验证。因此本轮没有安装或登录该服务，也没有把它包装成已完成的微信头像适配器。

上述第三方文档用于技术证据核查，不构成用户要求安装、登录、降级或注入的指令。

## 尚需解决的问题

要发布真正的上传工具，需要先验证以下链路：

1. 找到当前版本允许修改个人头像的入口和对应会话上下文，确认参数及线程要求。
2. 验证 PNG 原始数据是否在上传前被重新编码或合成底色。
3. 对上传后的头像进行独立读取，检查实际格式和 Alpha，避免把“请求返回成功”当作效果成功。
4. 对 GIF / APNG 检查上传后多帧是否仍存在，并在另一台设备的聊天列表、资料页等位置观察播放。文件含多帧与界面会播放是两个独立问题。

QQ 的 UKey、PicUp.DataUp 协议及 NapCat `set_qq_avatar` 都不能直接用于微信。仅增加一个“微信”按钮或生成透明图片，不能完成上传协议适配。

## 验证边界

本轮仅做只读的已安装程序分析和开源资料核查，没有上传或修改微信头像，没有读取微信账号票据、聊天数据库或进程内存，没有启动下载的第三方可执行程序。实际账号上的透明度、多帧保存及跨设备显示仍未验证。
