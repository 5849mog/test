using SteadyDesk;

var failures = new List<string>();
var checksRun = 0;

void Check(string name, bool condition)
{
    checksRun++;
    if (!condition)
    {
        failures.Add(name);
    }
}

ResolvedScheduleEntry? ActiveAt(ScheduleConfig schedule, DateTime moment)
{
    var day = ScheduleEngine.ResolveDay(schedule, moment.Date);
    return ScheduleEngine.GetActiveEntry(day, TimeOnly.FromDateTime(moment));
}

var monday = new DateTime(2026, 9, 21);
var friday = new DateTime(2026, 9, 25);
var saturday = new DateTime(2026, 9, 26);

var defaults = ScheduleConfig.CreateDefault();
Check("周一第一节", ActiveAt(defaults, monday.AddHours(8).AddMinutes(20))?.Course == "语文");
Check("周一第六节为空课", ActiveAt(defaults, monday.AddHours(12).AddMinutes(50)) is null);
Check("星期五体育使用特殊时间", ActiveAt(defaults, friday.AddHours(13).AddMinutes(30))?.Course == "体育");
Check("星期五体育开始边界", ActiveAt(defaults, friday.AddHours(13).AddMinutes(20))?.Course == "体育");
Check("星期五体育结束边界", ActiveAt(defaults, friday.AddHours(14)) is null);
Check("星期五课间空档", ActiveAt(defaults, friday.AddHours(14).AddMinutes(10)) is null);
Check("星期五历史", ActiveAt(defaults, friday.AddHours(14).AddMinutes(30))?.Course == "历史");
Check("星期五班会", ActiveAt(defaults, friday.AddHours(17))?.Course == "班会");
Check("星期五第十一节为空课", ActiveAt(defaults, friday.AddHours(17).AddMinutes(45)) is null);
Check("普通周末无课", ActiveAt(defaults, saturday.AddHours(8).AddMinutes(20)) is null);

var makeUpDay = ScheduleConfig.CreateDefault();
makeUpDay.DateOverrides.Add(new ScheduleDateOverrideConfig
{
    Date = saturday,
    Label = "补周一课程",
    BaseDayIndex = 0
});
ScheduleEngine.Normalize(makeUpDay, 4);
Check("周末可套用周一课表", ActiveAt(makeUpDay, saturday.AddHours(8).AddMinutes(20))?.Course == "语文");

var dayOff = ScheduleConfig.CreateDefault();
dayOff.DateOverrides.Add(new ScheduleDateOverrideConfig
{
    Date = friday,
    Label = "停课",
    IsDayOff = true
});
ScheduleEngine.Normalize(dayOff, 4);
Check("具体日期停课优先", ActiveAt(dayOff, friday.AddHours(13).AddMinutes(30)) is null);

var legacy = new ScheduleConfig
{
    Rows =
    [
        new ScheduleRowConfig
        {
            Label = "第六节课",
            Time = "12:40–13:20",
            Start = "12:40",
            End = "13:20",
            Courses = ["—", "—", "—", "—", "13:20–14:00\n体育"]
        }
    ]
};
ScheduleEngine.Normalize(legacy, 3);
var migratedFriday = ScheduleEngine.ResolveTemplateDay(legacy, 4).GetEntry("period-01");
var migratedMonday = ScheduleEngine.ResolveTemplateDay(legacy, 0).GetEntry("period-01");
Check("v3 星期五时间被迁移", migratedFriday?.UsesTimeOverride == true
    && migratedFriday.Start == new TimeOnly(13, 20)
    && migratedFriday.Course == "体育");
Check("v3 破折号迁移为空课", migratedMonday?.Kind == ScheduleCellKind.Empty);
Check("v3 Rows 迁移后清除", legacy.Rows is null);

var overlap = ScheduleConfig.CreateDefault();
var mondaySchedule = overlap.Days.Single(day => day.DayIndex == 0);
var secondPeriod = mondaySchedule.Cells.Single(cell => cell.PeriodId == "period-02");
secondPeriod.StartOverride = "08:20";
secondPeriod.EndOverride = "08:50";
Check("重叠时间会被验证器发现", ScheduleEngine.Validate(overlap).Any(error => error.Contains("时间重叠")));

var validationErrors = ScheduleEngine.Validate(defaults);
Check("默认课表通过验证", validationErrors.Count == 0);

if (failures.Count > 0)
{
    Console.Error.WriteLine("Schedule checks failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine("- " + failure);
    }

    return 1;
}

Console.WriteLine($"Schedule engine checks passed: {checksRun}");
return 0;
