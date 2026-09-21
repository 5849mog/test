namespace SteadyDesk;

internal sealed record PreviewScenarioResult(
    AppConfig Config,
    DateTime Moment,
    string Label);

internal static class PreviewScenarioFactory
{
    public static PreviewScenarioResult Create(AppConfig source, PreviewOptions options)
    {
        var config = source.Clone();
        var moment = options.Moment;
        var label = "当前状态";

        switch (options.Scenario)
        {
            case PreviewScenario.ClassInProgress:
                moment = FindMoment(config, moment.Date, ScheduleCellKind.Class, null, false);
                label = "上课状态";
                break;
            case PreviewScenario.BreakInProgress:
                moment = FindMoment(config, moment.Date, ScheduleCellKind.Break, null, false);
                label = "课间状态";
                break;
            case PreviewScenario.FridaySpecial:
                moment = FindMoment(config, moment.Date, ScheduleCellKind.Class, 4, true);
                label = "星期五特殊作息";
                break;
            case PreviewScenario.DayOff:
                moment = CreateDayOff(config, moment);
                label = "停课日";
                break;
            case PreviewScenario.RescheduledDay:
                moment = CreateRescheduledDay(config, moment);
                label = "调课日";
                break;
            case PreviewScenario.ManyCountdowns:
                AddPreviewEvents(config, moment.Date);
                label = "多倒计时";
                break;
        }

        config.Normalize();
        ScheduleEngine.ValidateOrThrow(config.Schedule);
        return new PreviewScenarioResult(config, moment, label);
    }

    private static DateTime FindMoment(
        AppConfig config,
        DateTime anchor,
        ScheduleCellKind kind,
        int? requiredDayIndex,
        bool requireTimeOverride)
    {
        var dayIndexes = requiredDayIndex.HasValue
            ? new[] { requiredDayIndex.Value }
            : Enumerable.Range(0, 5).ToArray();

        foreach (var dayIndex in dayIndexes)
        {
            var day = ScheduleEngine.ResolveTemplateDay(config.Schedule, dayIndex);
            var entry = day.Entries.FirstOrDefault(item =>
                item.Kind == kind
                && item.Start.HasValue
                && item.End.HasValue
                && (!requireTimeOverride || item.UsesTimeOverride));
            if (entry?.Start is not TimeOnly start || entry.End is not TimeOnly end)
            {
                continue;
            }

            var date = DateForDayIndex(anchor, dayIndex);
            config.Schedule.DateOverrides.RemoveAll(item => item.Date.Date == date.Date);
            return date.Add(Middle(start, end));
        }

        var fallbackDayIndex = requiredDayIndex ?? 0;
        var fallbackDate = DateForDayIndex(anchor, fallbackDayIndex);
        config.Schedule.DateOverrides.RemoveAll(item => item.Date.Date == fallbackDate.Date);
        return fallbackDate.AddHours(kind == ScheduleCellKind.Break ? 9 : 10).AddMinutes(20);
    }

    private static DateTime CreateDayOff(AppConfig config, DateTime moment)
    {
        var date = EnsureWeekday(moment.Date);
        config.Schedule.DateOverrides.RemoveAll(item => item.Date.Date == date);
        config.Schedule.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = date,
            Label = "预览 · 临时停课",
            IsDayOff = true
        });
        return date.AddHours(10).AddMinutes(20);
    }

    private static DateTime CreateRescheduledDay(AppConfig config, DateTime moment)
    {
        var date = DateForDayIndex(moment.Date, 1);
        const int sourceDayIndex = 0;
        config.Schedule.DateOverrides.RemoveAll(item => item.Date.Date == date);
        config.Schedule.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = date,
            Label = "预览 · 按周一课表上课",
            BaseDayIndex = sourceDayIndex,
            IsDayOff = false
        });

        var sourceDay = ScheduleEngine.ResolveTemplateDay(config.Schedule, sourceDayIndex);
        var entry = sourceDay.Entries.FirstOrDefault(item =>
            item.Kind == ScheduleCellKind.Class
            && item.Start.HasValue
            && item.End.HasValue);
        return entry is null
            ? date.AddHours(10).AddMinutes(20)
            : date.Add(Middle(entry.Start!.Value, entry.End!.Value));
    }

    private static void AddPreviewEvents(AppConfig config, DateTime today)
    {
        var visible = config.Events.Count(item => item.Visible);
        var titles = new[] { "阶段测验", "英语展示", "体育测试", "期中考试", "目标日期" };
        var colors = new[] { "#E5CB97", "#B69A68", "#C7A8A0", "#D8C39B", "#A97A62" };

        for (var index = visible; index < 5; index++)
        {
            config.Events.Add(new CountdownEventConfig
            {
                Id = "preview-event-" + index,
                Title = titles[index],
                Date = today.AddDays(7 + index * 11),
                Visible = true,
                ShowProgress = true,
                Color = colors[index]
            });
        }
    }

    private static DateTime DateForDayIndex(DateTime anchor, int dayIndex)
    {
        var target = (DayOfWeek)(dayIndex + 1);
        var delta = ((int)target - (int)anchor.DayOfWeek + 7) % 7;
        return anchor.Date.AddDays(delta);
    }

    private static DateTime EnsureWeekday(DateTime date)
    {
        return date.DayOfWeek switch
        {
            DayOfWeek.Saturday => date.AddDays(2),
            DayOfWeek.Sunday => date.AddDays(1),
            _ => date
        };
    }

    private static TimeSpan Middle(TimeOnly start, TimeOnly end)
    {
        var startTicks = start.ToTimeSpan().Ticks;
        var endTicks = end.ToTimeSpan().Ticks;
        return TimeSpan.FromTicks(startTicks + (endTicks - startTicks) / 2);
    }
}
