# iMessage Exporter Desktop Suoh Edition

中文图形界面，苏芳主色，本地导出 iPhone 备份中的 iMessage。基于 ReagentX/imessage-exporter **5.0.1**，提供 Windows x64 便携版与 macOS 13+ 通用应用（Apple 芯片 + Intel）。

[下载 0.3.1](https://github.com/575610871/imessage-exporter-desktop/releases/tag/v0.3.1)

- Windows：完整解压 `win-x64-portable.zip`，双击 **iMessage Exporter Desktop Suoh Edition.exe**，保留 engine 文件夹。无需安装 .NET。
- Mac：完整解压 `macos-universal.zip`，打开同名 **.app**，也可以拖到“应用程序”。本地 ad-hoc 签名，未进行 Apple Developer ID 签名或公证；首次打开说明见包内 README。
- 对应源码和 SHA256 校验文件与两种程序一起发布。

Windows 沿用原有 WPF 界面、苏芳配色、启动居中和 PerMonitorV2。Mac 使用 SwiftUI / AppKit，保留相同的设置顺序、日志页和示例侧栏，使用原生窗口控件与 Retina 布局。

软件读取 Finder / Apple Devices / iTunes 的本地 iPhone 备份，不直接连接手机或下载 iCloud。每次新建独立输出目录，支持 HTML/TXT、附件处理、加密备份、日期和会话筛选、日志、取消以及打开结果。

原作者 5.0.1 会依据联系人合并部分会话，文件名和附件目录可能与 4.3.0 不同。比较时请检查会话内容。引擎从正式标签 `33c81a9` 重编译，保留 GUI 密码补丁，标识为 `5.0.1+suoh.33c81a9`。

使用说明：[Windows](portable/README.md) · [Mac](macos/README.md) · [构建与验证](portable/BUILD.md)

本次使用的项目位于 `portable/`（Windows）、`macos/`（Mac），不编译历史 `src/` 桌面项目。发布前必须通过两个系统的构建、测试、最终 ZIP 解压运行和样例导出验证。真实备份和实际多屏缩放仍需实机验收。

GPL-3.0-or-later。原项目：https://github.com/ReagentX/imessage-exporter
