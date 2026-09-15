# iMessage Exporter Desktop

## 全新独立项目：Suoh Edition 便携版

新的 [iMessage Exporter Desktop Suoh Edition](portable/README.md) 位于 `portable/`，直接基于 ReagentX 原引擎源码，**不引用或编译下面旧版封装的代码**。新界面、任务管理、窗口定位、测试与打包均为独立实现。

苏芳色 `#992C49` · 启动按屏幕工作区居中 · PerMonitorV2 · 自包含免安装文件夹。

下载和使用请查看 [Releases](https://github.com/575610871/imessage-exporter-desktop/releases)；构建方法见 [独立项目说明](portable/BUILD.md)。

---

以下内容为保留的 **0.1.x 旧版** 文档，新版便携应用不使用其安装程序。

[![Build Windows](https://github.com/575610871/imessage-exporter-desktop/actions/workflows/build-windows.yml/badge.svg)](https://github.com/575610871/imessage-exporter-desktop/actions/workflows/build-windows.yml)

面向普通 Windows 用户的 iMessage 导出桌面应用。它为 [ReagentX/imessage-exporter](https://github.com/ReagentX/imessage-exporter) 提供中文优先的可视化界面、便携版和标准安装程序。

> 这是社区制作的非官方界面，与 Apple Inc. 或原上游作者没有隶属关系。

## 功能

- 自动寻找 Apple Devices 或 iTunes 创建的本地 iPhone 备份
- 图形化选择备份目录与导出目录
- 导出 HTML 或 TXT
- 附件不复制、原样复制、转换图片或完整转换
- 按日期、联系人、电话号码或邮箱筛选
- 支持加密 iPhone 备份；密码不保存、不显示在命令行中
- 显示实时日志，可随时取消
- 完成后直接打开导出文件夹
- 提供诊断模式
- 安装时可选择路径、创建桌面快捷方式并在完成后启动
- 支持 Windows 高 DPI 与多显示器缩放；窄窗口自动切换为单栏布局

## 使用

1. 用 Windows 的 **Apple Devices** 或 **iTunes** 将 iPhone 完整备份到电脑。
2. 下载并运行 `iMessage-Exporter-Desktop-Setup-x64.exe`。
3. 点击“自动查找备份”，或手动选择包含 `Manifest.plist` 的备份根目录。
4. 选择导出目录和格式，然后点击“开始导出”。

默认的“原样复制附件”不需要安装额外工具。“转换图片”需要 ImageMagick；“完整转换”还需要 FFmpeg。

## 隐私

应用和导出核心均在本机运行，不上传消息、联系人、附件或备份密码。加密备份密码只通过子进程环境传给本地导出核心，用后即丢弃，也不会写入偏好设置。

应用以只读方式使用 iPhone 备份，导出目录不得位于备份目录内部。

## 构建来源

构建工作流从上游 `develop` 分支的固定提交编译 Rust 核心：

`4d90fc8d20a745c0a8acc1e01c0631c4bab89cb4`

固定提交可让安装包可复现，也避免上游变化在未检查时进入成品。

## 开发构建

需要 Windows、.NET 8 SDK、Rust stable 和 Inno Setup 6。

```powershell
dotnet build src/ImessageExporterDesktop/ImessageExporterDesktop.csproj
```

完整安装包使用仓库中的 GitHub Actions 工作流构建。

## 显示缩放

应用声明为 Per-Monitor V2 DPI 感知。窗口会根据当前显示器的可用区域调整初始尺寸；拖动到不同缩放比例的显示器后会重新检查可用尺寸。窗口较窄时，导出选项会从双栏自动改为单栏，并保留纵向滚动。

## 许可证

本项目及其修改采用 **GPL-3.0-or-later**。导出核心版权归其原作者及贡献者所有，详情见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
