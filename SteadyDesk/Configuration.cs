using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteadyDesk;

internal sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 4;
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
        var sourceSchemaVersion = SchemaVersion;

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

        Events = Events.Where(item => item is not null).ToList();
        Content.Quotes ??= [];

        // v2 及更早版本没有明确区分“用户清空”和“配置缺失”。
        if (sourceSchemaVersion < 3)
        {
            if (!ScheduleEngine.HasAnyScheduleData(Schedule))
            {
                Schedule = ScheduleConfig.CreateDefault();
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

        ScheduleEngine.Normalize(Schedule, sourceSchemaVersion);
        NormalizeLayout();

        foreach (var item in Events)
        {
            item.Id = string.IsNullOrWhiteSpace(item.Id)
                ? Guid.NewGuid().ToString("N")
                : item.Id;
            item.Title = string.IsNullOrWhiteSpace(item.Title) ? "未命名事件" : item.Title.Trim();
            item.Color = string.IsNullOrWhiteSpace(item.Color) ? "#B69A68" : item.Color.Trim();
            item.Date = item.Date.Date;
        }

        Behavior.RefreshIntervalSeconds = Math.Clamp(Behavior.RefreshIntervalSeconds, 5, 300);
        SchemaVersion = 4;
    }

    private void NormalizeLayout()
    {
        const float scheduleWidth = 46f;
        const float rightMargin = 3f;
        const float gap = 2f;

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
    public List<SchedulePeriodConfig> Periods { get; set; } = [];
    public List<ScheduleDayConfig> Days { get; set; } = [];
    public List<ScheduleDateOverrideConfig> DateOverrides { get; set; } = [];

    // 仅用于读取 v3 及更早版本。迁移完成后会清空且不再写入 JSON。
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ScheduleRowConfig>? Rows { get; set; }

    public static ScheduleConfig CreateDefault() => ScheduleDefaults.Create();
}

internal sealed class SchedulePeriodConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string? Note { get; set; }
    public ScheduleCellKind Kind { get; set; } = ScheduleCellKind.Class;
}

internal sealed class ScheduleDayConfig
{
    public int DayIndex { get; set; }
    public List<ScheduleCellConfig> Cells { get; set; } = [];
}

internal sealed class ScheduleCellConfig
{
    public string PeriodId { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public ScheduleCellKind Kind { get; set; } = ScheduleCellKind.Class;
    public string? StartOverride { get; set; }
    public string? EndOverride { get; set; }
}

internal sealed class ScheduleDateOverrideConfig
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string Label { get; set; } = string.Empty;
    public bool IsDayOff { get; set; }
    public int? BaseDayIndex { get; set; }
    public List<ScheduleCellConfig> Cells { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum ScheduleCellKind
{
    Class,
    Break,
    Empty
}

// v3 兼容模型。
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
        "每一个清晨的坚持，都在为 6 月的答案加分。\n今天，也要稳稳地走一步。",
        "不急着和昨天比较。\n今天多做一点，六月就多一分底气。",
        "把会做的题做稳，\n把不会的题一点点变成会。",
        "每一次按时出发，\n都在靠近想去的六月。",
        "成绩会记录努力，\n时间会回答坚持。",
        "先完成今天的目标，\n再把目光放远一点。",
        "不慌，不乱，不停步。\n稳住节奏，稳住自己。",
        "现在的每一页，\n都会成为考场上的底牌。",
        "把基础打牢，把心态放稳，\n答案自然会越来越清楚。",
        "早起一点，专注一点，\n今天也会有新的收获。"
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
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string? LastRecoveryPath { get; private set; }
    public static string? LastMigrationBackupPath { get; private set; }
    private static int? _pendingMigrationSourceVersion;

    public static bool Exists => File.Exists(AppStorage.ConfigPath);

    public static AppConfig Load()
    {
        LastRecoveryPath = null;
        LastMigrationBackupPath = null;
        _pendingMigrationSourceVersion = null;
        AppConfig? config;
        var shouldSave = false;

        if (!File.Exists(AppStorage.ConfigPath))
        {
            config = LoadLegacyOrDefault();
            shouldSave = true;
        }
        else
        {
            var json = File.ReadAllText(AppStorage.ConfigPath);
            try
            {
                config = JsonSerializer.Deserialize<AppConfig>(json, Options);
                if (config is null)
                {
                    throw new JsonException("配置文件为空。");
                }
            }
            catch (JsonException)
            {
                LastRecoveryPath = BackupCorruptConfig();
                config = LoadLegacyOrDefault();
                shouldSave = true;
            }
        }

        var sourceSchemaVersion = config.SchemaVersion;
        var requiresMigration = sourceSchemaVersion < 4
            || config.Schedule?.Rows is not null;
        if (requiresMigration && File.Exists(AppStorage.ConfigPath))
        {
            LastMigrationBackupPath = BackupBeforeMigration(sourceSchemaVersion);
            if (LastMigrationBackupPath is null)
            {
                _pendingMigrationSourceVersion = sourceSchemaVersion;
            }
            else
            {
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
        if (_pendingMigrationSourceVersion is int sourceSchemaVersion
            && File.Exists(AppStorage.ConfigPath))
        {
            var backupPath = BackupBeforeMigration(sourceSchemaVersion);
            if (backupPath is null)
            {
                throw new IOException("旧版配置尚未成功备份，为避免数据丢失，本次保存已停止。");
            }

            LastMigrationBackupPath = backupPath;
            _pendingMigrationSourceVersion = null;
        }
        else if (_pendingMigrationSourceVersion.HasValue)
        {
            _pendingMigrationSourceVersion = null;
        }

        config.Normalize();
        Directory.CreateDirectory(AppStorage.RootDirectory);
        var temporaryPath = AppStorage.ConfigPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(config, Options));
        File.Move(temporaryPath, AppStorage.ConfigPath, true);
    }

    private static string? BackupBeforeMigration(int sourceSchemaVersion)
    {
        try
        {
            var backupPath = AppStorage.ConfigPath
                + ".v" + sourceSchemaVersion
                + "-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
                + ".json";
            File.Copy(AppStorage.ConfigPath, backupPath, false);
            return backupPath;
        }
        catch
        {
            return null;
        }
    }

    private static string? BackupCorruptConfig()
    {
        try
        {
            var backupPath = AppStorage.ConfigPath
                + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
                + ".json";
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
