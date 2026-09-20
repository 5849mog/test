using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SteadyDesk;

internal static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SteadyDesk";
    private const string LegacyValueName = "ZhengTeacherCountdownWallpaper";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
            ?? throw new InvalidOperationException("无法写入开机启动设置。");

        if (enabled)
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法获取程序路径。");
            var commandLineArgs = Environment.GetCommandLineArgs();
            var entryAssemblyPath = commandLineArgs.Length > 0 ? commandLineArgs[0] : null;
            var executableName = Path.GetFileNameWithoutExtension(executable);
            var command = string.Equals(executableName, "dotnet", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entryAssemblyPath)
                && File.Exists(entryAssemblyPath)
                ? "\"" + executable + "\" \"" + entryAssemblyPath + "\" --startup"
                : "\"" + executable + "\" --startup";
            key.SetValue(ValueName, command);
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    public static void RemoveLegacyEntries()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        key?.DeleteValue(LegacyValueName, false);
    }
}

internal static class WallpaperManager
{
    private const int SpiSetDesktopWallpaper = 0x0014;
    private const int SpifUpdateIniFile = 0x01;
    private const int SpifSendChange = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(
        int action,
        int parameter,
        string value,
        int flags);

    public static void Apply(
        string imagePath,
        string? wallpaperStyle = null,
        string? tileWallpaper = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            throw new FileNotFoundException("找不到要设置的壁纸。", imagePath);
        }

        using (var desktopKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
        {
            desktopKey?.SetValue("WallpaperStyle", wallpaperStyle ?? "10");
            desktopKey?.SetValue("TileWallpaper", tileWallpaper ?? "0");
        }

        if (!SystemParametersInfo(
                SpiSetDesktopWallpaper,
                0,
                imagePath,
                SpifUpdateIniFile | SpifSendChange))
        {
            throw new InvalidOperationException(
                "设置壁纸失败，Windows 错误码：" + Marshal.GetLastWin32Error());
        }
    }
}

internal static class WallpaperEngineDetector
{
    private static readonly string[] ProcessNames = ["wallpaper64", "wallpaper32"];

    public static bool IsRunning()
    {
        foreach (var processName in ProcessNames)
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            try
            {
                if (processes.Length > 0)
                {
                    return true;
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        return false;
    }
}
