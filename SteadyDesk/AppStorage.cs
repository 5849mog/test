using System.Reflection;
using Microsoft.Win32;

namespace SteadyDesk;

internal sealed record OriginalWallpaperSnapshot(
    string? Path,
    string? Style,
    string? TileWallpaper);

internal static class AppStorage
{
    private const string EmbeddedBackgroundName = "SteadyDesk.Assets.wallpaper-background-v1.png";

    public static string RootDirectory { get; } = ResolveRootDirectory();

    public static string ConfigPath => Path.Combine(RootDirectory, "config.json");
    public static string DefaultBackgroundPath => Path.Combine(RootDirectory, "默认背景.png");
    public static string RenderedWallpaperPath => Path.Combine(RootDirectory, "当前壁纸.jpg");
    public static string PreviewWallpaperPath => Path.Combine(RootDirectory, "预览壁纸.jpg");
    public static string OriginalWallpaperPath => Path.Combine(RootDirectory, "原壁纸.png");
    public static string CustomBackgroundPath => Path.Combine(RootDirectory, "自定义背景.png");

    private static string ResolveRootDirectory()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable("STEADY_DESK_DATA_DIR");
        return string.IsNullOrWhiteSpace(overrideDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "稳序桌面")
            : Path.GetFullPath(overrideDirectory);
    }

    public static void EnsureInitialized()
    {
        Directory.CreateDirectory(RootDirectory);
        ExtractDefaultBackground();
    }

    public static string CreateTemporaryBackgroundPath()
    {
        Directory.CreateDirectory(RootDirectory);
        return Path.Combine(RootDirectory, ".pending-background-" + Guid.NewGuid().ToString("N") + ".png");
    }

    public static string ImportBackground(string sourcePath, string? destinationPath = null)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath ?? CustomBackgroundPath);
        if (sourcePath.Equals(destination, StringComparison.OrdinalIgnoreCase))
        {
            return destination;
        }

        using var image = Image.FromFile(sourcePath);
        if (image.Width < 640 || image.Height < 360)
        {
            throw new InvalidOperationException("请选择至少 640×360 的图片。建议使用 16:9 横向图片。");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? RootDirectory);
        var temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var copy = new Bitmap(image);
            copy.Save(temporaryPath, System.Drawing.Imaging.ImageFormat.Png);
            File.Move(temporaryPath, destination, true);
            return destination;
        }
        finally
        {
            DeleteIfExists(temporaryPath);
        }
    }

    public static string CommitBackground(string stagedPath)
    {
        var destination = CustomBackgroundPath;
        if (string.IsNullOrWhiteSpace(stagedPath)
            || !File.Exists(stagedPath)
            || stagedPath.Equals(destination, StringComparison.OrdinalIgnoreCase))
        {
            return stagedPath;
        }

        File.Move(stagedPath, destination, true);
        return destination;
    }

    public static OriginalWallpaperSnapshot CaptureOriginalWallpaper()
    {
        try
        {
            using var desktopKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var currentPath = desktopKey?.GetValue("WallPaper") as string;
            var style = desktopKey?.GetValue("WallpaperStyle") as string;
            var tileWallpaper = desktopKey?.GetValue("TileWallpaper") as string;

            if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
            {
                return new OriginalWallpaperSnapshot(currentPath, style, tileWallpaper);
            }

            using var image = Image.FromFile(currentPath);
            using var copy = new Bitmap(image);
            copy.Save(OriginalWallpaperPath, System.Drawing.Imaging.ImageFormat.Png);
            return new OriginalWallpaperSnapshot(OriginalWallpaperPath, style, tileWallpaper);
        }
        catch
        {
            return new OriginalWallpaperSnapshot(null, null, null);
        }
    }

    public static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
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
