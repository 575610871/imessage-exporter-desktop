全新独立编写的 Windows 便携应用，不基于 0.1.x 的桌面封装代码。导出引擎从 ReagentX/imessage-exporter 固定源码重新编译。

0.2.2 统一命名：软件显示名称、Windows 文件属性及便携版文件夹为 **iMessage Exporter Desktop Suoh Edition**，主程序为 **iMessage Exporter Desktop Suoh Edition.exe**。内部命名空间改为 **IMessageExporterDesktopSuohEdition**，并同步 XAML、模型引用、构建与测试。苏芳主题、窗口居中、DPI 适配和导出功能保持不变。

下载 **imessage-exporter-desktop-suoh-edition-0.2.2-win-x64-portable.zip**，完整解压到新位置，打开 **iMessage Exporter Desktop Suoh Edition** 文件夹，双击其中同名的 **.exe**。无需安装或预装 .NET；请保留 engine 文件夹，不要覆盖旧版目录。

- 苏芳色 #992C49，新分步界面和独立任务日志页。
- 启动时按鼠标所在屏幕工作区限高限宽，再居中，保留原生标题栏。
- PerMonitorV2、随 Windows DPI 变化布局；窄屏收起预览，底部操作区固定。
- HTML/TXT、附件原文件复制、可选转换、加密备份密码、日期和会话筛选、检查与取消。
- 每次新建结果目录；密码不放入命令行或偏好文件。

自动验证包含 420 组屏幕几何组合及真实 Windows WPF 启动/布局检查；0.2.2 增加名称、元数据和命名空间检查，以及最终 ZIP 解压后的含空格路径启动/引擎导出验证。真实多屏动态缩放及用户 iPhone 备份仍需实机验收。未签名社区程序，下载后可核对 SHA256SUMS.txt。

原项目：https://github.com/ReagentX/imessage-exporter
原引擎提交：4d90fc8d20a745c0a8acc1e01c0631c4bab89cb4
新桌面源码：portable/。对应源码和构建说明见 Source.zip。
