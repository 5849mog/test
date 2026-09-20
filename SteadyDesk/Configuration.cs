using System.Text.Json;

namespace SteadyDesk;

internal sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 3;
    public WallpaperConfig Wallpaper { get; set; } = WallpaperConfig.CreateDefault();
    public ScheduleConfig Schedule { get; set; } = ScheduleConfig.CreateDefault();
    public List<CountdownEventConfig> Events { get; set; } = CountdownEventConfig.CreateDefault();
    public ThemeConfig Theme { get; set; } = ThemeConfig.CreateDefault();
    public ContentConfig Content { get; set; } = ContentConfig.CreateDefault();
    public BehaviorConfig Behavior { get; set; } = BehaviorConfig.CreateDefault();

    public static AppConfig CreateDefault() => new();

    public AppConfig Clone()
    {
        return JsonSerializer.Deserialize<AppConfig>(
            JsonSerializer.Serialize(this, ConfigStore.Options),
            ConfigStore.Options) ?? CreateDefault();
    }

    public void Normalize()
    {
        Wallpaper ??= WallpaperConfig.CreateDefault();
        Schedule ??= ScheduleConfig.CreateDefault();
        Events ??= [];
        Theme ??= ThemeConfig.CreateDefault();
        Content ??= ContentConfig.CreateDefault();
        Behavior ??= BehaviorConfig.CreateDefault();

        Wallpaper.BackgroundPath = string.IsNullOrWhiteSpace(Wallpaper.BackgroundPath)
            ? AppStorage.DefaultBackgroundPath
            : Wallpaper.BackgroundPath;

        Wallpaper.ScheduleXPercent = Math.Clamp(Wallpaper.ScheduleXPercent, 3f, 25f);
        Wallpaper.CountdownXPercent = Math.Clamp(Wallpaper.CountdownXPercent, 50f, 70f);
        Wallpaper.CountdownWidthPercent = Math.Clamp(Wallpaper.CountdownWidthPercent, 25f, 44f);

        if (Schedule.Weekdays == null || Schedule.Weekdays.Count != 5)
        {
            Schedule.Weekdays = ["周一", "周二", "周三", "周四", "周五"];
        }

        Schedule.Rows ??= [];
        Events = Events.Where(item => item is not null).ToList();
        Content.Quotes ??= [];

        // v2 及更早版本没有明确区分“用户清空”和“配置缺失”。
        // 只对旧版本的空集合补回默认值；v3 允许用户保存空列表。
        if (SchemaVersion < 3)
        {
            if (Schedule.Rows.Count == 0)
            {
                Schedule.Rows = ScheduleConfig.CreateDefault().Rows;
            }

            if (Events.Count == 0)
            {
                Events = CountdownEventConfig.CreateDefault();
            }

            if (Content.Quotes.Count == 0)
            {
                Content.Quotes = ContentConfig.CreateDefault().Quotes;
            }
        }

        NormalizeLayout();

        foreach (var row in Schedule.Rows)
        {
            row.Courses ??= [];
            while (row.Courses.Count < 5)
            {
                row.Courses.Add(string.Empty);
            }

            if (row.Courses.Count > 5)
            {
                row.Courses = row.Courses.Take(5).ToList();
            }
        }

        foreach (var item in Events)
        {
            item.Id = string.IsNullOrWhiteSpace(item.Id)
                ? Guid.NewGuid().ToString("N")
                : item.Id;
            item.Title = string.IsNullOrWhiteSpace(item.Title) ? "未命名事件" : item.Title;
            item.Color = string.IsNullOrWhiteSpace(item.Color) ? "#B69A68" : item.Color;
            item.Date = item.Date.Date;
        }

        Behavior.RefreshIntervalSeconds = Math.Clamp(Behavior.RefreshIntervalSeconds, 5, 300);
        SchemaVersion = 3;
    }

    private void NormalizeLayout()
    {
        const float scheduleWidth = 46f;
        const float rightMargin = 3f;
        const float gap = 2f;

        // 两块面板必须在同一张壁纸内完整显示，并至少保留一个小间距。
        var maxCountdownX = 100f - Wallpaper.CountdownWidthPercent - rightMargin;
        Wallpaper.CountdownXPercent = Math.Clamp(
            Wallpaper.CountdownXPercent,
            50f,
            maxCountdownX);

        var maximumScheduleX = Wallpaper.CountdownXPercent - gap - scheduleWidth;
        if (Wallpaper.ScheduleXPercent > maximumScheduleX)
        {
            Wallpaper.ScheduleXPercent = Math.Clamp(maximumScheduleX, 3f, 25f);
        }

        var scheduleRight = Wallpaper.ScheduleXPercent + scheduleWidth;
        if (Wallpaper.CountdownXPercent < scheduleRight + gap)
        {
            Wallpaper.CountdownXPercent = Math.Clamp(
                scheduleRight + gap,
                50f,
                maxCountdownX);
        }
    }
}

