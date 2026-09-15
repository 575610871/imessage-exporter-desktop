全新独立编写的 Windows 便携应用，不基于 0.1.x 的桌面封装代码。导出引擎从 ReagentX/imessage-exporter 固定源码重新编译。

下载 **Suoh-0.2.0-Windows-x64-Portable.zip**，完整解压，双击文件夹内 **Suoh.exe**。无需安装或预装 .NET；请保留 engine 文件夹。

- 苏芳色 #992C49，新分步界面和独立任务日志页。
- 启动时按鼠标所在屏幕工作区限高限宽，再居中，保留原生标题栏。
- PerMonitorV2、随 Windows DPI 变化布局；窄屏收起预览，底部操作区固定。
- HTML/TXT、附件原文件复制、可选转换、加密备份密码、日期和会话筛选、检查与取消。
- 每次新建结果目录；密码不放入命令行或偏好文件。

自动验证包含 420 组屏幕几何组合及真实 Windows WPF 启动/布局检查；真实多屏动态缩放及用户 iPhone 备份仍需实机验收。未签名社区程序，下载后可核对 SHA256SUMS.txt。

原项目：https://github.com/ReagentX/imessage-exporter
原引擎提交：4d90fc8d20a745c0a8acc1e01c0631c4bab89cb4
新桌面源码：portable/。对应源码和构建说明见 Source.zip。
