using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SteadyDesk;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _checkForUpdatesItem;
    private readonly AppUpdateClient _updateClient = new();
    private AppConfig _config;
    private DateTime _lastRefreshDay;
    private DateTime _lastRenderedMinute;
    private Size _lastScreenSize;
    private DateTime _nextRetryAt = DateTime.MinValue;
    private DateTime _lastErrorShownAt = DateTime.MinValue;
    private string? _lastErrorMessage;
    private bool _refreshing;

    public TrayApplicationContext(AppConfig config, bool firstRun, bool startedAutomatically, bool openSettings = false)
    {
        _config = config;

        if (firstRun)
        {
            if (string.IsNullOrWhiteSpace(_config.Wallpaper.OriginalWallpaperPath))
            {
                var snapshot = AppStorage.CaptureOriginalWallpaper();
                _config.Wallpaper.OriginalWallpaperPath = snapshot.Path;
                _config.Wallpaper.OriginalWallpaperStyle = snapshot.Style;
                _config.Wallpaper.OriginalTileWallpaper = snapshot.TileWallpaper;
            }

            _config.Behavior.AutoStart = true;
            AutoStartManager.RemoveLegacyEntries();
            AutoStartManager.SetEnabled(true);
            ConfigStore.Save(_config);
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开控制中心", null, (_, _) => OpenSettings());
        menu.Items.Add("立即刷新壁纸", null, (_, _) => RefreshWallpaper(true));
        _checkForUpdatesItem = new ToolStripMenuItem("检查更新")
        {
            Visible = AppUpdateClient.IsTrustConfigured
        };
        _checkForUpdatesItem.Click += async (_, _) => await CheckForUpdatesAsync(true);
        menu.Items.Add(_checkForUpdatesItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出并保留壁纸", null, (_, _) => ExitKeepingWallpaper());
        menu.Items.Add("停用并恢复原壁纸", null, (_, _) => DisableAndRestore());

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "稳序桌面",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        _timer = new System.Windows.Forms.Timer
        {
            Interval = Math.Clamp(_config.Behavior.RefreshIntervalSeconds * 1000, 5000, 300000)
        };
        _timer.Tick += (_, _) => RefreshIfNeeded();
        _timer.Start();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        RefreshWallpaper(false);
        if (_checkForUpdatesItem.Visible)
        {
            _ = CheckForUpdatesAsync(false);
        }

        if (openSettings)
        {
            var settingsTimer = new System.Windows.Forms.Timer { Interval = 250 };
            settingsTimer.Tick += (_, _) =>
            {
                settingsTimer.Stop();
                settingsTimer.Dispose();
                OpenSettings();
            };
            settingsTimer.Start();
        }

        if (!startedAutomatically)
        {
            ShowManualStartMessage(firstRun);
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_config, AutoStartManager.IsEnabled);
        var result = dialog.ShowDialog();
        if (result == DialogResult.Abort)
        {
            if (dialog.RestoreOriginalRequested)
            {
                DisableAndRestore();
            }
            else if (dialog.ExitRequested)
            {
                ExitKeepingWallpaper();
            }

            return;
        }

        if (result != DialogResult.OK)
        {
            return;
        }

        try
        {
            _config = dialog.EditedConfig;
            AutoStartManager.SetEnabled(dialog.AutoStartEnabled);
            _config.Behavior.AutoStart = dialog.AutoStartEnabled;
            ConfigStore.Save(_config);
            _timer.Interval = Math.Clamp(_config.Behavior.RefreshIntervalSeconds * 1000, 5000, 300000);
            RefreshWallpaper(true);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "设置保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckForUpdatesAsync(bool showCurrentVersionMessage)
    {
        if (!_checkForUpdatesItem.Visible || !_checkForUpdatesItem.Enabled)
        {
            return;
        }

        _checkForUpdatesItem.Enabled = false;
        _checkForUpdatesItem.Text = "正在检查更新…";
        try
        {
            var result = await _updateClient.CheckAsync();
            if (!result.IsUpdateAvailable)
            {
                if (showCurrentVersionMessage)
                {
                    MessageBox.Show(
                        "当前已是最新版本（" + result.CurrentVersion + "）。",
                        "稳序桌面更新",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            var sizeMiB = result.Manifest.PackageSizeBytes / 1024d / 1024d;
            var answer = MessageBox.Show(
                "发现新版本 " + result.AvailableVersion + "，安装包约 " + sizeMiB.ToString("F1") + " MiB。\n\n" +
                "现在下载并安装吗？安装时稳序桌面会自动关闭并重新打开。",
                "稳序桌面更新",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            await DownloadAndApplyUpdateAsync(result.Manifest);
        }
        catch (OperationCanceledException)
        {
            if (showCurrentVersionMessage)
            {
                MessageBox.Show("检查更新超时，请稍后重试。", "稳序桌面更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception exception)
        {
            if (showCurrentVersionMessage)
            {
                MessageBox.Show(
                    "检查或安装更新失败。程序仍会按当前版本运行。\n\n" + exception.Message,
                    "稳序桌面更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _checkForUpdatesItem.Enabled = true;
            _checkForUpdatesItem.Text = "检查更新";
        }
    }

    private async Task DownloadAndApplyUpdateAsync(UpdateManifest manifest)
    {
        var helperPath = Path.Combine(AppContext.BaseDirectory, "SteadyDesk.UpdateHelper.exe");
        var appPath = Environment.ProcessPath;
        if (!File.Exists(helperPath)
            || string.IsNullOrWhiteSpace(appPath)
            || !File.Exists(appPath)
            || !PathsEqual(AppContext.BaseDirectory, AppStorage.InstallDirectory))
        {
            throw new InvalidOperationException(
                "当前程序尚未安装到可更新目录，或缺少更新组件。请先把完整安装包解压到：\n\n" +
                AppStorage.InstallDirectory + "\n\n然后从该目录启动稳序桌面。");
        }

        _checkForUpdatesItem.Text = "正在下载更新…";
        _notifyIcon.ShowBalloonTip(2500, "正在下载更新", "下载完成并校验后才会安装。", ToolTipIcon.Info);
        var progress = new Progress<long>(bytes =>
        {
            if (_checkForUpdatesItem.Visible)
            {
                _checkForUpdatesItem.Text = "正在下载更新（" + (bytes / 1024 / 1024) + " MiB）…";
            }
        });
        var packagePath = await _updateClient.DownloadPackageAsync(manifest, progress);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = helperPath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--apply");
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(appPath);
        startInfo.ArgumentList.Add(manifest.Sha256);

        if (System.Diagnostics.Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException("无法启动更新组件。");
        }

        ExitKeepingWallpaper();
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshWallpaper(bool showConfirmation)
    {
        if (_refreshing)
        {
            return;
        }

        if (!showConfirmation && DateTime.Now < _nextRetryAt)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var bounds = GetDesktopBounds();
            var now = DateTime.Now;
            WallpaperRenderer.Render(_config, AppStorage.RenderedWallpaperPath, bounds.Width, bounds.Height, now);

            var style = Screen.AllScreens.Length > 1 ? "22" : "10";
            WallpaperManager.Apply(AppStorage.RenderedWallpaperPath, style, "0");

            _lastRefreshDay = now.Date;
            _lastRenderedMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            _lastScreenSize = bounds.Size;
            _nextRetryAt = DateTime.MinValue;
            _lastErrorMessage = null;

            ConfigStore.Save(_config);

            if (showConfirmation)
            {
                _notifyIcon.ShowBalloonTip(
                    2500,
                    "壁纸已刷新",
                    "已按 " + bounds.Width + "×" + bounds.Height + " 重新生成。",
                    ToolTipIcon.Info);
            }
        }
        catch (Exception exception)
        {
            var message = exception.Message;
            _nextRetryAt = DateTime.Now.AddSeconds(30);
            var shouldShow = showConfirmation
                || !string.Equals(_lastErrorMessage, message, StringComparison.Ordinal)
                || DateTime.Now - _lastErrorShownAt > TimeSpan.FromMinutes(5);

            _lastErrorMessage = message;
            if (shouldShow)
            {
                _lastErrorShownAt = DateTime.Now;
                MessageBox.Show(
                    message + "\n\n程序将在稍后自动重试。",
                    "壁纸刷新失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshIfNeeded()
    {
        var now = DateTime.Now;
        var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        var bounds = GetDesktopBounds();
        if (_lastRefreshDay != now.Date || _lastRenderedMinute != currentMinute || _lastScreenSize != bounds.Size)
        {
            RefreshWallpaper(false);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        RefreshWallpaper(false);
    }

    private static Rectangle GetDesktopBounds()
    {
        var bounds = SystemInformation.VirtualScreen;
        return bounds.Width > 0 && bounds.Height > 0
            ? bounds
            : new Rectangle(0, 0, 1920, 1080);
    }

    private static void ShowManualStartMessage(bool firstRun)
    {
        if (WallpaperEngineDetector.IsRunning())
        {
            MessageBox.Show(
                "Windows 静态壁纸已经设置成功，但检测到 Wallpaper Engine 正在运行。\n\n" +
                "Wallpaper Engine 会覆盖系统静态壁纸。请暂时暂停或退出后查看。\n\n" +
                "稳序桌面不受 Windows 是否激活影响。",
                "壁纸已设置，但可能被覆盖",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var firstRunText = firstRun ? "已保存原壁纸，并启用开机自动启动。\n\n" : string.Empty;
        var recoveryText = ConfigStore.LastRecoveryPath is null
            ? string.Empty
            : "检测到配置文件损坏，已备份到：\n" + ConfigStore.LastRecoveryPath + "\n\n";

        MessageBox.Show(
            recoveryText +
            firstRunText +
            "稳序桌面已经设置成功，程序会留在任务栏右下角运行。\n\n" +
            "右键图标即可打开控制中心。",
            "稳序桌面",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void DisableAndRestore()
    {
        var originalPath = _config.Wallpaper.OriginalWallpaperPath;
        if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
        {
            MessageBox.Show(
                "没有找到可恢复的原壁纸备份。当前程序仍会继续运行，避免误退出后无法恢复。",
                "无法恢复原壁纸",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            WallpaperManager.Apply(
                originalPath,
                _config.Wallpaper.OriginalWallpaperStyle ?? "10",
                _config.Wallpaper.OriginalTileWallpaper ?? "0");
            AutoStartManager.SetEnabled(false);
            _config.Behavior.AutoStart = false;
            ConfigStore.Save(_config);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "恢复原壁纸失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ExitKeepingWallpaper();
    }

    private void ExitKeepingWallpaper()
    {
        _timer.Stop();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _timer.Dispose();
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var wineBrush = new SolidBrush(Color.FromArgb(109, 31, 42));
        using var whiteBrush = new SolidBrush(Color.FromArgb(250, 246, 238));
        using var font = new Font("Microsoft YaHei", 29f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.FillEllipse(wineBrush, 2, 2, 60, 60);
        graphics.DrawString("稳", font, whiteBrush, new RectangleF(2, 0, 60, 60), center);
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
