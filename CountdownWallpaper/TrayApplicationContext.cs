using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace CountdownWallpaper;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly AppSettings _settings;
    private DateTime _lastRefreshDay;
    private DateTime _lastRenderedMinute;
    private Size _lastScreenSize;
    private bool _refreshing;

    public TrayApplicationContext(bool startedAutomatically, bool openSettings = false)
    {
        var isFirstRun = !SettingsStore.Exists;
        _settings = SettingsStore.Load();
        if (isFirstRun)
        {
            _settings.OriginalWallpaperPath = AppStorage.CaptureOriginalWallpaper();
            AutoStartManager.SetEnabled(true);
            SettingsStore.Save(_settings);
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("启动设置", null, (_, _) => OpenSettings());

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "郑老师中考倒计时",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        // 每 5 秒检查一次，减少跨分钟或跨天时的显示延迟；只有时间真正变化时才重绘壁纸。
        _timer = new System.Windows.Forms.Timer { Interval = 5_000 };
        _timer.Tick += (_, _) => RefreshIfNeeded();
        _timer.Start();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        RefreshWallpaper(showConfirmation: false);
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
            ShowManualStartMessage(isFirstRun);
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings, AutoStartManager.IsEnabled);
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
            AutoStartManager.SetEnabled(dialog.AutoStartEnabled);
            _settings.BackgroundPath = dialog.SelectedBackgroundPath;
            _settings.ScheduleXPercent = dialog.SelectedXPercent;
            SettingsStore.Save(_settings);
            RefreshWallpaper(showConfirmation: true);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "设置保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshWallpaper(bool showConfirmation)
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            var now = DateTime.Now;
            WallpaperRenderer.Render(
                _settings.BackgroundPath,
                AppStorage.RenderedWallpaperPath,
                bounds.Width,
                bounds.Height,
                now.Date,
                _settings.ScheduleXPercent,
                now);
            WallpaperManager.Apply(AppStorage.RenderedWallpaperPath);
            _lastRefreshDay = now.Date;
            _lastRenderedMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            _lastScreenSize = bounds.Size;
            _settings.LastRenderedDate = _lastRefreshDay.ToString("yyyy-MM-dd");
            _settings.LastRenderedWidth = bounds.Width;
            _settings.LastRenderedHeight = bounds.Height;
            SettingsStore.Save(_settings);

            if (showConfirmation)
            {
                _notifyIcon.ShowBalloonTip(2500, "壁纸已刷新", $"已按 {bounds.Width}×{bounds.Height} 重新生成。", ToolTipIcon.Info);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "壁纸刷新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static void ShowManualStartMessage(bool isFirstRun)
    {
        if (WallpaperEngineDetector.IsRunning())
        {
            MessageBox.Show(
                "Windows 静态壁纸已经设置成功，但检测到 Wallpaper Engine 正在运行。\n\n" +
                "Wallpaper Engine 会持续覆盖系统静态壁纸，所以桌面暂时看不到倒计时；这也是为什么“个性化”页面已经显示新壁纸，而桌面没有变化。\n\n" +
                "请暂时暂停或退出 Wallpaper Engine 后查看。本程序不受 Windows 是否激活影响；希沃电脑若未安装 Wallpaper Engine，将会直接显示。",
                "壁纸已设置，但被 Wallpaper Engine 覆盖",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var firstRunText = isFirstRun ? "已保存原壁纸，并启用开机自动启动。\n\n" : string.Empty;
        MessageBox.Show(
            firstRunText +
            "倒计时壁纸已经设置成功。程序会留在任务栏右下角运行。\n\n" +
            "右键酒红色“稳”图标，打开“启动设置”即可统一管理底图、课表位置、开机启动和原壁纸恢复。",
            "郑老师中考倒计时",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void RefreshIfNeeded()
    {
        var now = DateTime.Now;
        var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        if (_lastRefreshDay != now.Date || _lastRenderedMinute != currentMinute || _lastScreenSize != bounds.Size)
        {
            RefreshWallpaper(showConfirmation: false);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        RefreshWallpaper(showConfirmation: false);
    }

    private void ChooseBackground()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择新的底图（课表与倒计时不会改变）",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp|JPEG 图片|*.jpg;*.jpeg|PNG 图片|*.png|BMP 图片|*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        try
        {
            _settings.BackgroundPath = AppStorage.ImportBackground(dialog.FileName);
            SettingsStore.Save(_settings);
            RefreshWallpaper(showConfirmation: true);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "更换底图失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreDefaultBackground()
    {
        _settings.BackgroundPath = AppStorage.DefaultBackgroundPath;
        SettingsStore.Save(_settings);
        RefreshWallpaper(showConfirmation: true);
    }

    private void AdjustSchedulePosition()
    {
        using var dialog = new SchedulePositionDialog(_settings.ScheduleXPercent);
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settings.ScheduleXPercent = dialog.SelectedXPercent;
        SettingsStore.Save(_settings);
        RefreshWallpaper(showConfirmation: true);
    }

    private void SetAutoStart(bool enabled)
    {
        try
        {
            AutoStartManager.SetEnabled(enabled);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "开机启动设置失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OpenWallpaperDirectory()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AppStorage.RootDirectory,
            UseShellExecute = true
        });
    }

    private void DisableAndRestore()
    {
        try
        {
            AutoStartManager.SetEnabled(false);
            if (!string.IsNullOrWhiteSpace(_settings.OriginalWallpaperPath) && File.Exists(_settings.OriginalWallpaperPath))
            {
                WallpaperManager.Apply(_settings.OriginalWallpaperPath);
            }
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
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
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
