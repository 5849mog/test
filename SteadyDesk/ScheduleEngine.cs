using System.Globalization;

namespace SteadyDesk;

internal sealed record ResolvedScheduleEntry(
    string PeriodId,
    string Label,
    string? Note,
    string Course,
    ScheduleCellKind Kind,
    TimeOnly? Start,
    TimeOnly? End,
    bool UsesTimeOverride);

internal sealed class ResolvedScheduleDay
{
    private readonly Dictionary<string, ResolvedScheduleEntry> _entriesByPeriod;

    public ResolvedScheduleDay(
        DateTime date,
        int? displayDayIndex,
        bool isDayOff,
        string? label,
        IReadOnlyList<ResolvedScheduleEntry> entries)
    {
        Date = date.Date;
        DisplayDayIndex = displayDayIndex;
        IsDayOff = isDayOff;
        Label = label;
        Entries = entries;
        _entriesByPeriod = entries
            .GroupBy(entry => entry.PeriodId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public DateTime Date { get; }
    public int? DisplayDayIndex { get; }
    public bool IsDayOff { get; }
    public string? Label { get; }
    public IReadOnlyList<ResolvedScheduleEntry> Entries { get; }

    public ResolvedScheduleEntry? GetEntry(string periodId)
    {
        return _entriesByPeriod.TryGetValue(periodId, out var entry) ? entry : null;
    }
}

internal static class ScheduleEngine
{
    private static readonly string[] TimeFormats = ["HH:mm", "H:mm"];

    public static bool HasAnyScheduleData(ScheduleConfig schedule)
    {
        return (schedule.Periods?.Count ?? 0) > 0
            || (schedule.Days?.Count ?? 0) > 0
            || (schedule.Rows?.Count ?? 0) > 0;
    }

    public static void Normalize(ScheduleConfig schedule, int sourceSchemaVersion)
    {
        schedule.Weekdays ??= ["周一", "周二", "周三", "周四", "周五"];
        if (schedule.Weekdays.Count != 5)
        {
            schedule.Weekdays = ["周一", "周二", "周三", "周四", "周五"];
        }
        else
        {
            schedule.Weekdays = schedule.Weekdays
                .Select((name, index) => string.IsNullOrWhiteSpace(name) ? "周" + ChineseNumber(index + 1) : name.Trim())
                .ToList();
        }

        schedule.Periods ??= [];
        schedule.Days ??= [];
        schedule.DateOverrides ??= [];

        if (schedule.Rows is not null
            && (sourceSchemaVersion < 4 || schedule.Periods.Count == 0))
        {
            MigrateLegacyRows(schedule, schedule.Rows);
        }

        schedule.Rows = null;
        schedule.Periods = schedule.Periods
            .Where(period => period is not null)
            .ToList();

        for (var index = 0; index < schedule.Periods.Count; index++)
        {
            var period = schedule.Periods[index];
            period.Id = string.IsNullOrWhiteSpace(period.Id)
                ? "period-" + (index + 1).ToString("00")
                : period.Id.Trim();
            period.Label = string.IsNullOrWhiteSpace(period.Label)
                ? "第 " + (index + 1) + " 项"
                : period.Label.Trim();
            period.Start = period.Start?.Trim() ?? string.Empty;
            period.End = period.End?.Trim() ?? string.Empty;
            period.Note = string.IsNullOrWhiteSpace(period.Note) ? null : period.Note.Trim();
            if (!Enum.IsDefined(period.Kind))
            {
                period.Kind = ScheduleCellKind.Class;
            }
        }

        var validPeriodIds = schedule.Periods
            .Select(period => period.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceDays = schedule.Days
            .Where(day => day is not null)
            .ToList();
        var normalizedDays = new List<ScheduleDayConfig>();

        for (var dayIndex = 0; dayIndex < 5; dayIndex++)
        {
            var cells = sourceDays
                .Where(day => day.DayIndex == dayIndex)
                .SelectMany(day => day.Cells ?? [])
                .Where(cell => cell is not null && validPeriodIds.Contains(cell.PeriodId ?? string.Empty))
                .Select(CloneCell)
                .GroupBy(cell => cell.PeriodId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();

            foreach (var period in schedule.Periods)
            {
                if (cells.All(cell => !cell.PeriodId.Equals(period.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    cells.Add(new ScheduleCellConfig
                    {
                        PeriodId = period.Id,
                        Kind = ScheduleCellKind.Empty
                    });
                }
            }

            foreach (var cell in cells)
            {
                NormalizeCell(cell);
            }

            normalizedDays.Add(new ScheduleDayConfig
            {
                DayIndex = dayIndex,
                Cells = cells
            });
        }

        schedule.Days = normalizedDays;
        schedule.DateOverrides = schedule.DateOverrides
            .Where(item => item is not null)
            .Select(item =>
            {
                item.Date = item.Date.Date;
                item.Label = item.Label?.Trim() ?? string.Empty;
                if (item.BaseDayIndex is < 0 or > 4)
                {
                    item.BaseDayIndex = null;
                }

                item.Cells ??= [];
                item.Cells = item.Cells
                    .Where(cell => cell is not null && validPeriodIds.Contains(cell.PeriodId ?? string.Empty))
                    .Select(CloneCell)
                    .GroupBy(cell => cell.PeriodId, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Last())
                    .ToList();
                foreach (var cell in item.Cells)
                {
                    NormalizeCell(cell);
                }

                return item;
            })
            .ToList();
    }

    public static ResolvedScheduleDay ResolveTemplateDay(ScheduleConfig schedule, int dayIndex)
    {
        if (dayIndex is < 0 or > 4)
        {
            return new ResolvedScheduleDay(DateTime.MinValue, null, false, null, []);
        }

        var day = schedule.Days.FirstOrDefault(item => item.DayIndex == dayIndex);
        var cells = (day?.Cells ?? [])
            .GroupBy(cell => cell.PeriodId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        return BuildResolvedDay(schedule, DateTime.MinValue, dayIndex, false, null, cells);
    }

    public static ResolvedScheduleDay ResolveDay(ScheduleConfig schedule, DateTime date)
    {
        date = date.Date;
        var dateOverride = schedule.DateOverrides
            .LastOrDefault(item => item.Date.Date == date);

        if (dateOverride?.IsDayOff == true)
        {
            return new ResolvedScheduleDay(date, null, true, dateOverride.Label, []);
        }

        var calendarDayIndex = GetWeekdayIndex(date);
        var baseDayIndex = dateOverride?.BaseDayIndex ?? calendarDayIndex;
        var cells = new Dictionary<string, ScheduleCellConfig>(StringComparer.OrdinalIgnoreCase);

        if (baseDayIndex is >= 0 and <= 4)
        {
            var baseDay = schedule.Days.FirstOrDefault(item => item.DayIndex == baseDayIndex);
            foreach (var cell in baseDay?.Cells ?? [])
            {
                cells[cell.PeriodId] = cell;
            }
        }

        if (dateOverride is not null)
        {
            foreach (var cell in dateOverride.Cells)
            {
                cells[cell.PeriodId] = cell;
            }
        }

        if (baseDayIndex is < 0 or > 4 && cells.Count == 0)
        {
            return new ResolvedScheduleDay(date, null, false, dateOverride?.Label, []);
        }

        return BuildResolvedDay(
            schedule,
            date,
            baseDayIndex is >= 0 and <= 4 ? baseDayIndex : null,
            false,
            dateOverride?.Label,
            cells);
    }

    public static ResolvedScheduleEntry? GetActiveEntry(ResolvedScheduleDay day, TimeOnly time)
    {
        return day.Entries.FirstOrDefault(entry =>
            entry.Kind != ScheduleCellKind.Empty
            && entry.Start.HasValue
            && entry.End.HasValue
            && time >= entry.Start.Value
            && time < entry.End.Value);
    }

    public static string FormatCell(ResolvedScheduleEntry? entry)
    {
        if (entry is null || entry.Kind == ScheduleCellKind.Empty)
        {
            return "—";
        }

        var course = string.IsNullOrWhiteSpace(entry.Course)
            ? entry.Kind == ScheduleCellKind.Break ? "课间" : "—"
            : entry.Course;

        if (!entry.UsesTimeOverride || !entry.Start.HasValue || !entry.End.HasValue)
        {
            return course;
        }

        return entry.Start.Value.ToString("HH:mm", CultureInfo.InvariantCulture)
            + "–"
            + entry.End.Value.ToString("HH:mm", CultureInfo.InvariantCulture)
            + "\n"
            + course;
    }

    public static string FormatPeriodTime(SchedulePeriodConfig period)
    {
        var range = TryParseTimeRange(period.Start, period.End, out var start, out var end)
            ? start.ToString("HH:mm", CultureInfo.InvariantCulture)
                + "–"
                + end.ToString("HH:mm", CultureInfo.InvariantCulture)
            : (period.Start + "–" + period.End).Trim('–');

        return string.IsNullOrWhiteSpace(period.Note)
            ? range
            : range + "\n" + period.Note;
    }

    public static IReadOnlyList<string> Validate(ScheduleConfig schedule)
    {
        var errors = new List<string>();
        var duplicatePeriodIds = schedule.Periods
            .GroupBy(period => period.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicatePeriodIds.Count > 0)
        {
            errors.Add("课节标识不能重复或为空。");
        }

        foreach (var period in schedule.Periods)
        {
            if (string.IsNullOrWhiteSpace(period.Label))
            {
                errors.Add("课节名称不能为空。");
            }

            if (!TryParseTimeRange(period.Start, period.End, out _, out _))
            {
                errors.Add("课节“" + period.Label + "”的默认时间无效。");
            }
        }

        foreach (var day in schedule.Days)
        {
            foreach (var cell in day.Cells)
            {
                ValidateCellTime(cell, "星期 " + (day.DayIndex + 1) + " 的课程格", errors);
            }
        }

        foreach (var dateOverride in schedule.DateOverrides)
        {
            foreach (var cell in dateOverride.Cells)
            {
                ValidateCellTime(cell, dateOverride.Date.ToString("yyyy-MM-dd") + " 的课程格", errors);
            }
        }

        var duplicateDates = schedule.DateOverrides
            .GroupBy(item => item.Date.Date)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        foreach (var date in duplicateDates)
        {
            errors.Add(date.ToString("yyyy-MM-dd") + " 存在重复的特殊日期规则。");
        }

        for (var dayIndex = 0; dayIndex < 5; dayIndex++)
        {
            ValidateNoOverlap(
                ResolveTemplateDay(schedule, dayIndex),
                schedule.Weekdays.ElementAtOrDefault(dayIndex) ?? "工作日",
                errors);
        }

        foreach (var dateOverride in schedule.DateOverrides.Where(item => !item.IsDayOff))
        {
            ValidateNoOverlap(
                ResolveDay(schedule, dateOverride.Date),
                dateOverride.Date.ToString("yyyy-MM-dd"),
                errors);
        }

        return errors.Distinct().ToList();
    }

    public static void ValidateOrThrow(ScheduleConfig schedule)
    {
        var errors = Validate(schedule);
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "课表配置存在问题：\n"
            + string.Join("\n", errors.Take(6).Select(error => "· " + error)));
    }

    public static bool TryParseTime(string? value, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(
            value?.Trim(),
            TimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
    }

    public static bool TryParseTimeRange(
        string? startText,
        string? endText,
        out TimeOnly start,
        out TimeOnly end)
    {
        var hasStart = TryParseTime(startText, out start);
        var hasEnd = TryParseTime(endText, out end);
        return hasStart && hasEnd && end > start;
    }

    public static int GetWeekdayIndex(DateTime date)
    {
        return date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday
            ? (int)date.DayOfWeek - (int)DayOfWeek.Monday
            : -1;
    }

    public static ScheduleCellConfig CloneCell(ScheduleCellConfig source)
    {
        return new ScheduleCellConfig
        {
            PeriodId = source.PeriodId,
            Course = source.Course,
            Kind = source.Kind,
            StartOverride = source.StartOverride,
            EndOverride = source.EndOverride
        };
    }

    private static ResolvedScheduleDay BuildResolvedDay(
        ScheduleConfig schedule,
        DateTime date,
        int? displayDayIndex,
        bool isDayOff,
        string? label,
        IReadOnlyDictionary<string, ScheduleCellConfig> cells)
    {
        var entries = new List<ResolvedScheduleEntry>();
        foreach (var period in schedule.Periods)
        {
            var cell = cells.TryGetValue(period.Id, out var configuredCell)
                ? configuredCell
                : new ScheduleCellConfig
                {
                    PeriodId = period.Id,
                    Kind = ScheduleCellKind.Empty
                };

            var overrideStart = default(TimeOnly);
            var overrideEnd = default(TimeOnly);
            var usesTimeOverride = !string.IsNullOrWhiteSpace(cell.StartOverride)
                && !string.IsNullOrWhiteSpace(cell.EndOverride)
                && TryParseTimeRange(cell.StartOverride, cell.EndOverride, out overrideStart, out overrideEnd);
            var hasDefaultTime = TryParseTimeRange(period.Start, period.End, out var defaultStart, out var defaultEnd);

            entries.Add(new ResolvedScheduleEntry(
                period.Id,
                period.Label,
                period.Note,
                cell.Course,
                cell.Kind,
                usesTimeOverride ? overrideStart : hasDefaultTime ? defaultStart : null,
                usesTimeOverride ? overrideEnd : hasDefaultTime ? defaultEnd : null,
                usesTimeOverride));
        }

        return new ResolvedScheduleDay(date, displayDayIndex, isDayOff, label, entries);
    }

    private static void ValidateCellTime(
        ScheduleCellConfig cell,
        string context,
        ICollection<string> errors)
    {
        var hasStart = !string.IsNullOrWhiteSpace(cell.StartOverride);
        var hasEnd = !string.IsNullOrWhiteSpace(cell.EndOverride);
        if (hasStart != hasEnd)
        {
            errors.Add(context + "必须同时填写特殊开始和结束时间。");
            return;
        }

        if (hasStart && !TryParseTimeRange(cell.StartOverride, cell.EndOverride, out _, out _))
        {
            errors.Add(context + "的特殊时间无效。");
        }
    }

    private static void ValidateNoOverlap(
        ResolvedScheduleDay day,
        string dayName,
        ICollection<string> errors)
    {
        var timedEntries = day.Entries
            .Where(entry =>
                entry.Kind != ScheduleCellKind.Empty
                && entry.Start.HasValue
                && entry.End.HasValue)
            .OrderBy(entry => entry.Start)
            .ToList();

        for (var index = 1; index < timedEntries.Count; index++)
        {
            var previous = timedEntries[index - 1];
            var current = timedEntries[index];
            if (current.Start!.Value < previous.End!.Value)
            {
                errors.Add(
                    dayName
                    + " 的“" + previous.Label
                    + "”与“" + current.Label
                    + "”时间重叠。");
            }
        }
    }

    private static void MigrateLegacyRows(
        ScheduleConfig schedule,
        IReadOnlyList<ScheduleRowConfig> legacyRows)
    {
        var periods = new List<SchedulePeriodConfig>();
        var days = Enumerable.Range(0, 5)
            .Select(dayIndex => new ScheduleDayConfig { DayIndex = dayIndex })
            .ToList();

        for (var rowIndex = 0; rowIndex < legacyRows.Count; rowIndex++)
        {
            var row = legacyRows[rowIndex];
            if (row is null)
            {
                continue;
            }

            var periodId = "period-" + (rowIndex + 1).ToString("00");
            var periodKind = row.IsBreak ? ScheduleCellKind.Break : ScheduleCellKind.Class;
            periods.Add(new SchedulePeriodConfig
            {
                Id = periodId,
                Label = string.IsNullOrWhiteSpace(row.Label) ? "第 " + (rowIndex + 1) + " 项" : row.Label.Trim(),
                Start = row.Start?.Trim() ?? string.Empty,
                End = row.End?.Trim() ?? string.Empty,
                Note = ExtractLegacyNote(row.Time),
                Kind = periodKind
            });

            row.Courses ??= [];
            for (var dayIndex = 0; dayIndex < 5; dayIndex++)
            {
                var rawCourse = row.IsBreak
                    ? row.Courses.FirstOrDefault() ?? string.Empty
                    : dayIndex < row.Courses.Count ? row.Courses[dayIndex] : string.Empty;
                var cell = ParseLegacyCell(periodId, rawCourse, periodKind);
                days[dayIndex].Cells.Add(cell);
            }
        }

        schedule.Periods = periods;
        schedule.Days = days;
    }

    private static ScheduleCellConfig ParseLegacyCell(
        string periodId,
        string rawCourse,
        ScheduleCellKind periodKind)
    {
        var normalized = (rawCourse ?? string.Empty)
            .Replace("\r\n", "\n")
            .Trim();

        if (IsEmptyCourse(normalized))
        {
            return new ScheduleCellConfig
            {
                PeriodId = periodId,
                Kind = ScheduleCellKind.Empty
            };
        }

        string? startOverride = null;
        string? endOverride = null;
        var course = normalized;
        var newlineIndex = normalized.IndexOf('\n');
        if (newlineIndex > 0)
        {
            var firstLine = normalized[..newlineIndex].Trim();
            var remainder = normalized[(newlineIndex + 1)..].Trim();
            if (TrySplitTimeRange(firstLine, out var start, out var end)
                && !string.IsNullOrWhiteSpace(remainder))
            {
                startOverride = start;
                endOverride = end;
                course = remainder;
            }
        }

        return new ScheduleCellConfig
        {
            PeriodId = periodId,
            Course = course,
            Kind = periodKind,
            StartOverride = startOverride,
            EndOverride = endOverride
        };
    }

    private static string? ExtractLegacyNote(string? legacyTime)
    {
        var normalized = (legacyTime ?? string.Empty).Replace("\r\n", "\n").Trim();
        var newlineIndex = normalized.IndexOf('\n');
        return newlineIndex >= 0 && newlineIndex + 1 < normalized.Length
            ? normalized[(newlineIndex + 1)..].Trim()
            : null;
    }

    private static bool TrySplitTimeRange(
        string value,
        out string start,
        out string end)
    {
        foreach (var separator in new[] { '–', '—', '-' })
        {
            var separatorIndex = value.IndexOf(separator);
            if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
            {
                continue;
            }

            var startCandidate = value[..separatorIndex].Trim();
            var endCandidate = value[(separatorIndex + 1)..].Trim();
            if (TryParseTimeRange(startCandidate, endCandidate, out _, out _))
            {
                start = startCandidate;
                end = endCandidate;
                return true;
            }
        }

        start = string.Empty;
        end = string.Empty;
        return false;
    }

    private static void NormalizeCell(ScheduleCellConfig cell)
    {
        cell.PeriodId = cell.PeriodId?.Trim() ?? string.Empty;
        cell.Course = (cell.Course ?? string.Empty)
            .Replace("\r\n", "\n")
            .Trim();
        cell.StartOverride = string.IsNullOrWhiteSpace(cell.StartOverride)
            ? null
            : cell.StartOverride.Trim();
        cell.EndOverride = string.IsNullOrWhiteSpace(cell.EndOverride)
            ? null
            : cell.EndOverride.Trim();

        if (!Enum.IsDefined(cell.Kind))
        {
            cell.Kind = ScheduleCellKind.Class;
        }

        if (cell.Kind == ScheduleCellKind.Empty || IsEmptyCourse(cell.Course))
        {
            cell.Kind = ScheduleCellKind.Empty;
            cell.Course = string.Empty;
            cell.StartOverride = null;
            cell.EndOverride = null;
        }
    }

    private static bool IsEmptyCourse(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.Trim() is "—" or "-";
    }

    private static string ChineseNumber(int number)
    {
        return number switch
        {
            1 => "一",
            2 => "二",
            3 => "三",
            4 => "四",
            5 => "五",
            _ => number.ToString()
        };
    }
}

internal static class ScheduleDefaults
{
    public static ScheduleConfig Create()
    {
        var periods = new List<SchedulePeriodConfig>
        {
            Period("period-01", "第一节课", "08:00", "08:40"),
            Period("period-02", "第二节课", "08:55", "09:35"),
            Period("break-morning", "大课间", "09:35", "10:05", ScheduleCellKind.Break),
            Period("period-03", "第三节课", "10:05", "10:45"),
            Period("period-04", "第四节课", "11:00", "11:40"),
            Period("period-05", "第五节课", "11:55", "12:35"),
            Period("period-06", "第六节课", "12:40", "13:20"),
            Period("period-07", "第七节课", "13:35", "14:15"),
            Period("period-08", "第八节课", "14:30", "15:10"),
            Period("period-09", "第九节课", "15:25", "16:05"),
            Period("period-10", "第十节课", "16:15", "16:55"),
            Period("period-11", "第十一节课", "17:10", "18:10", note: "晚托走班"),
            Period("extended", "延时服务", "18:40", "20:30")
        };

        return new ScheduleConfig
        {
            Periods = periods,
            Days =
            [
                Day(0,
                    Cell("period-01", "语文"),
                    Cell("period-02", "语文"),
                    Break("break-morning"),
                    Cell("period-03", "道法"),
                    Cell("period-04", "英语"),
                    Cell("period-05", "英语"),
                    Empty("period-06"),
                    Cell("period-07", "物理"),
                    Cell("period-08", "历史"),
                    Cell("period-09", "数学"),
                    Cell("period-10", "数学"),
                    Cell("period-11", "物理（单）\n数学（双）"),
                    Cell("extended", "语文")),
                Day(1,
                    Cell("period-01", "英语"),
                    Cell("period-02", "英语"),
                    Break("break-morning"),
                    Cell("period-03", "数学"),
                    Cell("period-04", "体育"),
                    Cell("period-05", "数学"),
                    Empty("period-06"),
                    Cell("period-07", "语文"),
                    Cell("period-08", "化学"),
                    Cell("period-09", "化学"),
                    Cell("period-10", "物理"),
                    Cell("period-11", "体活"),
                    Cell("extended", "物理（单）\n化学（双）", "18:30", "20:30")),
                Day(2,
                    Cell("period-01", "道法"),
                    Cell("period-02", "数学"),
                    Break("break-morning"),
                    Cell("period-03", "化学"),
                    Cell("period-04", "体育"),
                    Cell("period-05", "英语"),
                    Empty("period-06"),
                    Cell("period-07", "语文"),
                    Cell("period-08", "语文"),
                    Cell("period-09", "历史"),
                    Cell("period-10", "道法"),
                    Cell("period-11", "化学（单）\n语文（双）"),
                    Cell("extended", "数学")),
                Day(3,
                    Cell("period-01", "化学"),
                    Cell("period-02", "语文"),
                    Break("break-morning"),
                    Cell("period-03", "英语"),
                    Cell("period-04", "英语"),
                    Cell("period-05", "美术（单）\n音乐（双）"),
                    Empty("period-06"),
                    Cell("period-07", "体育"),
                    Cell("period-08", "物理"),
                    Cell("period-09", "数学"),
                    Cell("period-10", "数学"),
                    Cell("period-11", "历史/道法（单）\n英语（双）"),
                    Cell("extended", "英语")),
                Day(4,
                    Cell("period-01", "语文"),
                    Cell("period-02", "语文"),
                    Break("break-morning"),
                    Cell("period-03", "物理"),
                    Cell("period-04", "物理"),
                    Cell("period-05", "生物（单）\n地理（双）"),
                    Cell("period-06", "体育", "13:20", "14:00"),
                    Cell("period-07", "历史", "14:15", "14:55"),
                    Cell("period-08", "数学", "15:10", "15:50"),
                    Cell("period-09", "英语", "16:00", "16:40"),
                    Cell("period-10", "班会", "16:50", "17:30"),
                    Empty("period-11"),
                    Empty("extended"))
            ],
            DateOverrides = []
        };
    }

    private static SchedulePeriodConfig Period(
        string id,
        string label,
        string start,
        string end,
        ScheduleCellKind kind = ScheduleCellKind.Class,
        string? note = null)
    {
        return new SchedulePeriodConfig
        {
            Id = id,
            Label = label,
            Start = start,
            End = end,
            Kind = kind,
            Note = note
        };
    }

    private static ScheduleDayConfig Day(int dayIndex, params ScheduleCellConfig[] cells)
    {
        return new ScheduleDayConfig
        {
            DayIndex = dayIndex,
            Cells = cells.ToList()
        };
    }

    private static ScheduleCellConfig Cell(
        string periodId,
        string course,
        string? startOverride = null,
        string? endOverride = null)
    {
        return new ScheduleCellConfig
        {
            PeriodId = periodId,
            Course = course,
            Kind = ScheduleCellKind.Class,
            StartOverride = startOverride,
            EndOverride = endOverride
        };
    }

    private static ScheduleCellConfig Break(string periodId)
    {
        return new ScheduleCellConfig
        {
            PeriodId = periodId,
            Course = "做操 / 运动 / 学习",
            Kind = ScheduleCellKind.Break
        };
    }

    private static ScheduleCellConfig Empty(string periodId)
    {
        return new ScheduleCellConfig
        {
            PeriodId = periodId,
            Kind = ScheduleCellKind.Empty
        };
    }
}
