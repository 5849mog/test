using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace CountdownWallpaper;

internal static class AppStorage
{
    private const string EmbeddedBackgroundName = "CountdownWallpaper.Assets.wallpaper-background-v1.png";

    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "郑老师中考倒计时");

    public static string SettingsPath => Path.Combine(RootDirectory, "settings.json");
    public static string DefaultBackgroundPath => Path.Combine(RootDirectory, "默认背景.png");
    public static string RenderedWallpaperPath => Path.Combine(RootDirectory, "当前壁纸.jpg");
    public static string OriginalWallpaperPath => Path.Combine(RootDirectory, "原壁纸.png");

    public static void EnsureInitialized()
    {
        Directory.CreateDirectory(RootDirectory);
        ExtractDefaultBackground();
    }

    public static string ImportBackground(string sourcePath)
    {
        var destination = Path.Combine(RootDirectory, "自定义背景.png");
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            return destination;
        }

        using var image = Image.FromFile(sourcePath);
        if (image.Width < 640 || image.Height < 360)
        {
            throw new InvalidOperationException("请选择至少 640×360 的图片。建议使用 16:9 横向图片。");
        }

        using var copy = new Bitmap(image);
        copy.Save(destination, System.Drawing.Imaging.ImageFormat.Png);
        return destination;
    }

    public static string? CaptureOriginalWallpaper()
    {
        try
        {
            using var desktopKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var currentPath = desktopKey?.GetValue("WallPaper") as string;
            if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
            {
                return currentPath;
            }

            using var image = Image.FromFile(currentPath);
            using var copy = new Bitmap(image);
            copy.Save(OriginalWallpaperPath, System.Drawing.Imaging.ImageFormat.Png);
            return OriginalWallpaperPath;
        }
        catch
        {
            return null;
        }
    }

    private static void ExtractDefaultBackground()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedBackgroundName)
            ?? throw new InvalidOperationException("程序内置背景资源缺失。");
        using var output = File.Create(DefaultBackgroundPath);
        stream.CopyTo(output);
    }
}

internal sealed class AppSettings
{
    public string BackgroundPath { get; set; } = AppStorage.DefaultBackgroundPath;
    public string? OriginalWallpaperPath { get; set; }
    public float ScheduleXPercent { get; set; } = 21.5f;
    public string? LastRenderedDate { get; set; }
    public int LastRenderedWidth { get; set; }
    public int LastRenderedHeight { get; set; }
}

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static bool Exists => File.Exists(AppStorage.SettingsPath);

    public static AppSettings Load()
    {
        if (!File.Exists(AppStorage.SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppStorage.SettingsPath), JsonOptions)
                ?? new AppSettings();
            if (string.IsNullOrWhiteSpace(settings.BackgroundPath) || !File.Exists(settings.BackgroundPath))
            {
                settings.BackgroundPath = AppStorage.DefaultBackgroundPath;
            }

            settings.ScheduleXPercent = Math.Clamp(settings.ScheduleXPercent, 3f, 25f);

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        File.WriteAllText(AppStorage.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
