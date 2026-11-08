import SwiftUI

enum Palette {
    static let suoh = Color(red: 153 / 255, green: 44 / 255, blue: 73 / 255)
    static let paper = Color(red: 250 / 255, green: 247 / 255, blue: 247 / 255)
    static let wash = Color(red: 246 / 255, green: 233 / 255, blue: 238 / 255)
    static let ink = Color(red: 31 / 255, green: 38 / 255, blue: 49 / 255)
    static let line = Color(red: 234 / 255, green: 215 / 255, blue: 221 / 255)
    static let muted = Color(red: 115 / 255, green: 103 / 255, blue: 110 / 255)
}

struct Card<Content: View>: View {
    let content: Content
    init(@ViewBuilder content: () -> Content) { self.content = content() }
    var body: some View {
        VStack(alignment: .leading, spacing: 12) { content }
            .frame(maxWidth: .infinity, alignment: .leading).padding(20)
            .background(.white).clipShape(RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).stroke(Palette.line, lineWidth: 1))
    }
}

struct MainView: View {
    @ObservedObject var model: AppModel
    @State private var advanced = false
    var body: some View {
        GeometryReader { geometry in
            VStack(spacing: 0) {
                header(width: geometry.size.width, height: geometry.size.height)
                HStack(alignment: .top, spacing: 16) {
                    if model.logsVisible { logPage }
                    else {
                        ScrollView {
                            VStack(spacing: 14) { form }
                                .disabled(model.busy || model.finding).padding(1).padding(.trailing, 5).padding(.bottom, 16)
                        }.frame(maxWidth: .infinity, maxHeight: .infinity)
                        if geometry.size.width >= 940 { aside.frame(width: 284) }
                    }
                }.padding(.horizontal, geometry.size.width < 940 ? 16 : 24)
                footer(width: geometry.size.width, height: geometry.size.height)
            }
            .background(Palette.paper).foregroundColor(Palette.ink)
        }
        .font(.system(size: 14)).tint(Palette.suoh)
        .textFieldStyle(.roundedBorder).controlSize(.large)
        .environment(\.colorScheme, .light)
        .onChange(of: model.remember) { value in if !value { model.forgetPreferences() } }
    }

    @ViewBuilder private func header(width: CGFloat, height: CGFloat) -> some View {
        VStack(alignment: .leading, spacing: height < 400 ? 6 : 16) {
            if height >= 400 {
                HStack(spacing: 12) {
                    Image(systemName: "bubble.left.fill").font(.system(size: 23, weight: .bold)).foregroundColor(.white)
                        .frame(width: 40, height: 40).background(Palette.suoh).clipShape(RoundedRectangle(cornerRadius: 12))
                    VStack(alignment: .leading, spacing: 3) {
                        Text("iMessage Exporter Desktop").font(.system(size: 19, weight: .semibold)).lineLimit(1)
                        Text("Suoh Edition  /  YOUR CONVERSATIONS, KEPT.").font(.system(size: 9)).foregroundColor(Palette.muted).lineLimit(1)
                    }.frame(maxWidth: .infinity, alignment: .leading)
                    Button("使用指南", action: model.guide).buttonStyle(.bordered)
                }
            }
            if height >= 740 {
                VStack(alignment: .leading, spacing: 8) {
                    Text("把对话，好好收藏。").font(.system(size: 28, weight: .semibold))
                    Text("从 iPhone 本地备份，导出可以随时翻阅的聊天记录。").foregroundColor(Palette.muted)
                }.padding(.vertical, 6)
            }
            HStack(spacing: 8) {
                tab("导出设置", logs: false)
                tab("任务日志", logs: true)
                if width >= 620 { Text("免安装 · 本地处理").font(.system(size: 12)).foregroundColor(Palette.muted).padding(.leading, 8) }
                Spacer(minLength: 0)
            }
        }.padding(.horizontal, height < 400 ? 16 : 24).padding(.top, height < 400 ? 8 : 18).padding(.bottom, 12)
    }
    private func tab(_ title: String, logs: Bool) -> some View {
        Button { model.logsVisible = logs } label: {
            Text(title).foregroundColor(Palette.suoh).padding(.horizontal, 17).padding(.vertical, 10)
                .background(model.logsVisible == logs ? Palette.wash : Color.clear)
                .clipShape(RoundedRectangle(cornerRadius: 8))
        }.buttonStyle(.plain)
    }
    private func hint(_ text: String) -> some View {
        Text(text).font(.system(size: 12)).foregroundColor(Palette.muted).fixedSize(horizontal: false, vertical: true)
    }
    private func heading(_ text: String) -> some View { Text(text).font(.system(size: 18, weight: .semibold)) }

