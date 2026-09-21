using System.Text.Json;

namespace SteadyDesk;

internal sealed partial class SettingsForm
{
    private void ChooseBackground()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择新的底图",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var stagedPath = AppStorage.CreateTemporaryBackgroundPath();
        try
        {
            AppStorage.ImportBackground(dialog.FileName, stagedPath);
            AppStorage.DeleteIfExists(_pendingBackgroundPath);
            _pendingBackgroundPath = stagedPath;
            _draftController.UpdateOrThrow(config =>
                config.Wallpaper.BackgroundPath = stagedPath);
            _backgroundLabel.Text = GetBackgroundName(stagedPath);
            NotifyDraftChanged();
        }
        catch (Exception exception)
        {
            AppStorage.DeleteIfExists(stagedPath);
            MessageBox.Show(this, exception.Message, "更换底图失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreDefaultBackground()
    {
        AppStorage.DeleteIfExists(_pendingBackgroundPath);
        _pendingBackgroundPath = null;
        _draftController.UpdateOrThrow(config =>
            config.Wallpaper.BackgroundPath = AppStorage.DefaultBackgroundPath);
        _backgroundLabel.Text = "内置默认底图";
        NotifyDraftChanged();
    }

    private void ExportConfig()
    {
        try
        {
            var current = SynchronizeDraftOrThrow();
            using var dialog = new SaveFileDialog
            {
                Title = "导出稳序桌面配置",
                Filter = "稳序桌面配置|*.steady.json|JSON 文件|*.json",
                FileName = "稳序桌面配置.steady.json"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var exportConfig = current.Clone();
            exportConfig.Wallpaper.OriginalWallpaperPath = null;
            exportConfig.Wallpaper.OriginalWallpaperStyle = null;
            exportConfig.Wallpaper.OriginalTileWallpaper = null;

            var directory = Path.GetDirectoryName(dialog.FileName) ?? ".";
            var backgroundFileName = DefaultBackgroundMarker;
            var source = exportConfig.Wallpaper.BackgroundPath;
            if (!IsDefaultBackground(source) && File.Exists(source))
            {
                backgroundFileName = Path.GetFileNameWithoutExtension(dialog.FileName) + ".background.png";
                var destination = Path.Combine(directory, backgroundFileName);
                if (!Path.GetFullPath(source).Equals(
                        Path.GetFullPath(destination),
                        StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(source, destination, true);
                }

                exportConfig.Wallpaper.BackgroundPath = backgroundFileName;
            }
            else
            {
                exportConfig.Wallpaper.BackgroundPath = DefaultBackgroundMarker;
            }

            var package = new ConfigPackage
            {
                Config = exportConfig,
                BackgroundFileName = backgroundFileName
            };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(package, ConfigStore.Options));
            _statusLabel.Text = "配置和底图已导出。";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportConfig()
    {
        string? newlyStagedBackgroundPath = null;
        using var dialog = new OpenFileDialog
        {
            Title = "导入稳序桌面配置",
            Filter = "稳序桌面配置|*.steady.json;*.json|JSON 文件|*.json",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            AppConfig imported;
            string? packageBackground = null;
            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.TryGetProperty("Config", out _))
                {
                    var package = JsonSerializer.Deserialize<ConfigPackage>(json, ConfigStore.Options)
                        ?? throw new InvalidOperationException("配置包为空。");
                    imported = package.Config;
                    packageBackground = package.BackgroundFileName;
                }
                else
                {
                    imported = JsonSerializer.Deserialize<AppConfig>(json, ConfigStore.Options)
                        ?? throw new InvalidOperationException("配置文件为空或格式不正确。");
                }
            }

            imported.Normalize();
            var current = _draftController.Snapshot();
            var configDirectory = Path.GetDirectoryName(dialog.FileName) ?? ".";
            var importedBackgroundPath = imported.Wallpaper.BackgroundPath;
            string? sourceBackground = null;
            string? warning = null;

            if (!string.Equals(
                    importedBackgroundPath,
                    DefaultBackgroundMarker,
                    StringComparison.OrdinalIgnoreCase))
            {
                var candidate = string.IsNullOrWhiteSpace(packageBackground)
                    ? importedBackgroundPath
                    : packageBackground;
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    sourceBackground = Path.IsPathRooted(candidate)
                        ? candidate
                        : Path.Combine(configDirectory, candidate);
                }
            }

            var previousPendingBackgroundPath = _pendingBackgroundPath;
            if (!string.IsNullOrWhiteSpace(sourceBackground) && File.Exists(sourceBackground))
            {
                newlyStagedBackgroundPath = AppStorage.CreateTemporaryBackgroundPath();
                AppStorage.ImportBackground(sourceBackground, newlyStagedBackgroundPath);
                imported.Wallpaper.BackgroundPath = newlyStagedBackgroundPath;
            }
            else
            {
                if (!string.Equals(
                        importedBackgroundPath,
                        DefaultBackgroundMarker,
                        StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(importedBackgroundPath))
                {
                    warning = "配置中的底图文件未找到，已改用内置默认底图。";
                }
                imported.Wallpaper.BackgroundPath = AppStorage.DefaultBackgroundPath;
            }

            imported.Wallpaper.OriginalWallpaperPath = current.Wallpaper.OriginalWallpaperPath;
            imported.Wallpaper.OriginalWallpaperStyle = current.Wallpaper.OriginalWallpaperStyle;
            imported.Wallpaper.OriginalTileWallpaper = current.Wallpaper.OriginalTileWallpaper;
            imported.Normalize();
            ScheduleEngine.ValidateOrThrow(imported.Schedule);

            AppStorage.DeleteIfExists(previousPendingBackgroundPath);
            _pendingBackgroundPath = newlyStagedBackgroundPath;
            _draftController.Replace(imported);
            LoadDraftIntoControls(markAsChanged: true);
            _previewPane.ShowRefreshing();
            _previewCoordinator.RefreshNow();
            _statusLabel.Text = warning is null
                ? "配置和底图已导入，点击“保存并应用”后生效。"
                : warning + " 点击“保存并应用”后生效。";
        }
        catch (Exception exception)
        {
            if (newlyStagedBackgroundPath is not null
                && !string.Equals(
                    newlyStagedBackgroundPath,
                    _pendingBackgroundPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                AppStorage.DeleteIfExists(newlyStagedBackgroundPath);
            }

            MessageBox.Show(this, exception.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetAll()
    {
        if (MessageBox.Show(
                this,
                "确定恢复全部默认设置吗？",
                "恢复默认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        var current = _draftController.Snapshot();
        var reset = AppConfig.CreateDefault();
        reset.Behavior.AutoStart = _autoStart.Checked;
        reset.Wallpaper.OriginalWallpaperPath = current.Wallpaper.OriginalWallpaperPath;
        reset.Wallpaper.OriginalWallpaperStyle = current.Wallpaper.OriginalWallpaperStyle;
        reset.Wallpaper.OriginalTileWallpaper = current.Wallpaper.OriginalTileWallpaper;

        AppStorage.DeleteIfExists(_pendingBackgroundPath);
        _pendingBackgroundPath = null;
        _draftController.Replace(reset);
        LoadDraftIntoControls(markAsChanged: true);
        _previewPane.ShowRefreshing();
        _previewCoordinator.RefreshNow();
    }

    private static void OpenDataFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppStorage.RootDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "打开数据文件夹失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