internal sealed class WallpaperConfig
{
    public string BackgroundPath { get; set; } = AppStorage.DefaultBackgroundPath;
    public string? OriginalWallpaperPath { get; set; }
    public string? OriginalWallpaperStyle { get; set; }
    public string? OriginalTileWallpaper { get; set; }
    public float ScheduleXPercent { get; set; } = 9f;
    public float CountdownXPercent { get; set; } = 57f;
    public float CountdownWidthPercent { get; set; } = 40f;
    public bool ShowClock { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public bool ShowQuote { get; set; } = true;

    public static WallpaperConfig CreateDefault() => new();
}

internal sealed class ScheduleConfig
{
    public List<string> Weekdays { get; set; } = ["周一", "周二", "周三", "周四", "周五"];
    public List<ScheduleRowConfig> Rows { get; set; } = [];

    public static ScheduleConfig CreateDefault()
    {
        return new ScheduleConfig
        {
            Rows =
            [
                new() { Label = "第一节课", Time = "08:00–08:40", Start = "08:00", End = "08:40", Courses = ["语文", "英语", "道法", "化学", "语文"] },
                new() { Label = "第二节课", Time = "08:55–09:35", Start = "08:55", End = "09:35", Courses = ["语文", "英语", "数学", "语文", "语文"] },
                new() { Label = "大课间", Time = "09:35–10:05", Start = "09:35", End = "10:05", IsBreak = true, Courses = ["做操 / 运动 / 学习"] },
                new() { Label = "第三节课", Time = "10:05–10:45", Start = "10:05", End = "10:45", Courses = ["道法", "数学", "化学", "英语", "物理"] },
                new() { Label = "第四节课", Time = "11:00–11:40", Start = "11:00", End = "11:40", Courses = ["英语", "体育", "体育", "英语", "物理"] },
                new() { Label = "第五节课", Time = "11:55–12:35", Start = "11:55", End = "12:35", Courses = ["英语", "数学", "英语", "美术（单）\\n音乐（双）", "生物（单）\\n地理（双）"] },
                new() { Label = "第六节课", Time = "12:40–13:20", Start = "12:40", End = "13:20", Courses = ["—", "—", "—", "—", "13:20–14:00\\n体育"] },
                new() { Label = "第七节课", Time = "13:35–14:15", Start = "13:35", End = "14:15", Courses = ["物理", "语文", "语文", "体育", "14:15–14:55\\n历史"] },
                new() { Label = "第八节课", Time = "14:30–15:10", Start = "14:30", End = "15:10", Courses = ["历史", "化学", "语文", "物理", "15:10–15:50\\n数学"] },
                new() { Label = "第九节课", Time = "15:25–16:05", Start = "15:25", End = "16:05", Courses = ["数学", "化学", "历史", "数学", "16:00–16:40\\n英语"] },
                new() { Label = "第十节课", Time = "16:15–16:55", Start = "16:15", End = "16:55", Courses = ["数学", "物理", "道法", "数学", "16:50–17:30\\n班会"] },
                new() { Label = "第十一节课", Time = "17:10–18:10\\n晚托走班", Start = "17:10", End = "18:10", Courses = ["物理（单）\\n数学（双）", "体活", "化学（单）\\n语文（双）", "历史/道法（单）\\n英语（双）", "—"] },
                new() { Label = "延时服务", Time = "18:40–20:30", Start = "18:40", End = "20:30", Courses = ["语文", "18:30–20:30\\n物理（单）\\n化学（双）", "数学", "英语", "—"] }
            ]
        };
    }
}

internal sealed class ScheduleRowConfig
{
    public string Label { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public bool IsBreak { get; set; }
    public List<string> Courses { get; set; } = ["", "", "", "", ""];
}

internal sealed class CountdownEventConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "中考";
    public DateTime Date { get; set; } = new(2027, 6, 19);
    public bool Visible { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public string Color { get; set; } = "#B69A68";

    public static List<CountdownEventConfig> CreateDefault()
    {
        return
        [
            new() { Id = "senior-high-exam", Title = "中考", Date = new DateTime(2027, 6, 19), Color = "#E5CB97" },
            new() { Id = "mock-exam", Title = "一模考试", Date = new DateTime(2027, 1, 12), Color = "#B69A68" }
        ];
    }
}

internal sealed class ThemeConfig
{
    public string Wine { get; set; } = "#6D1F2A";
    public string WineDeep { get; set; } = "#54151E";
    public string Paper { get; set; } = "#FAF6EE";
    public string Ink { get; set; } = "#241E1B";
    public string InkSoft { get; set; } = "#5F524B";
    public string Gold { get; set; } = "#B69A68";
    public string Champagne { get; set; } = "#E5CB97";

