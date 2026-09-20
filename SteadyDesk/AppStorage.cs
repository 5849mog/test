using System.Reflection;
using Microsoft.Win32;

namespace SteadyDesk;

internal static class AppStorage
{
    private const string EmbeddedBackgroundName = "SteadyDesk.Assets.wallpaper-background-v1.png";

    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "稳序桌面");

    public static string ConfigPath => Path.Combine(RootDirectory, "config.json");
    public static string DefaultBackgroundPath => Path.Combine(RootDirectory, "默认背景.png");
    public static string RenderedWallpaperPath => Path.Combine(RootDirectory, "当前壁纸.jpg");
    public static string PreviewWallpaperPath => Path.Combine(RootDirectory, "预览壁纸.jpg");
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
        if (File.Exists(DefaultBackgroundPath))
        {
            return;
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedBackgroundName)
            ?? throw new InvalidOperationException("程序内置背景资源缺失。");
        using var output = File.Create(DefaultBackgroundPath);
        stream.CopyTo(output);
    }
}
