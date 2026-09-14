using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using IMessageExporterDesktop.Core;
using IMessageExporterDesktop.Services;
using Microsoft.Win32;

namespace IMessageExporterDesktop;

public partial class MainWindow : Window
{
    private const int MaximumLogLength = 500_000;
    private const uint MonitorDefaultToNearest = 2;

    private bool? _compactLayout;

    private readonly ObservableCollection<BackupInfo> _detectedBackups = new();
    private readonly ExporterRunner _runner = new();

    private readonly Choice<ExportFormat>[] _formatChoices =
    {
        new("HTML 网页（推荐）", ExportFormat.Html),
        new("TXT 纯文本", ExportFormat.Text)
    };

    private readonly Choice<AttachmentMode>[] _attachmentChoices =
    {
        new("原样复制（推荐）", AttachmentMode.Clone),
        new("不复制附件", AttachmentMode.Disabled),
        new("转换图片（需要 ImageMagick）", AttachmentMode.Basic),
        new("完整转换（需要 ImageMagick + FFmpeg）", AttachmentMode.Full)
    };

    private CancellationTokenSource? _operationCancellation;
    private bool _isBusy;
    private string? _lastOutputPath;

    public MainWindow()
    {
        InitializeComponent();
        DetectedBackupsList.ItemsSource = _detectedBackups;
        FormatComboBox.ItemsSource = _formatChoices;
        AttachmentModeComboBox.ItemsSource = _attachmentChoices;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FitWindowToCurrentWorkArea(preferDefaultSize: true);
        ApplyResponsiveLayout();

        LoadPreferences();
        UpdateDateControls();
        UpdateFormatControls();
        UpdateCallerIdControls();

        if (!BackupLocator.IsBackupRoot(BackupPathTextBox.Text))
        {
            await DetectBackupsAsync(showEmptyMessage: false);
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void Window_DpiChanged(object sender, DpiChangedEventArgs e)
    {
        FitWindowToCurrentWorkArea(preferDefaultSize: false);
        ApplyResponsiveLayout();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isBusy)
        {
            MessageBox.Show(
                this,
                "导出核心仍在运行。请先点击“取消”，等待状态恢复后再关闭窗口。",
                "任务仍在运行",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            e.Cancel = true;
            return;
        }

        TrySavePreferences();
    }

    private void BrowseBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 iPhone 备份根目录",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!BackupLocator.IsBackupRoot(dialog.FolderName))
        {
            MessageBox.Show(
                this,
                "没有在该目录找到完整的 iPhone 备份。\n\n请选择包含 Manifest.plist 和 Manifest.db 的设备编号文件夹。",
                "不是备份根目录",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        BackupPathTextBox.Text = dialog.FolderName;
        SetStatus("已选择备份", StatusKind.Ready);
    }

    private async void DetectBackupsButton_Click(object sender, RoutedEventArgs e)
    {
        await DetectBackupsAsync(showEmptyMessage: true);
    }

    private void DetectedBackupsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DetectedBackupsList.SelectedItem is BackupInfo backup)
        {
            BackupPathTextBox.Text = backup.Path;
            SetStatus("已选择检测到的备份", StatusKind.Ready);
        }
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择导出文件夹",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputBaseTextBox.Text = dialog.FolderName;
        }
    }

