**0.2.3 更新导出引擎，保留 0.2.2 的界面与操作方式。** 软件名称、苏芳配色、启动居中、原生标题栏和 Windows DPI 适配均保留。

下载 **imessage-exporter-desktop-suoh-edition-0.2.3-win-x64-portable.zip**，完整解压到新位置，打开 **iMessage Exporter Desktop Suoh Edition** 文件夹，双击其中同名的 **.exe**。无需安装或预装 .NET；请保留 engine 文件夹，不要覆盖旧版目录。

这次使用的原始软件材料已经核对：

- 上传的 EXE 与官方 **4.3.0** Windows GNU 文件的 SHA256 完全一致。
- 旧版 0.2.2 的引擎源码已经来自 4.3.0 标签；上传的 develop ZIP 比这个标签多六次提交。
- 新版按上传源码对应的上游 **2026-09-18 develop 快照 `0d444ab`** 重编译，纳入空 NSKeyedArchiver 数据、链接缺少预览信息时的文本回退修复，以及依赖更新。
- 保留本地加密备份密码传递补丁。引擎版本标识为 **`4.3.0+suoh.0d444ab`**，不是原作者发布的另一个正式版本，也不是直接替换成上传的 EXE。

构建验证包含两个上游 Rust 包的测试（涵盖新增回归测试）、420 组屏幕几何组合、真实 Windows WPF 启动/布局检查，以及最终 ZIP 解压后的启动、引擎版本/参数、HTML/TXT 示例导出、日期/时间戳选项与诊断验证。
真实多屏动态缩放及用户 iPhone 备份仍需实机验收。未签名社区程序，下载后可核对 SHA256SUMS.txt。

原项目：https://github.com/ReagentX/imessage-exporter
正式版基线：https://github.com/ReagentX/imessage-exporter/releases/tag/4.3.0
本版源码提交：`0d444ab3220f5940d4632c05da4628009c785a02`
对应源码、密码补丁、版本设置脚本和构建说明包含在同版本 source.zip 中。
