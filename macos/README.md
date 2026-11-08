# iMessage Exporter Desktop Suoh Edition 0.3.1 · Mac

macOS 13 或更新版本，支持 Apple 芯片与 Intel。原生 SwiftUI / AppKit 界面，保留苏芳配色和原 Windows 版的操作布局。应用与导出引擎都包含两种架构，无需安装 .NET、Python、Rust 或其他运行时。

## 打开

1. 完整解压 `imessage-exporter-desktop-suoh-edition-0.3.1-macos-universal.zip`。
2. 双击文件夹中的 **iMessage Exporter Desktop Suoh Edition.app**。也可以将 .app 拖入“应用程序”。
3. 本版仅有本地 ad-hoc 签名，没有 Apple Developer ID 签名或公证。macOS 可能阻止首次打开；核对来源和 SHA256 后，可在系统设置的“隐私与安全性”中选择“仍要打开”。具体步骤见 [Apple 官方说明](https://support.apple.com/en-us/102445)。无需关闭 Gatekeeper 或运行移除隔离属性的命令。

使用前先在 Finder 完成 iPhone 的本地备份。选择同时包含 `Manifest.plist` 与 `Manifest.db` 的设备备份根目录，也可点击“查找本机备份”。默认位置为 `~/Library/Application Support/MobileSync/Backup`。如果系统报告权限不足，在系统设置的“隐私与安全性 → 完全磁盘访问权限”中按需授权本应用，再重新打开。

选择输出位置、HTML / TXT、附件处理方式。加密备份填写本地备份密码，然后开始导出。每次建立独立文件夹，不覆盖已有导出。界面目前选择 iPhone 本地备份；底层引擎也可在命令行导出 Mac 本机 chat.db。

“保留原文件”无需额外工具；“转换图片”使用 macOS 系统工具。“完整媒体转换”的视频部分需要 FFmpeg。可使用已有的 Homebrew / PATH 工具，或将工具及完整依赖放在 .app 旁的 `tools` 文件夹。

## 窗口与设置

每次启动在鼠标所在屏幕的可用区域居中，限制窗口大小以保留标题栏。使用原生关闭、最小化、全屏按钮和 Retina 布局；窄窗口隐藏示例侧栏，矮窗口折叠大标题，表单独立滚动。⌘0 重新居中，⌘L 打开日志。

仅勾选“记住目录和导出格式”时保存设置，位置为 `~/Library/Application Support/IMessageExporterDesktopSuohEdition/preferences.json`。取消勾选会删除设置。应用不修改签名后的 .app，不记录密码、不自动保存日志、不上传聊天内容。

密码通过子进程环境传递；本机有调试权限的程序仍可能读取进程环境或内存。每项任务使用独立的系统临时目录；正常完成或取消后清理，意外退出可能留下临时文件。取消时保留已有的部分导出，它们不代表完整结果。

## 5.0.1 导出变化

本版以原作者 5.0.1 正式标签 `33c81a9` 编译，保留 GUI 密码补丁。引擎标识为 `5.0.1+suoh.33c81a9`。

5.0.1 会依据联系人关联同一人的多个号码和邮箱，合并部分会话，文件名和附件目录可能与 4.3.0 不同。请在新目录导出，并按会话内容进行对照。

授权 GPL-3.0-or-later。同版本 `source.zip` 提供 Windows / Mac 界面、精确的原引擎源码、密码补丁、版本准备脚本与构建流程。原项目：https://github.com/ReagentX/imessage-exporter
