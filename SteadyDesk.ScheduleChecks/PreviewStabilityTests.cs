using SteadyDesk;

namespace SteadyDesk.ScheduleChecks;

internal static class PreviewStabilityTests
{
    public static void Run(StabilityTestRunner runner)
    {
        runner.Suite("设置草稿事务与回退检查", () => VerifyDraftTransactions(runner));
        runner.Suite("实时预览全部场景检查", () => VerifyPreviewScenarios(runner));
    }

    private static void VerifyDraftTransactions(StabilityTestRunner runner)
    {
        var controller = new SettingsDraftController(AppConfig.CreateDefault());
        var accepted = controller.UpdateOrThrow(config =>
            config.Content.ScheduleTitle = "实时预览测试");
        runner.Equal("有效草稿修改被接受", "实时预览测试", accepted.Content.ScheduleTitle);

        var updated = controller.TryUpdate(
            config => config.Schedule.Weekdays = ["周一", "周一", "周三", "周四", "周五"],
            out var fallback,
            out var error);
        runner.Check("无效草稿不会被接受", !updated);
        runner.Check("无效草稿返回明确提示", !string.IsNullOrWhiteSpace(error));
        runner.Equal(
            "无效草稿保留上一次有效版本",
            "实时预览测试",
            fallback.Content.ScheduleTitle);
        runner.Equal(
            "无效草稿不会污染控制器",
            5,
            controller.Snapshot().Schedule.Weekdays.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static void VerifyPreviewScenarios(StabilityTestRunner runner)
    {
        var source = AppConfig.CreateDefault();
        var originalJson = System.Text.Json.JsonSerializer.Serialize(source, ConfigStore.Options);
        var anchor = new DateTime(2026, 9, 21, 12, 0, 0);

        foreach (var scenario in Enum.GetValues<PreviewScenario>())
        {
            var result = PreviewScenarioFactory.Create(
                source,
                new PreviewOptions(1280, 720, anchor, scenario));
            runner.Check(scenario + " 返回配置", result.Config is not null);
            runner.Check(scenario + " 返回标签", !string.IsNullOrWhiteSpace(result.Label));
            runner.DoesNotThrow(
                scenario + " 课表仍然有效",
                () => ScheduleEngine.ValidateOrThrow(result.Config.Schedule));
        }

        var classPreview = PreviewScenarioFactory.Create(
            source,
            new PreviewOptions(1280, 720, anchor, PreviewScenario.ClassInProgress));
        var activeClass = ScheduleEngine.GetActiveEntry(
            ScheduleEngine.ResolveDay(source.Schedule, classPreview.Moment),
            TimeOnly.FromDateTime(classPreview.Moment));
        runner.Check("上课场景命中课程", activeClass?.Kind == ScheduleCellKind.Class);

        var breakPreview = PreviewScenarioFactory.Create(
            source,
            new PreviewOptions(1280, 720, anchor, PreviewScenario.BreakInProgress));
        var activeBreak = ScheduleEngine.GetActiveEntry(
            ScheduleEngine.ResolveDay(source.Schedule, breakPreview.Moment),
            TimeOnly.FromDateTime(breakPreview.Moment));
        runner.Check("课间场景命中课间", activeBreak?.Kind == ScheduleCellKind.Break);

        var fridayPreview = PreviewScenarioFactory.Create(
            source,
            new PreviewOptions(1280, 720, anchor, PreviewScenario.FridaySpecial));
        runner.Equal("星期五场景落在周五", 4, ScheduleEngine.GetWeekdayIndex(fridayPreview.Moment));
        var fridayEntry = ScheduleEngine.GetActiveEntry(
            ScheduleEngine.ResolveDay(fridayPreview.Config.Schedule, fridayPreview.Moment),
            TimeOnly.FromDateTime(fridayPreview.Moment));
        runner.Check("星期五场景命中特殊时间", fridayEntry?.UsesTimeOverride == true);

        var dayOffPreview = PreviewScenarioFactory.Create(
            source,
            new PreviewOptions(1280, 720, anchor, PreviewScenario.DayOff));
        runner.Check(
            "停课场景解析为停课",
            ScheduleEngine.ResolveDay(dayOffPreview.Config.Schedule, dayOffPreview.Moment).IsDayOff);

        var rescheduledPreview = PreviewScenarioFactory.Create(
            source,
            new PreviewOptions(1280, 720, anchor, PreviewScenario.RescheduledDay));
        runner.Equal<int?>(
            "调课场景套用周一课表",
            0,
            ScheduleEngine.ResolveDay(
                rescheduledPreview.Config.Schedule,
                rescheduledPreview.Moment).DisplayDayIndex);

        var manyEventsPreview = PreviewScenarioFactory.Create(
            source,
            new PreviewOptions(1280, 720, anchor, PreviewScenario.ManyCountdowns));
        runner.Check(
            "多倒计时场景至少包含五个可见事件",
            manyEventsPreview.Config.Events.Count(item => item.Visible) >= 5);

        var finalJson = System.Text.Json.JsonSerializer.Serialize(source, ConfigStore.Options);
        runner.Equal("场景预览不会修改原配置", originalJson, finalJson);
    }
}