    @ViewBuilder private var form: some View {
        Card {
            heading("01   选择备份")
            HStack(spacing: 8) {
                TextField("iPhone 备份根目录", text: $model.source).accessibilityLabel("iPhone 备份根目录")
                Button("浏览…", action: model.pickSource).buttonStyle(.bordered)
            }
            hint("支持 Finder / iTunes 的本地备份；不能直接读取连接的手机。")
            HStack {
                Button("查找本机备份", action: model.findBackups)
                Button("检查备份") { model.run(diagnostics: true) }
            }.buttonStyle(.bordered)
            if !model.backups.isEmpty {
                Picker("找到的备份", selection: $model.source) {
                    if !model.backups.contains(model.source) { Text("请选择").tag(model.source) }
                    ForEach(model.backups, id: \.self) { Text($0).tag($0) }
                }.labelsHidden()
            }
        }
        Card {
            heading("02   保存到哪里")
            HStack(spacing: 8) {
                TextField("导出位置", text: $model.output).accessibilityLabel("导出位置")
                Button("浏览…", action: model.pickOutput).buttonStyle(.bordered)
            }
            hint("每次自动新建独立文件夹，已有导出不会被覆盖。")
        }
        Card {
            heading("03   导出方式")
            HStack(spacing: 12) {
                formatChoice("HTML 网页", detail: "在浏览器里翻阅", value: "html")
                formatChoice("TXT 文本", detail: "便于检索和归档", value: "txt")
            }
            Text("照片与附件").fontWeight(.semibold).padding(.top, 3)
            Picker("附件处理方式", selection: $model.media) {
                Text("保留原文件 · 推荐").tag("clone")
                Text("不复制附件").tag("disabled")
                Text("转换图片 · 使用 macOS 系统工具").tag("basic")
                Text("转换全部媒体 · 另需 FFmpeg").tag("full")
            }.labelsHidden().frame(maxWidth: .infinity, alignment: .leading)
            hint(mediaHint)
        }
        Card {
            Text("备份密码").fontWeight(.semibold)
            SecureField("加密备份密码，可选", text: $model.password).accessibilityLabel("加密备份密码，可选")
            hint("仅加密备份需要。不是 Apple ID 密码；只传给本地引擎，不保存到设置或命令行。")
        }
        Card {
            DisclosureGroup("更多选项 · 日期、会话与显示", isExpanded: $advanced) {
                VStack(alignment: .leading, spacing: 12) {
                    Text("日期范围（留空表示不限）").fontWeight(.semibold)
                    HStack(alignment: .top, spacing: 12) {
                        VStack(alignment: .leading, spacing: 5) {
                            hint("从这一天开始")
                            TextField("YYYY-MM-DD", text: $model.from).accessibilityLabel("开始日期")
                        }
                        VStack(alignment: .leading, spacing: 5) {
                            hint("到这一天之前（不含当天）")
                            TextField("YYYY-MM-DD", text: $model.before).accessibilityLabel("结束日期，不含当天")
                        }
                    }
                    Text("指定会话（可选）").fontWeight(.semibold)
                    TextField("手机号、邮箱或群聊名称", text: $model.conversation)
                    hint("多个条件用英文逗号分隔。留空导出全部；5.0.1 会关联同一联系人的不同地址。")
                    Text("记录中“我”的名字（可选）").fontWeight(.semibold)
                    TextField("自定义名字", text: $model.myName).disabled(model.callerID)
                    Toggle("用账户号码 / 邮箱代替自定义名字", isOn: $model.callerID)
                    Toggle("HTML 中立即加载图片", isOn: $model.eager).disabled(model.format != "html")
                    Toggle("使用消息时间作为导出文件时间", isOn: $model.timestamps)
                }.toggleStyle(.checkbox).padding(.top, 12)
            }
        }
        VStack(alignment: .leading, spacing: 10) {
            Toggle("记住目录和导出格式", isOn: $model.remember)
            Toggle("完成后打开文件夹", isOn: $model.openAfter)
        }.toggleStyle(.checkbox).frame(maxWidth: .infinity, alignment: .leading)
    }
    private func formatChoice(_ title: String, detail: String, value: String) -> some View {
        Button { model.format = value } label: {
            VStack(alignment: .leading, spacing: 6) {
                Text(title).fontWeight(.semibold)
                hint(detail)
            }.frame(maxWidth: .infinity, alignment: .leading).padding(14)
                .background(model.format == value ? Palette.wash : Palette.paper)
                .clipShape(RoundedRectangle(cornerRadius: 9))
                .overlay(RoundedRectangle(cornerRadius: 9).stroke(model.format == value ? Palette.suoh : Palette.line))
        }.buttonStyle(.plain).accessibilityAddTraits(model.format == value ? .isSelected : [])
    }
    private var mediaHint: String {
        switch model.media {
        case "disabled": return "仅导出文字和附件引用，不复制照片、视频等文件。"
        case "basic": return "使用系统 sips 转换图片，其他媒体保留原文件。"
        case "full": return "图片和音频使用 macOS 系统工具；视频转换需要可用的 FFmpeg。"
        default: return "复制原始照片、视频等附件，不需要额外工具。HEIC 等格式需要对应查看器。"
        }
    }
    private var logPage: some View {
        Card {
            HStack { heading("任务日志"); Spacer(); Button("保存日志…", action: model.saveLog).buttonStyle(.bordered) }
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: 0) {
                        Text(model.logs).font(.system(size: 12, design: .monospaced)).textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .leading)
                        Color.clear.frame(height: 1).id("bottom")
                    }.padding(8)
                }.background(Palette.paper).clipShape(RoundedRectangle(cornerRadius: 6))
                    .onChange(of: model.logs) { _ in proxy.scrollTo("bottom", anchor: .bottom) }
            }
            hint("日志可能包含目录和联系人信息。分享前请检查；本程序不会自动上传日志。")
        }.frame(maxWidth: .infinity, maxHeight: .infinity).padding(.bottom, 14)
    }
    private var aside: some View {
        ScrollView {
            VStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 18) {
                    Text("留住那些，\n舍不得删的日常。").font(.system(size: 24, weight: .semibold)).lineSpacing(5)
                    Text("每一句问候，都值得被好好保存。").font(.system(size: 12)).opacity(0.85)
                    Text("今天也有好好吃饭吗？").font(.system(size: 13)).padding(14)
                        .frame(maxWidth: .infinity, alignment: .leading).background(.white.opacity(0.16)).clipShape(RoundedRectangle(cornerRadius: 12)).padding(.trailing, 16)
                    Text("有呀。等下给你打电话。").font(.system(size: 13)).foregroundColor(Palette.suoh).padding(14)
                        .frame(maxWidth: .infinity, alignment: .leading).background(Palette.paper).clipShape(RoundedRectangle(cornerRadius: 12)).padding(.leading, 16)
                    Text("示例对话 · 非真实聊天数据").font(.system(size: 10)).opacity(0.85).frame(maxWidth: .infinity)
                }.foregroundColor(.white).padding(24).frame(maxWidth: .infinity, alignment: .leading)
                    .background(Palette.suoh).clipShape(RoundedRectangle(cornerRadius: 14))
                Card {
                    Text("开始之前").font(.system(size: 16, weight: .semibold))
                    hint("1. 用 Finder 完成 iPhone 本地备份。\n\n2. 选择设备对应的备份目录。\n\n3. 留出足够空间，开始导出。")
                    Divider().padding(.vertical, 6)
                    hint("引擎来自 ReagentX / imessage-exporter；Suoh Edition 提供 Windows 与 Mac 图形界面。")
                }
            }.padding(.bottom, 16)
        }
    }
    private func footer(width: CGFloat, height: CGFloat) -> some View {
        VStack(spacing: height < 300 ? 4 : 10) {
            if height >= 300 {
                HStack {
                    Text(model.status).lineLimit(1).help(model.status)
                    Spacer(minLength: 8)
                    Text(model.elapsed).font(.system(size: 12)).foregroundColor(Palette.muted)
                }
            }
            HStack(spacing: 8) {
                VStack(alignment: .leading, spacing: 5) {
                    if model.busy { ProgressView().progressViewStyle(.linear).frame(height: 3) }
                    if width >= 700 { hint("Suoh Edition \(Product.version) · Mac 版") }
                }.frame(maxWidth: .infinity, alignment: .leading)
                Button("打开结果", action: model.openResult).disabled(model.result == nil)
                Button("取消", action: model.stop).disabled(!model.busy)
                Button("开始导出  →") { model.run() }.buttonStyle(.borderedProminent)
                    .disabled(model.busy || model.finding).keyboardShortcut(.return, modifiers: .command)
            }.buttonStyle(.bordered)
        }.padding(.horizontal, height < 400 ? 16 : 24).padding(.vertical, height < 400 ? 8 : 12)
            .background(.white).overlay(alignment: .top) { Rectangle().fill(Palette.line).frame(height: 1) }
    }
}
