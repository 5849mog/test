namespace CountdownWallpaper;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        AppStorage.EnsureInitialized();

        if (TryRunRenderPreview(args))
        {
            return;
        }

        using var mutex = new Mutex(true, "Local\\ZhengTeacherCountdownWallpaper", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "中考倒计时已经在运行。请在任务栏右下角找到酒红色图标。",
                "郑老师中考倒计时",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext(
            args.Contains("--startup", StringComparer.OrdinalIgnoreCase),
            args.Contains("--settings", StringComparer.OrdinalIgnoreCase)));
    }

    private static bool TryRunRenderPreview(string[] args)
    {
        if (args.Length < 2 || !args[0].Equals("--render-preview", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var outputPath = Path.GetFullPath(args[1]);
        var width = args.Length >= 3 && int.TryParse(args[2], out var parsedWidth) ? parsedWidth : 1920;
        var height = args.Length >= 4 && int.TryParse(args[3], out var parsedHeight) ? parsedHeight : 1080;
        var settings = SettingsStore.Load();
        var backgroundPath = args.Length >= 5 ? Path.GetFullPath(args[4]) : settings.BackgroundPath;
        var scheduleXPercent = args.Length >= 6 && float.TryParse(args[5], out var parsedX)
            ? parsedX
            : settings.ScheduleXPercent;
        var today = args.Length >= 7 && DateTime.TryParse(args[6], out var parsedDate)
            ? parsedDate.Date
            : DateTime.Today;
        var now = args.Length >= 8 && DateTime.TryParse(args[7], out var parsedNow)
            ? parsedNow
            : DateTime.Now;
        WallpaperRenderer.Render(backgroundPath, outputPath, width, height, today, scheduleXPercent, now);
        return true;
    }
}
