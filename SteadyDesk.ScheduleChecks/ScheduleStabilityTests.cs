using System.Text.Json;
using SteadyDesk;

namespace SteadyDesk.ScheduleChecks;

internal static class ScheduleStabilityTests
{
    private static readonly DateTime Monday = new(2026, 9, 21);
    private static readonly DateTime Friday = new(2026, 9, 25);
    private static readonly DateTime Saturday = new(2026, 9, 26);
    private static readonly DateTime Sunday = new(2026, 9, 27);

    public static void Run(StabilityTestRunner runner)
    {
        runner.Suite("默认课表与全部时间边界", () => VerifyDefaultSchedule(runner));
        runner.Suite("特殊日期优先级", () => VerifyDateOverrides(runner));
        runner.Suite("时间解析与验证器", () => VerifyValidation(runner));
        runner.Suite("Schema v3 迁移完整性", () => VerifyLegacyMigration(runner));
        runner.Suite("标准化幂等与去噪", () => VerifyNormalization(runner));
        runner.Suite("固定种子随机压力测试", () => VerifyRandomStress(runner));
    }

    private static void VerifyDefaultSchedule(StabilityTestRunner runner)
    {
        var schedule = ScheduleConfig.CreateDefault();

        runner.Equal("周一第一节", "语文", ActiveAt(schedule, Monday.AddHours(8).AddMinutes(20))?.Course);
        runner.Check("周一第六节为空课", ActiveAt(schedule, Monday.AddHours(12).AddMinutes(50)) is null);
        runner.Check("星期五特殊节次前保持空档", ActiveAt(schedule, Friday.AddHours(12).AddMinutes(50)) is null);
        runner.Equal("星期五体育使用特殊时间", "体育", ActiveAt(schedule, Friday.AddHours(13).AddMinutes(30))?.Course);
        runner.Equal("星期五体育开始边界", "体育", ActiveAt(schedule, Friday.AddHours(13).AddMinutes(20))?.Course);
        runner.Check("星期五体育结束边界不包含", ActiveAt(schedule, Friday.AddHours(14)) is null);
        runner.Check("星期五课间空档", ActiveAt(schedule, Friday.AddHours(14).AddMinutes(10)) is null);
        runner.Equal("星期五历史", "历史", ActiveAt(schedule, Friday.AddHours(14).AddMinutes(30))?.Course);
        runner.Equal("星期五班会", "班会", ActiveAt(schedule, Friday.AddHours(17))?.Course);
        runner.Check("星期五第十一节为空课", ActiveAt(schedule, Friday.AddHours(17).AddMinutes(45)) is null);
        runner.Check("普通周六无课", ActiveAt(schedule, Saturday.AddHours(8).AddMinutes(20)) is null);
        runner.Check("普通周日无课", ActiveAt(schedule, Sunday.AddHours(8).AddMinutes(20)) is null);

        for (var dayIndex = 0; dayIndex < 5; dayIndex++)
        {
            var date = Monday.AddDays(dayIndex);
            var day = ScheduleEngine.ResolveDay(schedule, date);
            runner.Equal($"工作日 {dayIndex} 映射正确", dayIndex, day.DisplayDayIndex);

            foreach (var entry in day.Entries.Where(item => item.Start.HasValue && item.End.HasValue))
            {
                var atStart = ScheduleEngine.GetActiveEntry(day, entry.Start!.Value);
                if (entry.Kind == ScheduleCellKind.Empty)
                {
                    runner.Check(
                        $"{date:yyyy-MM-dd} {entry.PeriodId} 空课在开始时刻不激活",
                        atStart?.PeriodId != entry.PeriodId);
                    continue;
                }

                runner.Equal(
                    $"{date:yyyy-MM-dd} {entry.PeriodId} 开始边界包含",
                    entry.PeriodId,
                    atStart?.PeriodId);
                runner.Equal(
                    $"{date:yyyy-MM-dd} {entry.PeriodId} 结束前仍激活",
                    entry.PeriodId,
                    ScheduleEngine.GetActiveEntry(day, entry.End!.Value.AddMinutes(-1))?.PeriodId);
                runner.Check(
                    $"{date:yyyy-MM-dd} {entry.PeriodId} 结束边界不包含",
                    ScheduleEngine.GetActiveEntry(day, entry.End.Value)?.PeriodId != entry.PeriodId);
            }
        }

        runner.Equal("格式化空课", "—", ScheduleEngine.FormatCell(null));
        runner.Check(
            "特殊时间格式会显示时间",
            ScheduleEngine.FormatCell(ScheduleEngine.ResolveDay(schedule, Friday).GetEntry("period-06"))
                .StartsWith("13:20–14:00", StringComparison.Ordinal));
        runner.Equal("周末索引", -1, ScheduleEngine.GetWeekdayIndex(Saturday));
        runner.Equal("周一索引", 0, ScheduleEngine.GetWeekdayIndex(Monday));
    }

