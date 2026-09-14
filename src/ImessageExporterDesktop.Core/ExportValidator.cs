namespace IMessageExporterDesktop.Core;

public sealed record ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Failure(string error) => new(false, error);
}

public static class ExportValidator
{
    public static ValidationResult ValidateSource(ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.BackupPath))
        {
            return ValidationResult.Failure("请先选择 iPhone 备份目录。");
        }

        string backupPath;
        try
        {
            backupPath = Path.GetFullPath(settings.BackupPath.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ValidationResult.Failure($"备份路径无效：{exception.Message}");
        }

        if (!Directory.Exists(backupPath))
        {
            return ValidationResult.Failure("所选备份目录不存在。");
        }

        if (!File.Exists(Path.Combine(backupPath, "Manifest.plist")))
        {
            return ValidationResult.Failure(
                "这里不像 iPhone 备份根目录：没有找到 Manifest.plist。请选择设备编号对应的那一层文件夹。");
        }

        if (!File.Exists(Path.Combine(backupPath, "Manifest.db")) &&
            !File.Exists(Path.Combine(backupPath, "Manifest.mbdb")))
        {
            return ValidationResult.Failure(
                "备份不完整：没有找到 Manifest.db。请先用 Apple Devices 或 iTunes 完成一次本地备份。");
        }

        if (settings.StartDate is { } start &&
            settings.EndDate is { } end &&
            start.Date >= end.Date)
        {
            return ValidationResult.Failure("结束日期必须晚于开始日期；结束日期当天不包含在导出范围内。");
        }

        if (settings.UseCallerId && !string.IsNullOrWhiteSpace(settings.CustomName))
        {
            return ValidationResult.Failure("“使用账户号码/邮箱”和“自定义我的名字”不能同时启用。");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateExport(ExportSettings settings, string outputPath)
    {
        var sourceResult = ValidateSource(settings);
        if (!sourceResult.IsValid)
        {
            return sourceResult;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return ValidationResult.Failure("请先选择导出位置。");
        }

        string backupFullPath;
        string outputFullPath;
        try
        {
            backupFullPath = Path.GetFullPath(settings.BackupPath.Trim());
            outputFullPath = Path.GetFullPath(outputPath.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ValidationResult.Failure($"导出路径无效：{exception.Message}");
        }

        if (IsSameOrInside(outputFullPath, backupFullPath))
        {
            return ValidationResult.Failure("为保护原始备份，导出目录不能位于 iPhone 备份目录内部。");
        }

        if (File.Exists(outputFullPath))
        {
            return ValidationResult.Failure("导出位置指向了一个文件，请选择文件夹。");
        }

        if (Directory.Exists(outputFullPath))
        {
            var extension = settings.Format == ExportFormat.Html ? "*.html" : "*.txt";
            try
            {
                if (Directory.EnumerateFiles(outputFullPath, extension, SearchOption.TopDirectoryOnly).Any())
                {
                    return ValidationResult.Failure(
                        "该目录已经包含同格式的导出文件。请选择空目录，或启用“每次新建带日期的文件夹”。");
                }
            }
            catch (UnauthorizedAccessException)
            {
                return ValidationResult.Failure("没有权限读取所选导出目录。");
            }
        }

        return ValidationResult.Success();
    }

    private static bool IsSameOrInside(string candidate, string parent)
    {
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(candidate);
        var normalizedParent = Path.TrimEndingDirectorySeparator(parent);

        if (string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parentWithSeparator = normalizedParent + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