    private async void StartExportButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(diagnostics: false);
    }

    private async void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(diagnostics: true);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_operationCancellation is null)
        {
            return;
        }

        SetStatus("正在取消…", StatusKind.Warning);
        AppendLog("正在停止导出核心，请稍候…");
        _operationCancellation.Cancel();
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        OpenOutputFolder();
    }

    private void OpenUpstreamButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/ReagentX/imessage-exporter",
            UseShellExecute = true
        });
    }

    private void DateCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateDateControls();
    }

    private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFormatControls();
    }

    private void UseCallerIdCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateCallerIdControls();
    }

    private async Task DetectBackupsAsync(bool showEmptyMessage)
    {
        if (_isBusy)
        {
            return;
        }

        DetectBackupsButton.IsEnabled = false;
        SetStatus("正在查找备份…", StatusKind.Busy);

        try
        {
            var backups = await Task.Run(BackupLocator.FindWindowsBackups);
            _detectedBackups.Clear();

            foreach (var backup in backups)
            {
                _detectedBackups.Add(backup);
            }

            if (backups.Count == 0)
            {
                SetStatus("未自动找到备份", StatusKind.Warning);

                if (showEmptyMessage)
                {
                    MessageBox.Show(
                        this,
                        "没有在 Apple Devices 或 iTunes 的常见位置找到完整备份。\n\n请先完成一次本地备份，或点击“浏览”手动选择。",
                        "未找到备份",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;
            }

            var currentPath = BackupPathTextBox.Text.Trim();
            var selected = backups.FirstOrDefault(backup =>
                string.Equals(backup.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                ?? backups[0];

            DetectedBackupsList.SelectedItem = selected;
            DetectedBackupsList.ScrollIntoView(selected);
            BackupPathTextBox.Text = selected.Path;
            SetStatus($"找到 {backups.Count} 个备份", StatusKind.Ready);
        }
        catch (Exception exception)
        {
            SetStatus("查找备份失败", StatusKind.Error);
            AppendLog($"自动查找失败：{exception.Message}");
        }
        finally
        {
            DetectBackupsButton.IsEnabled = true;
        }
    }

    private async Task RunOperationAsync(bool diagnostics)
    {
        if (_isBusy)
        {
            return;
        }

        var settings = ReadSettings();
        string? outputPath = null;

        if (diagnostics)
        {
            var sourceValidation = ExportValidator.ValidateSource(settings);
            if (!sourceValidation.IsValid)
            {
                ShowValidationError(sourceValidation.Error);
                return;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(OutputBaseTextBox.Text))
            {
                ShowValidationError("请先选择导出位置。");
                return;
            }

            outputPath = NewSubfolderCheckBox.IsChecked == true
                ? CreateUniqueOutputPath(OutputBaseTextBox.Text.Trim())
                : OutputBaseTextBox.Text.Trim();

            var validation = ExportValidator.ValidateExport(settings, outputPath);
            if (!validation.IsValid)
            {
                ShowValidationError(validation.Error);
                return;
            }

            var missingTools = DependencyChecker.GetMissingTools(settings.AttachmentMode);
            if (missingTools.Count > 0)
            {
                MessageBox.Show(
                    this,
                    $"当前附件模式缺少：{string.Join("、", missingTools)}。\n\n请先安装这些工具并重新打开应用，或将附件处理改为“原样复制”。",
                    "缺少转换工具",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(outputPath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                ShowValidationError($"无法创建导出目录：{exception.Message}");
                return;
            }
        }

        TrySavePreferences();
        _operationCancellation = new CancellationTokenSource();
        SetBusy(true);
        LogTextBox.Clear();

        var actionName = diagnostics ? "诊断" : "导出";
        AppendLog($"{actionName}开始。所有内容仅在本机处理。");
        SetStatus(diagnostics ? "正在运行诊断…" : "正在导出…", StatusKind.Busy);

        if (!diagnostics)
        {
            _lastOutputPath = outputPath;
            OpenOutputButton.IsEnabled = false;
            AppendLog($"导出位置：{outputPath}");
        }

        var progress = new Progress<string>(AppendLog);

        try
        {
            var exitCode = await _runner.RunAsync(
                settings,
                outputPath,
                diagnostics,
                progress,
                _operationCancellation.Token);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"导出核心返回错误代码 {exitCode}。请查看上方日志。");
            }

            OperationProgressBar.IsIndeterminate = false;
            OperationProgressBar.Value = 100;
            SetStatus(diagnostics ? "诊断完成" : "导出完成", StatusKind.Success);
            AppendLog(diagnostics ? "诊断完成。" : "导出完成。");

            if (!diagnostics && _lastOutputPath is not null)
            {
                OpenOutputButton.IsEnabled = true;
                if (OpenWhenDoneCheckBox.IsChecked == true)
                {
                    OpenOutputFolder();
                }
            }
        }
        catch (OperationCanceledException)
        {
            OperationProgressBar.IsIndeterminate = false;
            OperationProgressBar.Value = 0;
            SetStatus("已取消", StatusKind.Warning);
            AppendLog("任务已取消。已经写出的文件不会自动删除。");
        }
        catch (Exception exception)
        {
            OperationProgressBar.IsIndeterminate = false;
            OperationProgressBar.Value = 0;
            SetStatus("处理失败", StatusKind.Error);
            AppendLog($"错误：{exception.Message}");
            MessageBox.Show(
                this,
                $"{actionName}没有完成。\n\n{exception.Message}\n\n详细信息保留在窗口日志中。",
                $"{actionName}失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            BackupPasswordBox.Clear();
            _operationCancellation.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private ExportSettings ReadSettings()
    {
        return new ExportSettings
        {
            BackupPath = BackupPathTextBox.Text.Trim(),
            Format = SelectedValue(FormatComboBox, ExportFormat.Html),
            AttachmentMode = SelectedValue(AttachmentModeComboBox, AttachmentMode.Clone),
            StartDate = StartDateCheckBox.IsChecked == true
                ? StartDatePicker.SelectedDate?.Date
                : null,
            EndDate = EndDateCheckBox.IsChecked == true
                ? EndDatePicker.SelectedDate?.Date
                : null,
            ConversationFilter = ConversationFilterTextBox.Text,
            CustomName = CustomNameTextBox.Text,
            UseCallerId = UseCallerIdCheckBox.IsChecked == true,
            DisableLazyLoading = NoLazyCheckBox.IsChecked == true,
            IgnoreDiskWarning = IgnoreDiskWarningCheckBox.IsChecked == true,
            UseMessageTimes = UseMessageTimesCheckBox.IsChecked == true,
            BackupPassword = BackupPasswordBox.Password
        };
    }

    private void LoadPreferences()
    {
        var preferences = PreferencesStore.Load();

        BackupPathTextBox.Text = preferences.LastBackupPath;
        OutputBaseTextBox.Text = preferences.OutputBasePath;
        FormatComboBox.SelectedItem =
            _formatChoices.First(choice => choice.Value == preferences.Format);
        AttachmentModeComboBox.SelectedItem =
            _attachmentChoices.First(choice => choice.Value == preferences.AttachmentMode);

        StartDateCheckBox.IsChecked = preferences.StartDateEnabled;
        EndDateCheckBox.IsChecked = preferences.EndDateEnabled;
        StartDatePicker.SelectedDate = preferences.StartDate ?? DateTime.Today.AddYears(-1);
        EndDatePicker.SelectedDate = preferences.EndDate ?? DateTime.Today.AddDays(1);
        ConversationFilterTextBox.Text = preferences.ConversationFilter;
        CustomNameTextBox.Text = preferences.CustomName;
        UseCallerIdCheckBox.IsChecked = preferences.UseCallerId;
        NoLazyCheckBox.IsChecked = preferences.DisableLazyLoading;
        IgnoreDiskWarningCheckBox.IsChecked = preferences.IgnoreDiskWarning;
        UseMessageTimesCheckBox.IsChecked = preferences.UseMessageTimes;
        NewSubfolderCheckBox.IsChecked = preferences.CreateTimestampedFolder;
    }

    private void TrySavePreferences()
    {
        try
        {
            PreferencesStore.Save(new UserPreferences
            {
                LastBackupPath = BackupPathTextBox.Text.Trim(),
                OutputBasePath = OutputBaseTextBox.Text.Trim(),
                Format = SelectedValue(FormatComboBox, ExportFormat.Html),
                AttachmentMode = SelectedValue(AttachmentModeComboBox, AttachmentMode.Clone),
                StartDate = StartDatePicker.SelectedDate,
                EndDate = EndDatePicker.SelectedDate,
                StartDateEnabled = StartDateCheckBox.IsChecked == true,
                EndDateEnabled = EndDateCheckBox.IsChecked == true,
                ConversationFilter = ConversationFilterTextBox.Text,
                CustomName = CustomNameTextBox.Text,
                UseCallerId = UseCallerIdCheckBox.IsChecked == true,
                DisableLazyLoading = NoLazyCheckBox.IsChecked == true,
                IgnoreDiskWarning = IgnoreDiskWarningCheckBox.IsChecked == true,
                UseMessageTimes = UseMessageTimesCheckBox.IsChecked == true,
                CreateTimestampedFolder = NewSubfolderCheckBox.IsChecked == true
            });
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            AppendLog($"未能保存界面偏好：{exception.Message}");
        }
    }

    private void UpdateDateControls()
    {
        StartDatePicker.IsEnabled = StartDateCheckBox.IsChecked == true;
        EndDatePicker.IsEnabled = EndDateCheckBox.IsChecked == true;
    }

    private void UpdateFormatControls()
    {
        if (NoLazyCheckBox is null)
        {
            return;
        }

        var isHtml = SelectedValue(FormatComboBox, ExportFormat.Html) == ExportFormat.Html;
        NoLazyCheckBox.IsEnabled = isHtml;
        if (!isHtml)
        {
            NoLazyCheckBox.IsChecked = false;
        }
    }

    private void UpdateCallerIdControls()
    {
        if (CustomNameTextBox is null)
        {
            return;
        }

        CustomNameTextBox.IsEnabled = UseCallerIdCheckBox.IsChecked != true;
        if (UseCallerIdCheckBox.IsChecked == true)
        {
            CustomNameTextBox.Text = string.Empty;
        }
    }

    private void FitWindowToCurrentWorkArea(bool preferDefaultSize)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>()
        };

        if (monitorHandle == IntPtr.Zero || !GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var workAreaWidth = (monitorInfo.Work.Right - monitorInfo.Work.Left) / dpi.DpiScaleX;
        var workAreaHeight = (monitorInfo.Work.Bottom - monitorInfo.Work.Top) / dpi.DpiScaleY;
        var layout = AdaptiveWindowSizer.Calculate(workAreaWidth, workAreaHeight);

        MinWidth = layout.MinimumWidth;
        MinHeight = layout.MinimumHeight;

        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var currentWidth = double.IsNaN(Width) || Width <= 0 ? ActualWidth : Width;
        var currentHeight = double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height;

        Width = preferDefaultSize
            ? layout.Width
            : Math.Max(layout.MinimumWidth, Math.Min(currentWidth, layout.Width));
        Height = preferDefaultSize
            ? layout.Height
            : Math.Max(layout.MinimumHeight, Math.Min(currentHeight, layout.Height));
    }

    private void ApplyResponsiveLayout()
    {
        if (ExportOptionsGrid is null || ActualWidth <= 0)
        {
            return;
        }

        var useCompactLayout = AdaptiveWindowSizer.ShouldUseCompact(ActualWidth);
        if (_compactLayout == useCompactLayout)
        {
            return;
        }

        _compactLayout = useCompactLayout;
        ExportOptionsGrid.ColumnDefinitions.Clear();
        ExportOptionsGrid.RowDefinitions.Clear();

        if (useCompactLayout)
        {
            ExportOptionsGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (var index = 0; index < 7; index++)
            {
                ExportOptionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            PlacePanel(FormatPanel, 0);
            PlacePanel(AttachmentPanel, 1);
            PlacePanel(StartDatePanel, 2);
            PlacePanel(EndDatePanel, 3);
            PlacePanel(ConversationPanel, 4);
            PlacePanel(CustomNamePanel, 5);
            PlacePanel(PasswordPanel, 6);

            PrivacyBadge.Visibility = Visibility.Collapsed;
            HeaderGrid.Margin = new Thickness(18, 16);
            MainContentPanel.Margin = new Thickness(16, 16, 16, 8);
            FooterGrid.Margin = new Thickness(16, 8);
            BackupDiscoveryPanel.Orientation = Orientation.Vertical;
            DetectBackupsButton.Margin = new Thickness(0, 0, 0, 6);
            BackupLocationHint.Margin = Thickness.Empty;
        }
        else
        {
            ExportOptionsGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ExportOptionsGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(24) });
            ExportOptionsGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (var index = 0; index < 4; index++)
            {
                ExportOptionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            PlacePanel(FormatPanel, 0, 0);
            PlacePanel(AttachmentPanel, 0, 2);
            PlacePanel(StartDatePanel, 1, 0);
            PlacePanel(EndDatePanel, 1, 2);
            PlacePanel(ConversationPanel, 2, 0);
            PlacePanel(CustomNamePanel, 2, 2);
            PlacePanel(PasswordPanel, 3, 0, 3);

            PrivacyBadge.Visibility = Visibility.Visible;
            HeaderGrid.Margin = new Thickness(28, 22);
            MainContentPanel.Margin = new Thickness(28, 24, 28, 10);
            FooterGrid.Margin = new Thickness(28, 10);
            BackupDiscoveryPanel.Orientation = Orientation.Horizontal;
            DetectBackupsButton.Margin = new Thickness(0, 0, 10, 0);
            BackupLocationHint.Margin = Thickness.Empty;
        }
    }

    private static void PlacePanel(
        FrameworkElement panel,
        int row,
        int column = 0,
        int columnSpan = 1)
    {
        Grid.SetRow(panel, row);
        Grid.SetColumn(panel, column);
        Grid.SetColumnSpan(panel, columnSpan);
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        StartExportButton.IsEnabled = !busy;
        DiagnosticsButton.IsEnabled = !busy;
        BrowseBackupButton.IsEnabled = !busy;
        BrowseOutputButton.IsEnabled = !busy;
        DetectBackupsButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;

        if (busy)
        {
            OperationProgressBar.Value = 0;
            OperationProgressBar.IsIndeterminate = true;
        }
    }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusTextBlock.Text = text;

        (StatusBorder.Background, StatusTextBlock.Foreground) = kind switch
        {
            StatusKind.Ready => (BrushFrom("#EEF4FF"), BrushFrom("#175CD3")),
            StatusKind.Busy => (BrushFrom("#EFF8FF"), BrushFrom("#026AA2")),
            StatusKind.Success => (BrushFrom("#ECFDF3"), BrushFrom("#027A48")),
            StatusKind.Warning => (BrushFrom("#FFFAEB"), BrushFrom("#B54708")),
            StatusKind.Error => (BrushFrom("#FEF3F2"), BrushFrom("#B42318")),
            _ => (BrushFrom("#F2F4F7"), BrushFrom("#475467"))
        };
    }

    private void AppendLog(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLog(message));
            return;
        }

        if (LogTextBox.Text.Length > MaximumLogLength)
        {
            LogTextBox.Text = LogTextBox.Text[^250_000..];
        }

        if (LogTextBox.Text.Length > 0 && !LogTextBox.Text.EndsWith(Environment.NewLine))
        {
            LogTextBox.AppendText(Environment.NewLine);
        }

        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }

    private void ShowValidationError(string? message)
    {
        SetStatus("请检查设置", StatusKind.Warning);
        MessageBox.Show(
            this,
            message ?? "设置无效。",
            "无法开始",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OpenOutputFolder()
    {
        if (_lastOutputPath is null || !Directory.Exists(_lastOutputPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastOutputPath,
            UseShellExecute = true
        });
    }

    private static string CreateUniqueOutputPath(string basePath)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        var candidate = Path.Combine(basePath, $"iMessage Export {timestamp}");
        var suffix = 2;

        while (Directory.Exists(candidate) || File.Exists(candidate))
        {
            candidate = Path.Combine(basePath, $"iMessage Export {timestamp} ({suffix})");
            suffix++;
        }

        return candidate;
    }

    private static T SelectedValue<T>(ComboBox comboBox, T fallback)
    {
        return comboBox.SelectedItem is Choice<T> choice ? choice.Value : fallback;
    }

    private static SolidColorBrush BrushFrom(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private sealed record Choice<T>(string Label, T Value);

    private enum StatusKind
    {
        Ready,
        Busy,
        Success,
        Warning,
        Error
    }
}
