**0.3.1：修正跨平台源码包的换行格式，包含 0.3.0 的 5.0.1 引擎升级和原生 Mac 版。** Windows 保留 0.2.3 的界面、苏芳配色、启动居中、原生标题栏和 PerMonitorV2 DPI 布局。

0.3.0 的 Windows 和 Mac 主程序已验证通过；0.3.1 补充 Git 换行规则，保证 Windows 打包的 Mac 源码和构建脚本仍为 LF，并在发布前检查。两种主程序同步更新版本号，操作方式与引擎保持一致。

## 下载与打开

- **Windows x64**：下载 `imessage-exporter-desktop-suoh-edition-0.3.1-win-x64-portable.zip`，完整解压到新位置，双击文件夹内的 **iMessage Exporter Desktop Suoh Edition.exe**。保留旁边的 engine 文件夹，无需安装 .NET。
- **macOS 13+**：下载 `imessage-exporter-desktop-suoh-edition-0.3.1-macos-universal.zip`，解压后打开同名的 **.app**。同时支持 Apple 芯片与 Intel，无需另装运行时。Mac 版有本地 ad-hoc 签名，尚无 Apple Developer ID 签名或公证；首次打开可能需要在“系统设置 → 隐私与安全性”中选择“仍要打开”。详见包内 README。
- **对应源码**：`imessage-exporter-desktop-suoh-edition-0.3.1-source.zip`。`SHA256SUMS.txt` 包含三个压缩包的校验值。

## 引擎更新

按 **5.0.1 正式标签** `33c81a90beb151583983fb6ff000fc6f085ec5e8` 重编译，保留 GUI 密码环境传递补丁，不将密码放入命令行。版本标识为 `5.0.1+suoh.33c81a9`，用于说明这是附带 GUI 适配的构建。

上游更新包括联系人关联与会话合并、消息读取性能改进、投票计数修复、空预览链接显示修复、恢复消息的附件归档修复，以及数据库路径错误提示改进。**5.0 会改变部分会话分组、文件名和附件目录**；新版继续为每项导出建立独立目录，便于与旧版内容对照。

## Mac 界面

使用 SwiftUI / AppKit 实现相同的设置顺序、示例侧栏、任务日志和操作区。支持 Finder 备份查找、HTML / TXT、附件方式、加密备份、日期/会话筛选、命名/时间选项、取消、打开结果和可选的本机设置。每次启动居中，使用原生标题栏及 Retina 布局，窄窗口自动隐藏示例区。⌘0 居中，⌘L 查看日志。

## 验证

Windows 与 Mac 分别编译、运行上游测试，并使用公共 SQLite 样例验证引擎版本、GUI 参数、HTML/TXT 导出、日期和时间选项及诊断。最终 ZIP 解压后再次验证启动与引擎定位；Windows 保留 420 组屏幕几何组合及 WPF 布局检查，Mac 另验证原生窗口、SwiftUI 布局、Unicode/空格路径、符号链接保护、密码脱敏、任务取消和临时目录清理。

自动验证不替代用户真实加密 iPhone 备份或实际多显示器缩放切换验收。macOS 同时编译两个架构并验证通用二进制，运行测试使用构建机的原生架构。

原项目：https://github.com/ReagentX/imessage-exporter/releases/tag/5.0.1
