using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SteadyDesk;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private AppConfig _config;
    private DateTime _lastRefreshDay;
    private DateTime _lastRenderedMinute;
    private Size _lastScreenSize;
    private bool _refreshing;

    public TrayApplicationContext(AppConfig config, bool firstRun, bool startedAutomatically, bool openSettings = false)
    {
        _config = config;
        if (firstRun)
        {
            _config.Wallpaper.OriginalWallpaperPath = AppStorage.CaptureOriginalWallpaper();
            _config.Behavior.AutoStart = true;
            AutoStartManager.SetEnabled(true);
            ConfigStore.Save(_config);
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开控制中心", null, (_, _) => OpenSettings());
        menu.Items.Add("立即刷新壁纸", null, (_, _) => RefreshWallpaper(true));
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
            RefreshWallpaper(true);
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
            WallpaperRenderer.Render(_config, AppStorage.RenderedWallpaperPath, bounds.Width, bounds.Height, now);
            WallpaperManager.Apply(AppStorage.RenderedWallpaperPath);
            _lastRefreshDay = now.Date;
            _lastRenderedMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            _lastScreenSize = bounds.Size;
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
            MessageBox.Show(exception.Message, "壁纸刷新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        if (_lastRefreshDay != now.Date || _lastRenderedMinute != currentMinute || _lastScreenSize != bounds.Size)
        {
            RefreshWallpaper(false);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        RefreshWallpaper(false);
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
        MessageBox.Show(
            firstRunText +
            "稳序桌面已经设置成功，程序会留在任务栏右下角运行。\n\n" +
            "右键图标即可打开控制中心。",
            "稳序桌面",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void DisableAndRestore()
    {
        try
        {
            AutoStartManager.SetEnabled(false);
            if (!string.IsNullOrWhiteSpace(_config.Wallpaper.OriginalWallpaperPath)
                && File.Exists(_config.Wallpaper.OriginalWallpaperPath))
            {
                WallpaperManager.Apply(_config.Wallpaper.OriginalWallpaperPath);
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
