using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SteadyDesk.UpdateHelper;

internal static class Program
{
    private const string AppFileName = "稳序桌面.exe";
    private const string HelperFileName = "SteadyDesk.UpdateHelper.exe";
    private const long MaximumArchiveBytes = 256L * 1024 * 1024;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length != 5 || !string.Equals(args[0], "--apply", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("更新参数无效。");
            }

            ApplyUpdate(args[1], args[2], args[3], args[4]);
            return 0;
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
            return 1;
        }
    }

    private static void ApplyUpdate(string archivePath, string processIdText, string appPath, string expectedHash)
    {
        if (!int.TryParse(processIdText, out var processId)
            || expectedHash.Length != 64
            || !expectedHash.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("更新参数格式错误。");
        }

        archivePath = Path.GetFullPath(archivePath);
        appPath = Path.GetFullPath(appPath);
        var appDirectory = Path.GetDirectoryName(appPath)
            ?? throw new InvalidOperationException("无法确定程序安装目录。");
        var expectedInstallDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "SteadyDesk");
        var helperPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定更新组件路径。");

        if (!string.Equals(Path.GetFileName(appPath), AppFileName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(helperPath), HelperFileName, StringComparison.OrdinalIgnoreCase)
            || !PathsEqual(Path.GetDirectoryName(helperPath) ?? string.Empty, appDirectory)
            || !PathsEqual(appDirectory, expectedInstallDirectory)
            || !IsInsideUpdatesDirectory(archivePath))
        {
            throw new InvalidOperationException("程序、更新组件和更新包位置不匹配，已停止安装。");
        }

        var archiveInfo = new FileInfo(archivePath);
        if (!archiveInfo.Exists || archiveInfo.Length is <= 0 or > MaximumArchiveBytes)
        {
            throw new InvalidDataException("更新包不存在或大小异常。");
        }

        using (var archiveStream = File.OpenRead(archivePath))
        {
            var hash = Convert.ToHexString(SHA256.HashData(archiveStream));
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(hash),
                    Convert.FromHexString(expectedHash)))
            {
                throw new InvalidDataException("更新包校验失败，未修改当前程序。");
            }
        }

        WaitForProcessToExit(processId);

        var workDirectory = Path.Combine(appDirectory, ".steady-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var stagedAppPath = Path.Combine(workDirectory, AppFileName);
        var backupAppPath = Path.Combine(workDirectory, AppFileName + ".backup");
        var replaced = false;

        try
        {
            ExtractApplication(archivePath, stagedAppPath);
            if (!File.Exists(appPath))
            {
                throw new FileNotFoundException("未找到当前安装的稳序桌面程序。", appPath);
            }

            File.Copy(appPath, backupAppPath, false);
            File.Move(stagedAppPath, appPath, true);
            replaced = true;

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = appDirectory,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("新版本程序启动失败。");

            Thread.Sleep(1500);
            if (process.HasExited && process.ExitCode != 0)
            {
                throw new InvalidOperationException("新版本启动失败，已恢复旧版本。");
            }

            AppStorageCleanup.DeleteFile(archivePath);
        }
        catch
        {
            if (replaced && File.Exists(backupAppPath))
            {
                File.Copy(backupAppPath, appPath, true);
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = appPath,
                        WorkingDirectory = appDirectory,
                        UseShellExecute = false
                    });
                }
                catch
                {
                    // The previous executable is restored even if Windows cannot relaunch it.
                }
            }

            throw;
        }
        finally
        {
            try
            {
                Directory.Delete(workDirectory, true);
            }
            catch
            {
                // A leftover temporary directory is harmless and can be removed later.
            }
        }
    }

    private static void ExtractApplication(string archivePath, string destinationPath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var appEntries = archive.Entries
            .Where(entry => string.Equals(entry.FullName, AppFileName, StringComparison.Ordinal))
            .ToArray();
        if (archive.Entries.Count != 1 || appEntries.Length != 1
            || appEntries[0].Length is <= 1024 * 1024 or > MaximumArchiveBytes)
        {
            throw new InvalidDataException("更新包内容不符合稳序桌面的发布格式。");
        }

        using var source = appEntries[0].Open();
        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(destination);
        destination.Flush(true);
    }

    private static void WaitForProcessToExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(90_000))
            {
                throw new TimeoutException("稳序桌面没有及时退出，更新已取消。");
            }
        }
        catch (ArgumentException)
        {
            // The application already exited before the updater started.
        }
    }

    private static bool IsInsideUpdatesDirectory(string path)
    {
        var updatesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "稳序桌面",
            "updates");
        var normalizedDirectory = Path.GetFullPath(updatesDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ShowError(string message)
    {
        try
        {
            MessageBox.Show(
                message + "\n\n当前版本仍保留，稍后可以重新尝试。",
                "稳序桌面更新失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch
        {
            // The helper may be running in a non-interactive session.
        }
    }

    private static class AppStorageCleanup
    {
        public static void DeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Retaining a verified package does not affect the installed program.
            }
        }
    }
}
