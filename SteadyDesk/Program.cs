namespace SteadyDesk;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        AppStorage.EnsureInitialized();

        var firstRun = !ConfigStore.Exists;
        var config = ConfigStore.Load();

        if (TryRunRenderPreview(args, config))
        {
            return;
        }

        using var mutex = new Mutex(true, "Local\\SteadyDeskWallpaper", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "稳序桌面已经在运行。请在任务栏右下角找到图标。",
                "稳序桌面",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext(
            config,
            firstRun,
            args.Contains("--startup", StringComparer.OrdinalIgnoreCase),
            args.Contains("--settings", StringComparer.OrdinalIgnoreCase)));
    }

    private static bool TryRunRenderPreview(string[] args, AppConfig config)
    {
        if (args.Length < 2 || !args[0].Equals("--render-preview", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var outputPath = Path.GetFullPath(args[1]);
        var width = args.Length >= 3 && int.TryParse(args[2], out var parsedWidth) ? parsedWidth : 1920;
        var height = args.Length >= 4 && int.TryParse(args[3], out var parsedHeight) ? parsedHeight : 1080;

        if (args.Length >= 5)
        {
            config.Wallpaper.BackgroundPath = Path.GetFullPath(args[4]);
        }

        if (args.Length >= 6 && float.TryParse(args[5], out var parsedX))
        {
            config.Wallpaper.ScheduleXPercent = parsedX;
        }

        var now = DateTime.Now;
        if (args.Length >= 7 && DateTime.TryParse(args[6], out var parsedDate))
        {
            now = parsedDate.Date.Add(now.TimeOfDay);
        }

        if (args.Length >= 8 && DateTime.TryParse(args[7], out var parsedNow))
        {
            now = parsedNow;
        }

        WallpaperRenderer.Render(config, outputPath, width, height, now);
        return true;
    }
}