    private static void VerifyDateOverrides(StabilityTestRunner runner)
    {
        var makeUpDay = ScheduleConfig.CreateDefault();
        makeUpDay.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = Saturday,
            Label = "补周一课程",
            BaseDayIndex = 0
        });
        ScheduleEngine.Normalize(makeUpDay, 4);
        var resolvedMakeUp = ScheduleEngine.ResolveDay(makeUpDay, Saturday);
        runner.Equal("周末可套用周一课表", "语文", ActiveAt(makeUpDay, Saturday.AddHours(8).AddMinutes(20))?.Course);
        runner.Equal("补课日显示基础星期", 0, resolvedMakeUp.DisplayDayIndex);
        runner.Equal("补课日标签保留", "补周一课程", resolvedMakeUp.Label);

        var dayOff = ScheduleConfig.CreateDefault();
        dayOff.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = Friday.AddHours(12),
            Label = "停课",
            IsDayOff = true,
            BaseDayIndex = 0,
            Cells =
            [
                new ScheduleCellConfig
                {
                    PeriodId = "period-01",
                    Course = "不应出现",
                    Kind = ScheduleCellKind.Class
                }
            ]
        });
        ScheduleEngine.Normalize(dayOff, 4);
        var resolvedDayOff = ScheduleEngine.ResolveDay(dayOff, Friday);
        runner.Check("具体日期停课优先", ActiveAt(dayOff, Friday.AddHours(13).AddMinutes(30)) is null);
        runner.Check("停课日没有条目", resolvedDayOff.IsDayOff && resolvedDayOff.Entries.Count == 0);
        runner.Equal("日期会归一到零点", Friday, dayOff.DateOverrides.Single().Date);

        var dateCellOverride = ScheduleConfig.CreateDefault();
        dateCellOverride.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = Friday,
            Label = "临时调课",
            BaseDayIndex = 4,
            Cells =
            [
                new ScheduleCellConfig
                {
                    PeriodId = "period-06",
                    Course = "临时数学",
                    Kind = ScheduleCellKind.Class,
                    StartOverride = "13:25",
                    EndOverride = "14:05"
                }
            ]
        });
        ScheduleEngine.Normalize(dateCellOverride, 4);
        runner.Equal(
            "具体日期课程覆盖基础模板",
            "临时数学",
            ActiveAt(dateCellOverride, Friday.AddHours(13).AddMinutes(30))?.Course);
        runner.Check(
            "日期覆盖后的原开始时刻不再激活",
            ActiveAt(dateCellOverride, Friday.AddHours(13).AddMinutes(20)) is null);

        var dateEmptyOverride = ScheduleConfig.CreateDefault();
        dateEmptyOverride.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = Friday,
            BaseDayIndex = 4,
            Cells =
            [
                new ScheduleCellConfig
                {
                    PeriodId = "period-06",
                    Kind = ScheduleCellKind.Empty
                }
            ]
        });
        ScheduleEngine.Normalize(dateEmptyOverride, 4);
        runner.Check(
            "具体日期空课覆盖基础模板",
            ActiveAt(dateEmptyOverride, Friday.AddHours(13).AddMinutes(30)) is null);

        var standaloneSundayClass = ScheduleConfig.CreateDefault();
        standaloneSundayClass.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = Sunday,
            Label = "周日临时课程",
            Cells =
            [
                new ScheduleCellConfig
                {
                    PeriodId = "period-01",
                    Course = "临时答疑",
                    Kind = ScheduleCellKind.Class
                }
            ]
        });
        ScheduleEngine.Normalize(standaloneSundayClass, 4);
        runner.Equal(
            "无基础模板的周日也可逐节加课",
            "临时答疑",
            ActiveAt(standaloneSundayClass, Sunday.AddHours(8).AddMinutes(20))?.Course);
        runner.Check(
            "未覆盖的周日节次保持空课",
            ActiveAt(standaloneSundayClass, Sunday.AddHours(8).AddMinutes(58)) is null);
    }

    private static void VerifyValidation(StabilityTestRunner runner)
    {
        runner.Check("解析 HH:mm", ScheduleEngine.TryParseTime("08:05", out var hhmm)
            && hhmm == new TimeOnly(8, 5));
        runner.Check("解析 H:mm", ScheduleEngine.TryParseTime("8:05", out var hmm)
            && hmm == new TimeOnly(8, 5));
        runner.Check("兼容 HH:mm:ss", ScheduleEngine.TryParseTime("08:05:30", out var seconds)
            && seconds == new TimeOnly(8, 5, 30));
        runner.Check("拒绝 24:00", !ScheduleEngine.TryParseTime("24:00:00", out _));
        runner.Check("拒绝负时间", !ScheduleEngine.TryParseTime("-01:00", out _));
        runner.Check("拒绝空时间", !ScheduleEngine.TryParseTime(null, out _));
        runner.Check("合法时间范围", ScheduleEngine.TryParseTimeRange("08:00", "08:40", out _, out _));
        runner.Check("拒绝相等时间", !ScheduleEngine.TryParseTimeRange("08:00", "08:00", out _, out _));
        runner.Check("拒绝倒序时间", !ScheduleEngine.TryParseTimeRange("09:00", "08:00", out _, out _));

        var defaults = ScheduleConfig.CreateDefault();
        runner.Equal("默认课表通过验证", 0, ScheduleEngine.Validate(defaults).Count);
        runner.DoesNotThrow("默认课表 ValidateOrThrow 不抛出", () => ScheduleEngine.ValidateOrThrow(defaults));

        var duplicateIds = CloneSchedule(defaults);
        duplicateIds.Periods[1].Id = duplicateIds.Periods[0].Id;
        runner.Check(
            "重复课节标识会被发现",
            ScheduleEngine.Validate(duplicateIds).Any(error => error.Contains("标识", StringComparison.Ordinal)));

        var invalidPeriod = CloneSchedule(defaults);
        invalidPeriod.Periods[0].End = invalidPeriod.Periods[0].Start;
        runner.Check(
            "无效默认时间会被发现",
            ScheduleEngine.Validate(invalidPeriod).Any(error => error.Contains("默认时间无效", StringComparison.Ordinal)));

        var partialOverride = CloneSchedule(defaults);
        partialOverride.Days[0].Cells[0].StartOverride = "07:30";
        partialOverride.Days[0].Cells[0].EndOverride = null;
        runner.Check(
            "只填一侧特殊时间会被发现",
            ScheduleEngine.Validate(partialOverride).Any(error => error.Contains("同时填写", StringComparison.Ordinal)));

        var invalidOverride = CloneSchedule(defaults);
        invalidOverride.Days[0].Cells[0].StartOverride = "09:00";
        invalidOverride.Days[0].Cells[0].EndOverride = "08:00";
        runner.Check(
            "倒序特殊时间会被发现",
            ScheduleEngine.Validate(invalidOverride).Any(error => error.Contains("特殊时间无效", StringComparison.Ordinal)));

        var duplicateDates = CloneSchedule(defaults);
        duplicateDates.DateOverrides =
        [
            new ScheduleDateOverrideConfig { Date = Friday },
            new ScheduleDateOverrideConfig { Date = Friday.AddHours(8) }
        ];
        runner.Check(
            "重复日期规则会被发现",
            ScheduleEngine.Validate(duplicateDates).Any(error => error.Contains("重复", StringComparison.Ordinal)));

        var overlap = CloneSchedule(defaults);
        var mondaySchedule = overlap.Days.Single(day => day.DayIndex == 0);
        var secondPeriod = mondaySchedule.Cells.Single(cell => cell.PeriodId == "period-02");
        secondPeriod.StartOverride = "08:20";
        secondPeriod.EndOverride = "08:50";
        runner.Check(
            "重叠时间会被验证器发现",
            ScheduleEngine.Validate(overlap).Any(error => error.Contains("时间重叠", StringComparison.Ordinal)));
        runner.Throws<InvalidOperationException>(
            "ValidateOrThrow 会阻止无效课表",
            () => ScheduleEngine.ValidateOrThrow(overlap));
    }

    private static void VerifyLegacyMigration(StabilityTestRunner runner)
    {
        var legacy = new ScheduleConfig
        {
            Rows =
            [
                new ScheduleRowConfig
                {
                    Label = "第一节课",
                    Time = "08:00:00–08:40:00\n晨读",
                    Start = "08:00:00",
                    End = "08:40:00",
                    Courses = ["—", "英语", "数学", "物理", "13:20:00–14:00:00\n体育"]
                },
                new ScheduleRowConfig
                {
                    Label = "大课间",
                    Time = "09:35:00–10:05:00",
                    Start = "09:35:00",
                    End = "10:05:00",
                    IsBreak = true,
                    Courses = ["伸展"]
                },
                new ScheduleRowConfig
                {
                    Label = "第三节课",
                    Time = "10:05:00–10:45:00",
                    Start = "10:05:00",
                    End = "10:45:00",
                    Courses = ["数学"]
                }
            ]
        };

        ScheduleEngine.Normalize(legacy, 3);

        var migratedPeriod = legacy.Periods[0];
        var migratedFriday = ScheduleEngine.ResolveTemplateDay(legacy, 4).GetEntry("period-01");
        var migratedMonday = ScheduleEngine.ResolveTemplateDay(legacy, 0).GetEntry("period-01");
        runner.Check(
            "v3 带秒默认时间归一化",
            migratedPeriod.Start == "08:00" && migratedPeriod.End == "08:40");
        runner.Equal("v3 行备注迁移", "晨读", migratedPeriod.Note);
        runner.Check(
            "v3 星期五时间被迁移",
            migratedFriday?.UsesTimeOverride == true
            && migratedFriday.Start == new TimeOnly(13, 20)
            && migratedFriday.End == new TimeOnly(14, 0)
            && migratedFriday.Course == "体育");
        runner.Equal("v3 破折号迁移为空课", ScheduleCellKind.Empty, migratedMonday?.Kind);
        runner.Check("v3 Rows 迁移后清除", legacy.Rows is null);

        for (var dayIndex = 0; dayIndex < 5; dayIndex++)
        {
            var breakEntry = ScheduleEngine.ResolveTemplateDay(legacy, dayIndex).GetEntry("period-02");
            runner.Check(
                $"v3 课间复制到星期 {dayIndex + 1}",
                breakEntry?.Kind == ScheduleCellKind.Break && breakEntry.Course == "伸展");
        }

        runner.Equal(
            "v3 短课程数组保留已有课程",
            "数学",
            ScheduleEngine.ResolveTemplateDay(legacy, 0).GetEntry("period-03")?.Course);
        runner.Equal(
            "v3 短课程数组缺项为空课",
            ScheduleCellKind.Empty,
            ScheduleEngine.ResolveTemplateDay(legacy, 1).GetEntry("period-03")?.Kind);
        runner.Equal("迁移结果通过验证", 0, ScheduleEngine.Validate(legacy).Count);
    }

    private static void VerifyNormalization(StabilityTestRunner runner)
    {
        var schedule = ScheduleConfig.CreateDefault();
        schedule.Weekdays = ["  周一  ", "", "周三", "周四", "周五"];
        schedule.Days.Add(new ScheduleDayConfig
        {
            DayIndex = 0,
            Cells =
            [
                new ScheduleCellConfig
                {
                    PeriodId = "period-01",
                    Course = "最后版本",
                    Kind = ScheduleCellKind.Class
                },
                new ScheduleCellConfig
                {
                    PeriodId = "不存在",
                    Course = "应被移除",
                    Kind = ScheduleCellKind.Class
                }
            ]
        });
        schedule.DateOverrides.Add(new ScheduleDateOverrideConfig
        {
            Date = Friday.AddHours(21),
            BaseDayIndex = 99,
            Cells =
            [
                new ScheduleCellConfig
                {
                    PeriodId = "period-01",
                    Course = "  临时语文  ",
                    Kind = ScheduleCellKind.Class
                }
            ]
        });

        ScheduleEngine.Normalize(schedule, 4);
        runner.Equal("星期名称去除首尾空白", "周一", schedule.Weekdays[0]);
        runner.Equal("空星期名称补默认值", "周二", schedule.Weekdays[1]);
        runner.Equal(
            "重复星期模板最后课程格生效",
            "最后版本",
            schedule.Days.Single(day => day.DayIndex == 0)
                .Cells.Single(cell => cell.PeriodId == "period-01").Course);
        runner.Check(
            "未知课节课程格被移除",
            schedule.Days.SelectMany(day => day.Cells).All(cell => cell.PeriodId != "不存在"));
        runner.Check("非法基础星期被清空", schedule.DateOverrides.Single().BaseDayIndex is null);
        runner.Equal("日期覆盖课程去除空白", "临时语文", schedule.DateOverrides.Single().Cells.Single().Course);

        var firstJson = JsonSerializer.Serialize(schedule, ConfigStore.Options);
        ScheduleEngine.Normalize(schedule, 4);
        var secondJson = JsonSerializer.Serialize(schedule, ConfigStore.Options);
        runner.Equal("课表标准化可重复执行且结果稳定", firstJson, secondJson);
        runner.Equal("标准化后仍通过验证", 0, ScheduleEngine.Validate(schedule).Count);
    }

    private static void VerifyRandomStress(StabilityTestRunner runner)
    {
        const int scenarioCount = 200;
        var random = new Random(20260920);

        for (var scenario = 0; scenario < scenarioCount; scenario++)
        {
            var schedule = CloneSchedule(ScheduleConfig.CreateDefault());
            var selectedDay = schedule.Days[random.Next(schedule.Days.Count)];
            var selectedCell = selectedDay.Cells[random.Next(selectedDay.Cells.Count)];
            if (random.Next(3) == 0)
            {
                selectedCell.Kind = ScheduleCellKind.Empty;
                selectedCell.Course = string.Empty;
            }
            else
            {
                selectedCell.Kind = ScheduleCellKind.Class;
                selectedCell.Course = "压力课程-" + scenario;
            }

            var targetDate = new DateTime(2026, 10, 1).AddDays(random.Next(180));
            var rule = new ScheduleDateOverrideConfig
            {
                Date = targetDate,
                Label = "场景-" + scenario
            };

            switch (random.Next(4))
            {
                case 0:
                    rule.IsDayOff = true;
                    break;
                case 1:
                    rule.BaseDayIndex = random.Next(5);
                    break;
                case 2:
                case 3:
                    rule.BaseDayIndex = random.Next(5);
                    var period = schedule.Periods[random.Next(schedule.Periods.Count)];
                    rule.Cells.Add(new ScheduleCellConfig
                    {
                        PeriodId = period.Id,
                        Course = random.Next(2) == 0 ? string.Empty : "临时课程-" + scenario,
                        Kind = random.Next(2) == 0 ? ScheduleCellKind.Empty : ScheduleCellKind.Class
                    });
                    break;
            }

            schedule.DateOverrides.Add(rule);
            ScheduleEngine.Normalize(schedule, 4);
            var validationErrors = ScheduleEngine.Validate(schedule);

            var roundTrip = CloneSchedule(schedule);
            ScheduleEngine.Normalize(roundTrip, 4);
            var roundTripErrors = ScheduleEngine.Validate(roundTrip);
            runner.Check(
                $"随机场景 {scenario} 验证结果序列化前后一致",
                validationErrors.SequenceEqual(roundTripErrors, StringComparer.Ordinal));
            if (validationErrors.Count == 0)
            {
                runner.DoesNotThrow(
                    $"随机合法场景 {scenario} 可通过 ValidateOrThrow",
                    () => ScheduleEngine.ValidateOrThrow(roundTrip));
            }
            else
            {
                runner.Throws<InvalidOperationException>(
                    $"随机非法场景 {scenario} 会被 ValidateOrThrow 拒绝",
                    () => ScheduleEngine.ValidateOrThrow(roundTrip));
            }

            var mondayOffset = ((int)targetDate.DayOfWeek + 6) % 7;
            var sweepStart = targetDate.AddDays(-mondayOffset);
            for (var dayOffset = 0; dayOffset < 7; dayOffset++)
            {
                var date = sweepStart.AddDays(dayOffset);
                var resolved = ScheduleEngine.ResolveDay(roundTrip, date);
                var secondResolution = ScheduleEngine.ResolveDay(roundTrip, date);
                runner.Check(
                    $"随机场景 {scenario} 日期 {dayOffset} 解析稳定",
                    resolved.Entries.Select(EntrySignature)
                        .SequenceEqual(secondResolution.Entries.Select(EntrySignature), StringComparer.Ordinal));

                for (var halfHour = 0; halfHour < 48; halfHour++)
                {
                    var time = new TimeOnly(halfHour / 2, halfHour % 2 * 30);
                    var active = ScheduleEngine.GetActiveEntry(resolved, time);
                    var valid = active is null
                        || (active.Kind != ScheduleCellKind.Empty
                            && active.Start.HasValue
                            && active.End.HasValue
                            && time >= active.Start.Value
                            && time < active.End.Value
                            && !string.IsNullOrWhiteSpace(active.Course));
                    runner.Check(
                        $"随机场景 {scenario} 日期 {dayOffset} 时间 {time:HH\\:mm}",
                        valid);
                }
            }
        }
    }

    private static string EntrySignature(ResolvedScheduleEntry entry)
    {
        return string.Join(
            "|",
            entry.PeriodId,
            entry.Course,
            entry.Kind,
            entry.Start?.ToString("HH:mm") ?? string.Empty,
            entry.End?.ToString("HH:mm") ?? string.Empty,
            entry.UsesTimeOverride);
    }

    private static ResolvedScheduleEntry? ActiveAt(ScheduleConfig schedule, DateTime moment)
    {
        var day = ScheduleEngine.ResolveDay(schedule, moment.Date);
        return ScheduleEngine.GetActiveEntry(day, TimeOnly.FromDateTime(moment));
    }

    private static ScheduleConfig CloneSchedule(ScheduleConfig schedule)
    {
        return JsonSerializer.Deserialize<ScheduleConfig>(
            JsonSerializer.Serialize(schedule, ConfigStore.Options),
            ConfigStore.Options) ?? throw new InvalidOperationException("Unable to clone schedule.");
    }
}
