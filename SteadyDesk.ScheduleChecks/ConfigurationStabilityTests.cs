using System.Text.Json;
using SteadyDesk;

namespace SteadyDesk.ScheduleChecks;

internal static class ConfigurationStabilityTests
{
    public static void Run(StabilityTestRunner runner)
    {
        runner.Suite("配置 JSON 往返与深拷贝", () => VerifyRoundTripAndClone(runner));
        runner.Suite("配置版本语义与范围修正", () => VerifyNormalization(runner));
        runner.Suite("损坏配置与未知枚举", () => VerifyInvalidJson(runner));
    }

    private static void VerifyRoundTripAndClone(StabilityTestRunner runner)
    {
        var config = AppConfig.CreateDefault();
        config.Schedule.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = new DateTime(2026, 10, 1),
            Label = "国庆停课",
            IsDayOff = true
        });
        config.Events.Add(new CountdownEventConfig
        {
            Id = "round-trip",
            Title = "往返测试",
            Date = new DateTime(2027, 3, 2, 18, 30, 0),
            Color = "#123456"
        });
        config.Normalize();

        var json = JsonSerializer.Serialize(config, ConfigStore.Options);
        var restored = JsonSerializer.Deserialize<AppConfig>(json, ConfigStore.Options)
            ?? throw new InvalidOperationException("Configuration round-trip returned null.");
        restored.Normalize();

        runner.Equal("SchemaVersion 写入 v4", 4, restored.SchemaVersion);
        runner.Check("枚举按字符串写入", json.Contains("\"Kind\": \"Class\"", StringComparison.Ordinal));
        runner.Check("迁移专用 Rows 不再写入", !json.Contains("\"Rows\"", StringComparison.Ordinal));
        runner.Check(
            "特殊日期往返保留",
            restored.Schedule.DateOverrides.Any(item =>
                item.Date == new DateTime(2026, 10, 1) && item.IsDayOff));
        runner.Check(
            "事件往返保留",
            restored.Events.Any(item => item.Id == "round-trip" && item.Color == "#123456"));

        var clone = config.Clone();
        clone.Schedule.Days[0].Cells[0].Course = "已修改副本";
        clone.Events[0].Title = "副本事件";
        clone.Content.Quotes[0] = "副本文案";

        runner.Check(
            "课表深拷贝隔离",
            config.Schedule.Days[0].Cells[0].Course != clone.Schedule.Days[0].Cells[0].Course);
        runner.Check("事件深拷贝隔离", config.Events[0].Title != clone.Events[0].Title);
        runner.Check("文案深拷贝隔离", config.Content.Quotes[0] != clone.Content.Quotes[0]);
    }

    private static void VerifyNormalization(StabilityTestRunner runner)
    {
        var extreme = AppConfig.CreateDefault();
        extreme.Wallpaper.ScheduleXPercent = -500f;
        extreme.Wallpaper.CountdownXPercent = 500f;
        extreme.Wallpaper.CountdownWidthPercent = 500f;
        extreme.Behavior.RefreshIntervalSeconds = -1;
        extreme.Events =
        [
            new CountdownEventConfig
            {
                Id = " ",
                Title = " ",
                Date = new DateTime(2027, 1, 1, 23, 59, 59),
                Color = " "
            }
        ];
        extreme.Normalize();

        runner.Check(
            "左侧布局范围受控",
            extreme.Wallpaper.ScheduleXPercent is >= 3f and <= 25f);
        runner.Check(
            "右侧布局范围受控",
            extreme.Wallpaper.CountdownXPercent is >= 50f and <= 70f);
        runner.Check(
            "右侧宽度范围受控",
            extreme.Wallpaper.CountdownWidthPercent is >= 25f and <= 44f);
        runner.Check(
            "左右面板至少保留间距",
            extreme.Wallpaper.CountdownXPercent
                >= extreme.Wallpaper.ScheduleXPercent + 46f + 2f);
        runner.Equal("刷新间隔下限", 5, extreme.Behavior.RefreshIntervalSeconds);
        runner.Equal("事件日期去除时间", new DateTime(2027, 1, 1), extreme.Events[0].Date);
        runner.Equal("空事件标题使用默认值", "未命名事件", extreme.Events[0].Title);
        runner.Equal("空事件颜色使用默认值", "#B69A68", extreme.Events[0].Color);
        runner.Check("空事件标识会自动补齐", !string.IsNullOrWhiteSpace(extreme.Events[0].Id));

        var upperRefresh = AppConfig.CreateDefault();
        upperRefresh.Behavior.RefreshIntervalSeconds = 9999;
        upperRefresh.Normalize();
        runner.Equal("刷新间隔上限", 300, upperRefresh.Behavior.RefreshIntervalSeconds);

        var v4Empty = new AppConfig
        {
            SchemaVersion = 4,
            Schedule = new ScheduleConfig(),
            Events = [],
            Content = new ContentConfig { Quotes = [] }
        };
        v4Empty.Normalize();
        runner.Equal("v4 用户清空课表会保留", 0, v4Empty.Schedule.Periods.Count);
        runner.Equal("v4 用户清空事件会保留", 0, v4Empty.Events.Count);
        runner.Equal("v4 用户清空文案会保留", 0, v4Empty.Content.Quotes.Count);

        var v2Empty = new AppConfig
        {
            SchemaVersion = 2,
            Schedule = new ScheduleConfig(),
            Events = [],
            Content = new ContentConfig { Quotes = [] }
        };
        v2Empty.Normalize();
        runner.Check("v2 空课表按旧语义恢复默认值", v2Empty.Schedule.Periods.Count > 0);
        runner.Check("v2 空事件按旧语义恢复默认值", v2Empty.Events.Count > 0);
        runner.Check("v2 空文案按旧语义恢复默认值", v2Empty.Content.Quotes.Count > 0);

        var firstJson = JsonSerializer.Serialize(extreme, ConfigStore.Options);
        extreme.Normalize();
        var secondJson = JsonSerializer.Serialize(extreme, ConfigStore.Options);
        runner.Equal("应用配置标准化幂等", firstJson, secondJson);
    }

    private static void VerifyInvalidJson(StabilityTestRunner runner)
    {
        runner.Throws<JsonException>(
            "截断 JSON 会被拒绝",
            () => JsonSerializer.Deserialize<AppConfig>("{\"SchemaVersion\": 4", ConfigStore.Options));

        runner.Throws<JsonException>(
            "未知课程格枚举会被拒绝",
            () => JsonSerializer.Deserialize<ScheduleCellConfig>(
                "{\"PeriodId\":\"p\",\"Course\":\"x\",\"Kind\":\"UnknownKind\"}",
                ConfigStore.Options));

        runner.Check(
            "大小写不敏感读取仍可用",
            JsonSerializer.Deserialize<AppConfig>(
                "{\"schemaversion\":4}",
                ConfigStore.Options)?.SchemaVersion == 4);
    }
}
