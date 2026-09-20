using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SteadyDesk;

namespace SteadyDesk.ScheduleChecks;

internal static class RenderingStabilityTests
{
    public static void Run(StabilityTestRunner runner)
    {
        runner.Suite("多分辨率与极值内容渲染", () => VerifyRenderingMatrix(runner));
        runner.Suite("渲染输入边界", () => VerifyInvalidDimensions(runner));
    }

    private static void VerifyRenderingMatrix(StabilityTestRunner runner)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "steady-desk-stability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var missingBackground = Path.Combine(root, "missing-background.png");
            var config = AppConfig.CreateDefault();
            config.Wallpaper.BackgroundPath = missingBackground;

            var cases = new[]
            {
                (Width: 480, Height: 270, Date: new DateTime(2026, 9, 21, 8, 20, 0)),
                (Width: 1280, Height: 720, Date: new DateTime(2026, 9, 25, 13, 30, 0)),
                (Width: 1920, Height: 1080, Date: new DateTime(2026, 9, 26, 12, 0, 0)),
                (Width: 2560, Height: 1080, Date: new DateTime(2026, 9, 25, 17, 0, 0)),
                (Width: 1080, Height: 1920, Date: new DateTime(2026, 9, 27, 8, 20, 0))
            };

            foreach (var item in cases)
            {
                var output = Path.Combine(root, $"render-{item.Width}x{item.Height}.jpg");
                WallpaperRenderer.Render(config, output, item.Width, item.Height, item.Date);
                VerifyJpeg(
                    runner,
                    $"分辨率 {item.Width}x{item.Height}",
                    output,
                    item.Width,
                    item.Height,
                    item.Width == 480 ? 1_000 : 8_000);
            }

            var overwritePath = Path.Combine(root, "overwrite.jpg");
            WallpaperRenderer.Render(config, overwritePath, 1280, 720, cases[0].Date);
            WallpaperRenderer.Render(config, overwritePath, 1920, 1080, cases[1].Date);
            VerifyJpeg(runner, "同一路径覆盖且释放文件句柄", overwritePath, 1920, 1080, 8_000);

            var backgroundPath = Path.Combine(root, "source-background.png");
            using (var background = new Bitmap(160, 90))
            using (var graphics = Graphics.FromImage(background))
            {
                graphics.Clear(Color.FromArgb(52, 65, 78));
                using var brush = new LinearGradientBrush(
                    new Rectangle(0, 0, background.Width, background.Height),
                    Color.FromArgb(115, 44, 58),
                    Color.FromArgb(225, 203, 151),
                    15f);
                graphics.FillRectangle(brush, 0, 0, background.Width, background.Height);
                background.Save(backgroundPath, ImageFormat.Png);
            }

            config.Wallpaper.BackgroundPath = backgroundPath;
            var backgroundOutput = Path.Combine(root, "with-background.jpg");
            WallpaperRenderer.Render(config, backgroundOutput, 1280, 720, cases[1].Date);
            VerifyJpeg(runner, "真实底图裁切", backgroundOutput, 1280, 720, 8_000);
            File.Delete(backgroundPath);
            runner.Check("底图读取后句柄已释放", !File.Exists(backgroundPath));

            var emptyConfig = new AppConfig
            {
                SchemaVersion = 4,
                Schedule = new ScheduleConfig(),
                Events = [],
                Content = new ContentConfig
                {
                    Eyebrow = string.Empty,
                    ScheduleTitle = string.Empty,
                    CountdownTitle = string.Empty,
                    Quotes = []
                }
            };
            emptyConfig.Wallpaper.BackgroundPath = missingBackground;
            var emptyOutput = Path.Combine(root, "empty-content.jpg");
            WallpaperRenderer.Render(
                emptyConfig,
                emptyOutput,
                1280,
                720,
                new DateTime(2026, 9, 26, 0, 0, 0));
            VerifyJpeg(runner, "空课表空事件空文案", emptyOutput, 1280, 720, 4_000);

            var extremeConfig = AppConfig.CreateDefault();
            extremeConfig.Wallpaper.BackgroundPath = missingBackground;
            extremeConfig.Theme.Wine = "not-a-color";
            extremeConfig.Theme.WineDeep = "#XYZXYZ";
            extremeConfig.Theme.Paper = string.Empty;
            extremeConfig.Theme.Ink = "transparent-ish";
            extremeConfig.Theme.InkSoft = "!";
            extremeConfig.Theme.Gold = "123";
            extremeConfig.Theme.Champagne = "invalid";
            extremeConfig.Content.ScheduleTitle = new string('课', 120);
            extremeConfig.Content.CountdownTitle = new string('倒', 160);
            extremeConfig.Content.Eyebrow = new string('A', 200);
            extremeConfig.Content.Quotes =
            [
                string.Join("\n", Enumerable.Repeat(new string('长', 80), 8))
            ];
            extremeConfig.Events = Enumerable.Range(0, 18)
                .Select(index => new CountdownEventConfig
                {
                    Id = "event-" + index,
                    Title = "超长事件-" + index + "-" + new string('项', 50),
                    Date = new DateTime(2027, 1, 1).AddDays(index),
                    Color = index % 2 == 0 ? "invalid" : "#B69A68"
                })
                .ToList();
            var extremeOutput = Path.Combine(root, "extreme-content.jpg");
            WallpaperRenderer.Render(
                extremeConfig,
                extremeOutput,
                1920,
                1080,
                new DateTime(2026, 9, 25, 15, 25, 0));
            VerifyJpeg(runner, "超长文案与非法颜色回退", extremeOutput, 1920, 1080, 8_000);

            var repeatedOutput = Path.Combine(root, "repeated.jpg");
            for (var index = 0; index < 12; index++)
            {
                WallpaperRenderer.Render(
                    config,
                    repeatedOutput,
                    640,
                    360,
                    new DateTime(2026, 9, 21, 8, 0, 0).AddMinutes(index * 7));
            }

            VerifyJpeg(runner, "连续重复渲染", repeatedOutput, 640, 360, 2_000);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void VerifyInvalidDimensions(StabilityTestRunner runner)
    {
        var config = AppConfig.CreateDefault();
        var output = Path.Combine(
            Path.GetTempPath(),
            "steady-desk-invalid-" + Guid.NewGuid().ToString("N") + ".jpg");

        runner.Throws<ArgumentOutOfRangeException>(
            "宽度低于 480 被拒绝",
            () => WallpaperRenderer.Render(config, output, 479, 270));
        runner.Throws<ArgumentOutOfRangeException>(
            "高度低于 270 被拒绝",
            () => WallpaperRenderer.Render(config, output, 480, 269));
        runner.Check("非法尺寸不会留下半成品", !File.Exists(output));
    }

    private static void VerifyJpeg(
        StabilityTestRunner runner,
        string name,
        string path,
        int width,
        int height,
        long minimumBytes)
    {
        runner.Check(name + " 生成文件", File.Exists(path));
        var fileInfo = new FileInfo(path);
        runner.Check(name + " 文件大小合理", fileInfo.Length >= minimumBytes);

        var bytes = File.ReadAllBytes(path);
        runner.Check(
            name + " JPEG 文件头正确",
            bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8);

        using var image = Image.FromFile(path);
        runner.Equal(name + " 宽度正确", width, image.Width);
        runner.Equal(name + " 高度正确", height, image.Height);
    }
}
