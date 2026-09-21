namespace SteadyDesk;

internal sealed class SettingsDraftController
{
    private AppConfig _current;

    public SettingsDraftController(AppConfig source)
    {
        _current = Validate(source.Clone());
    }

    public AppConfig Snapshot() => _current.Clone();

    public void Replace(AppConfig source)
    {
        _current = Validate(source.Clone());
    }

    public AppConfig UpdateOrThrow(Action<AppConfig> update)
    {
        var candidate = _current.Clone();
        update(candidate);
        candidate = Validate(candidate);
        _current = candidate;
        return candidate.Clone();
    }

    public bool TryUpdate(
        Action<AppConfig> update,
        out AppConfig snapshot,
        out string? error)
    {
        try
        {
            snapshot = UpdateOrThrow(update);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            snapshot = _current.Clone();
            error = exception.Message;
            return false;
        }
    }

    private static AppConfig Validate(AppConfig candidate)
    {
        candidate.Normalize();
        if (candidate.Schedule.Weekdays.Count != 5
            || candidate.Schedule.Weekdays.Any(string.IsNullOrWhiteSpace)
            || candidate.Schedule.Weekdays.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 5)
        {
            throw new InvalidOperationException("五个工作日名称必须完整且不能重复。");
        }
        ScheduleEngine.ValidateOrThrow(candidate.Schedule);
        return candidate;
    }
}
