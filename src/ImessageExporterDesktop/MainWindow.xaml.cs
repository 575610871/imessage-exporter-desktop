using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IMessageExporterDesktop.Core;
using IMessageExporterDesktop.Services;
using Microsoft.Win32;

namespace IMessageExporterDesktop;

public partial class MainWindow : Window
{
    private const int MaximumLogLength = 500_000;

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
        LoadPreferences();
        UpdateDateControls();
        UpdateFormatControls();
        UpdateCallerIdControls();

        if (!BackupLocator.IsBackupRoot(BackupPathTextBox.Text))
        {
            await DetectBackupsAsync(showEmptyMessage: false);
        }
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