    public static ThemeConfig CreateDefault() => new();
}

internal sealed class ContentConfig
{
    public string Eyebrow { get; set; } = "COUNTDOWN";
    public string ScheduleTitle { get; set; } = "课程安排";
    public string CountdownTitle { get; set; } = "2027 · 奔赴六月";
    public DateTime PreparationStartDate { get; set; } = new(2026, 9, 1);
    public List<string> Quotes { get; set; } =
    [
        "每一个清晨的坚持，都在为 6 月的答案加分。\\n今天，也要稳稳地走一步。",
        "不急着和昨天比较。\\n今天多做一点，六月就多一分底气。",
        "把会做的题做稳，\\n把不会的题一点点变成会。",
        "每一次按时出发，\\n都在靠近想去的六月。",
        "成绩会记录努力，\\n时间会回答坚持。",
        "先完成今天的目标，\\n再把目光放远一点。",
        "不慌，不乱，不停步。\\n稳住节奏，稳住自己。",
        "现在的每一页，\\n都会成为考场上的底牌。",
        "把基础打牢，把心态放稳，\\n答案自然会越来越清楚。",
        "早起一点，专注一点，\\n今天也会有新的收获。"
    ];

    public static ContentConfig CreateDefault() => new();
}

internal sealed class BehaviorConfig
{
    public bool AutoStart { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 5;

    public static BehaviorConfig CreateDefault() => new();
}

internal static class ConfigStore
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string? LastRecoveryPath { get; private set; }

    public static bool Exists => File.Exists(AppStorage.ConfigPath);

    public static AppConfig Load()
    {
        LastRecoveryPath = null;
        AppConfig? config;
        var shouldSave = false;

        if (!File.Exists(AppStorage.ConfigPath))
        {
            config = LoadLegacyOrDefault();
            shouldSave = true;
        }
        else
        {
            try
            {
                config = JsonSerializer.Deserialize<AppConfig>(
                    File.ReadAllText(AppStorage.ConfigPath), Options);

                if (config is null)
                {
                    throw new JsonException("配置文件为空。");
                }
            }
            catch
            {
                LastRecoveryPath = BackupCorruptConfig();
                config = LoadLegacyOrDefault();
                shouldSave = true;
            }
        }

        config.Normalize();

        if (shouldSave)
        {
            Save(config);
        }

        return config;
    }

    public static void Save(AppConfig config)
    {
        config.Normalize();
        Directory.CreateDirectory(AppStorage.RootDirectory);
        var temporaryPath = AppStorage.ConfigPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(config, Options));
        File.Move(temporaryPath, AppStorage.ConfigPath, true);
    }

    private static string? BackupCorruptConfig()
    {
        try
        {
            var backupPath = AppStorage.ConfigPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
            File.Move(AppStorage.ConfigPath, backupPath, true);
            return backupPath;
        }
        catch
        {
            return null;
        }
    }

    private static AppConfig LoadLegacyOrDefault()
    {
        var config = AppConfig.CreateDefault();
        var legacyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "郑老师中考倒计时",
            "settings.json");

        if (!File.Exists(legacyPath))
        {
            return config;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(legacyPath));
            var root = document.RootElement;
            if (root.TryGetProperty("BackgroundPath", out var background)
                && background.ValueKind == JsonValueKind.String)
            {
                var path = background.GetString();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    config.Wallpaper.BackgroundPath = path;
                }
            }

            if (root.TryGetProperty("OriginalWallpaperPath", out var original)
                && original.ValueKind == JsonValueKind.String)
            {
                config.Wallpaper.OriginalWallpaperPath = original.GetString();
            }

            if (root.TryGetProperty("ScheduleXPercent", out var x)
                && x.TryGetSingle(out var xPercent))
            {
                config.Wallpaper.ScheduleXPercent = xPercent;
            }
        }
        catch
        {
            return AppConfig.CreateDefault();
        }

        return config;
    }
}
