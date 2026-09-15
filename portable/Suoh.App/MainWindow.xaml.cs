using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using IMessageExporterDesktopSuohEdition.Model;

namespace IMessageExporterDesktopSuohEdition;

public partial class MainWindow : Window
{
    internal DisplayPlacement Placement { get; }
    private readonly bool _testing;
    private readonly DispatcherTimer _logClock;
    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly Queue<string> _history = new();
    private readonly Stopwatch _elapsed = new();
    private CancellationTokenSource? _stop;
    private bool _active, _finding, _logView, _closeAfterStop;
    private string? _resultFolder;

    public MainWindow(bool testing = false)
    {
        _testing = testing;
        InitializeComponent();
        Placement = new(this);
        _logClock = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background,
            (_, _) => FlushLog(), Dispatcher);
        Closed += (_, _) => { _logClock.Stop(); _stop?.Dispose(); };
        if (!testing)
        {
            var saved = PortableOptions.Read();
            SourceInput.Text = saved.Source;
            OutputInput.Text = saved.OutputFolder;
            MediaChoice.SelectedIndex = Math.Clamp(saved.Media, 0, 3);
            TextChoice.IsChecked = saved.TextOnly;
            HtmlChoice.IsChecked = !saved.TextOnly;
            RememberOption.IsChecked = File.Exists(PortableOptions.FilePath);
        }
        if (string.IsNullOrWhiteSpace(OutputInput.Text))
            OutputInput.Text = testing ? @"C:\Exports" : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "iMessage Exports");
    }

    private void OnLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Aside is null) return;
        var narrow = ActualWidth < 940;
        AsideColumn.Width = narrow ? new GridLength(0) : new GridLength(300);
        Aside.Visibility = narrow || _logView ? Visibility.Collapsed : Visibility.Visible;
        Hero.Visibility = ActualHeight < 740 ? Visibility.Collapsed : Visibility.Visible;
        BrandRow.Visibility = ActualHeight < 400 ? Visibility.Collapsed : Visibility.Visible;
        JobStatus.Visibility = ActualHeight < 300 ? Visibility.Collapsed : Visibility.Visible;
        Footer.Padding = ActualHeight < 400 ? new Thickness(16, 8, 16, 8) : new Thickness(24, 12, 24, 12);
        PortableBadge.Visibility = ActualWidth < 620 ? Visibility.Collapsed : Visibility.Visible;
        FooterNote.Visibility = ActualWidth < 700 ? Visibility.Collapsed : Visibility.Visible;
        Header.Margin = ActualHeight < 500 ? new Thickness(16, 8, 16, 8) : new Thickness(24, 18, 24, 12);
        Workspace.Margin = narrow ? new Thickness(16, 0, 16, 0) : new Thickness(24, 0, 24, 0);
    }

    private void SwitchPage(bool logs)
    {
        _logView = logs;
        SettingsPage.Visibility = logs ? Visibility.Collapsed : Visibility.Visible;
        LogPage.Visibility = logs ? Visibility.Visible : Visibility.Collapsed;
        Aside.Visibility = logs || ActualWidth < 940 ? Visibility.Collapsed : Visibility.Visible;
        SettingsTab.Background = (Brush)FindResource(logs ? "Paper" : "Wash");
        LogsTab.Background = (Brush)FindResource(logs ? "Wash" : "Paper");
        SettingsTab.BorderBrush = SettingsTab.Background;
        LogsTab.BorderBrush = LogsTab.Background;
    }
    private void ShowSettings(object sender, RoutedEventArgs e) => SwitchPage(false);
    private void ShowLogs(object sender, RoutedEventArgs e) => SwitchPage(true);

    private string? PickFolder(string title, string initial)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (Directory.Exists(initial)) dialog.InitialDirectory = initial;
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }
    private void PickSource(object sender, RoutedEventArgs e)
    {
        if (PickFolder("选择设备备份根目录", SourceInput.Text) is { } folder) SourceInput.Text = folder;
    }
    private void PickOutput(object sender, RoutedEventArgs e)
    {
        if (PickFolder("选择导出文件夹的保存位置", OutputInput.Text) is { } folder) OutputInput.Text = folder;
    }

    private async void FindBackups(object sender, RoutedEventArgs e)
    {
        if (_finding || _active) return;
        _finding = true;
        FindButton.IsEnabled = false;
        ExportButton.IsEnabled = false;
        DiagnoseButton.IsEnabled = false;
        try
        {
            SetStatus("正在查找本机 Apple 备份目录…");
            var found = await Task.Run(BackupSearch.Scan);
            BackupChoices.ItemsSource = found;
            BackupChoices.Visibility = found.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (found.Count == 1) BackupChoices.SelectedIndex = 0;
            SetStatus(found.Count > 0 ? $"找到 {found.Count} 个备份，请确认所选目录。" : "没有找到本机备份。可以点击“浏览”选择其他磁盘上的备份。");
        }
        catch (Exception ex) { ShowProblem(ex.Message); }
        finally
        {
            _finding = false;
            FindButton.IsEnabled = !_active;
            ExportButton.IsEnabled = !_active;
            DiagnoseButton.IsEnabled = !_active;
        }
    }
    private void ChooseBackup(object sender, SelectionChangedEventArgs e)
    {
        if (BackupChoices.SelectedItem is string path) SourceInput.Text = path;
    }

    private void FormatChanged(object sender, RoutedEventArgs e)
    {
        if (EagerOption is not null) EagerOption.IsEnabled = HtmlChoice.IsChecked == true;
    }
    private void MediaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MediaHint is null) return;
        MediaHint.Text = MediaChoice.SelectedIndex switch
        {
            1 => "仅导出聊天文字和附件引用，不复制照片、视频等文件。",
            2 => "转换图片以方便浏览；需要可用的 ImageMagick，其他媒体保留原文件。",
            3 => "转换图片、音频和视频；需要 ImageMagick 与 FFmpeg，耗时及空间占用较高。",
            _ => "复制原始照片、视频等附件，不需要额外工具。HEIC 等格式可能需要对应查看器。"
        };
    }
    private void CallerIdChanged(object sender, RoutedEventArgs e)
    {
        if (NameInput is not null) NameInput.IsEnabled = CallerIdOption.IsChecked != true;
    }

    private ExportPlan MakePlan(bool diagnostics)
    {
        DateOnly? ReadDate(DatePicker picker)
        {
            if (!string.IsNullOrWhiteSpace(picker.Text) && picker.SelectedDate is null)
                throw new ArgumentException("请输入有效的日期，或清空日期框。");
            return picker.SelectedDate is { } date ? DateOnly.FromDateTime(date) : null;
        }
        if (!diagnostics && string.IsNullOrWhiteSpace(OutputInput.Text)) throw new ArgumentException("请选择导出位置。");
        if (string.IsNullOrWhiteSpace(SourceInput.Text)) throw new ArgumentException("请选择 iPhone 本地备份根目录。");
        return new ExportPlan(Path.GetFullPath(SourceInput.Text.Trim()), diagnostics ? "" : ExportPlan.NewDestination(OutputInput.Text),
            HtmlChoice.IsChecked == true ? TranscriptKind.Html : TranscriptKind.Txt,
            (MediaHandling)Math.Clamp(MediaChoice.SelectedIndex, 0, 3),
            diagnostics ? null : ReadDate(FromInput), diagnostics ? null : ReadDate(BeforeInput),
            ConversationInput.Text, NameInput.Text, CallerIdOption.IsChecked == true,
            EagerOption.IsChecked == true, TimestampOption.IsChecked == true);
    }

    private async void Export(object sender, RoutedEventArgs e) => await RunJob(false);
    private async void Diagnose(object sender, RoutedEventArgs e) => await RunJob(true);

    private async Task RunJob(bool diagnostics)
    {
        if (_active || _finding || _testing) return;
        ExportPlan plan;
        try
        {
            plan = MakePlan(diagnostics);
            if (plan.Validate(diagnostics) is { } error) { ShowProblem(error); return; }
            if (!File.Exists(EngineProcess.Binary)) { ShowProblem("找不到导出引擎。请完整解压便携包，并保留 engine 文件夹。"); return; }
            if (!diagnostics && EngineProcess.MissingConverters(plan.Media) is { Count: > 0 } missing)
            {
                ShowProblem("缺少转换工具：" + string.Join("、", missing) + "。可改选“保留原文件”，或把工具及其依赖放入 tools 文件夹 / PATH。");
                return;
            }
            if (!diagnostics) Directory.CreateDirectory(plan.Destination);
        }
        catch (Exception e) { ShowProblem(e.Message); return; }

        _active = true;
        _resultFolder = null;
        _stop = new CancellationTokenSource();
        _history.Clear();
        while (_incoming.TryDequeue(out _)) { }
        LogOutput.Clear();
        OpenResultButton.IsEnabled = false;
        Form.IsEnabled = false;
        ExportButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        Activity.IsIndeterminate = true;
        _elapsed.Restart();
        SetStatus(diagnostics ? "正在检查备份…" : "正在导出…具体进展请查看任务日志。");
        SwitchPage(true);
        QueueLog(diagnostics ? "开始检查（仅本地读取）" : "开始导出（每次使用独立输出目录）");
        QueueLog("备份：" + plan.Source);
        if (!diagnostics) QueueLog("输出：" + plan.Destination);
        StorePreferences();
        var password = PasswordInput.Password;
        PasswordInput.Clear();
        var completed = false;
        try
        {
            var exitCode = await new EngineProcess().Execute(plan, diagnostics, password, QueueLog, _stop.Token);
            if (exitCode == 0)
            {
                completed = true;
                SetStatus(diagnostics ? "检查完成，请查看日志中的检测结果。" : "导出完成 · 可打开结果文件夹查看。");
                QueueLog("引擎正常完成。请留意上方是否有附件缺失等警告。");
            }
            else
            {
                SetStatus($"任务未完成（退出码 {exitCode}）。请查看日志；加密备份请确认密码。", true);
                QueueLog("如果提示 SUOH_BACKUP_PASSWORD，请回到导出设置填写本地备份密码后重试。");
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消。部分导出文件会保留，不会自动删除。", true);
            QueueLog("用户取消了任务。部分文件不代表完整导出。");
        }
        catch (Exception e)
        {
            SetStatus("任务出错，请查看日志。", true);
            QueueLog("错误：" + (password.Length == 0 ? e.Message : e.Message.Replace(password, "[密码已隐藏]", StringComparison.Ordinal)));
        }
        finally
        {
            password = ""; // Managed strings cannot be guaranteed to be immediately erased from memory.
            _elapsed.Stop();
            _active = false;
            _stop.Dispose();
            _stop = null;
            Form.IsEnabled = true;
            FindButton.IsEnabled = !_finding;
            ExportButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            Activity.IsIndeterminate = false;
            Activity.Value = completed ? 100 : 0;
            if (!diagnostics && Directory.Exists(plan.Destination))
            {
                _resultFolder = plan.Destination;
                OpenResultButton.IsEnabled = true;
            }
            FlushLog();
        }
        if (_closeAfterStop) Close();
        else if (completed && !diagnostics && OpenAfterOption.IsChecked == true) OpenResultFolder();
    }

    private void StorePreferences()
    {
        if (_testing || RememberOption.IsChecked != true) return;
        try { new PortableOptions(SourceInput.Text, OutputInput.Text, MediaChoice.SelectedIndex, TextChoice.IsChecked == true).Write(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { QueueLog("当前便携目录无法保存偏好设置，不影响本次导出：" + e.Message); }
    }
    private void ForgetPreferences(object sender, RoutedEventArgs e)
    {
        if (_testing || !IsLoaded) return;
        try { PortableOptions.Forget(); }
        catch (Exception ex) { ShowProblem("未能移除已保存的目录设置：" + ex.Message); }
    }
    private void CancelJob(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        SetStatus("正在停止导出引擎…");
        _stop?.Cancel();
    }

    private void QueueLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _incoming.Enqueue($"[{DateTime.Now:HH:mm:ss}] {line}");
        while (_incoming.Count > 3000) _incoming.TryDequeue(out _);
    }
    private void FlushLog()
    {
        var changed = false;
        while (_incoming.TryDequeue(out var line))
        {
            _history.Enqueue(line);
            if (_history.Count > 3000) _history.Dequeue();
            changed = true;
        }
        if (changed)
        {
            LogOutput.Text = string.Join(Environment.NewLine, _history);
            LogOutput.ScrollToEnd();
        }
        if (_elapsed.Elapsed > TimeSpan.Zero) ElapsedText.Text = _elapsed.Elapsed.ToString(@"hh\:mm\:ss");
    }
    private void SaveLog(object sender, RoutedEventArgs e)
    {
        FlushLog();
        var save = new SaveFileDialog { Title = "保存日志（分享前请检查隐私信息）", FileName = "imessage-exporter-desktop-suoh-edition-log.txt", Filter = "文本文件|*.txt" };
        if (save.ShowDialog(this) != true) return;
        try { File.WriteAllText(save.FileName, LogOutput.Text, new UTF8Encoding(true)); }
        catch (Exception ex) { ShowProblem("无法保存日志：" + ex.Message); }
    }

    private void OpenResult(object sender, RoutedEventArgs e) => OpenResultFolder();
    private void OpenResultFolder()
    {
        if (_resultFolder is null || !Directory.Exists(_resultFolder)) { ShowProblem("结果目录不存在或已经被移动。"); return; }
        try { Process.Start(new ProcessStartInfo(_resultFolder) { UseShellExecute = true }); }
        catch (Exception ex) { ShowProblem("无法打开结果目录：" + ex.Message); }
    }
    private void SetStatus(string message, bool error = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(error ? "Suoh" : "Ink");
    }
    private void ShowProblem(string message)
    {
        SetStatus(message, true);
        MessageBox.Show(this, message, App.DisplayName + " · 请检查", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void ShowGuide(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        $"1. 用 Apple Devices 或 iTunes 将 iPhone 备份到这台电脑。\n2. 选择包含 Manifest.plist 和 Manifest.db 的设备目录。\n3. 选择输出位置、格式及附件方式。加密备份请填写备份密码。\n4. 点击开始导出；HTML 文件可用浏览器打开。\n\n不直接连接手机、不下载 iCloud 数据，也不会上传聊天记录。云端尚未下载到备份的内容无法凭空恢复。\n\n窗口：每次启动在鼠标所在屏幕的可用区域居中。Ctrl+0 可重新居中，Ctrl+L 查看日志。F1 打开指南。\n\n便携：完整解压后运行 {App.ExecutableName}，无需安装。可选的偏好设置位于程序旁 data 文件夹；密码不会保存。引擎可能暂存解密数据，正常结束/取消后尝试清理，崩溃时可能留下临时文件。\n\n{App.DisplayName} {App.DisplayVersion}\n原引擎 ReagentX/imessage-exporter · GPL-3.0-or-later。",
        App.DisplayName + " · 使用指南", MessageBoxButton.OK, MessageBoxImage.Information);

    private void OnShortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1) { ShowGuide(this, e); e.Handled = true; return; }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key is Key.D0 or Key.NumPad0) { Placement.Recenter(); e.Handled = true; }
        if (e.Key == Key.L) { SwitchPage(true); e.Handled = true; }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_active) { StorePreferences(); return; }
        e.Cancel = true;
        if (MessageBox.Show(this, "任务仍在运行。要停止导出并关闭窗口吗？部分导出文件会保留。", App.DisplayName, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _closeAfterStop = true;
        _stop?.Cancel();
    }
}
