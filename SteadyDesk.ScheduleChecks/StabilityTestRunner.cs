using System.Diagnostics;

namespace SteadyDesk.ScheduleChecks;

internal sealed class StabilityTestRunner
{
    private const int MaximumReportedFailures = 60;

    private readonly Stopwatch _total = Stopwatch.StartNew();
    private readonly List<string> _reportedFailures = [];
    private int _suiteCount;
    private int _assertionCount;
    private int _failureCount;

    public void Suite(string name, Action test)
    {
        _suiteCount++;
        var failuresBefore = _failureCount;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            test();
        }
        catch (Exception exception)
        {
            RecordFailure(name + " raised " + exception.GetType().Name + ": " + exception.Message);
        }

        stopwatch.Stop();
        var status = failuresBefore == _failureCount ? "PASS" : "FAIL";
        Console.WriteLine($"[{status}] {name} ({stopwatch.ElapsedMilliseconds} ms)");
    }

    public void Check(string name, bool condition)
    {
        _assertionCount++;
        if (!condition)
        {
            RecordFailure(name);
        }
    }

    public void Equal<T>(string name, T expected, T actual)
    {
        Check(name + $" (expected: {expected}; actual: {actual})",
            EqualityComparer<T>.Default.Equals(expected, actual));
    }

    public void Throws<TException>(string name, Action action)
        where TException : Exception
    {
        _assertionCount++;

        try
        {
            action();
            RecordFailure(name + " (no exception was thrown)");
        }
        catch (TException)
        {
        }
        catch (Exception exception)
        {
            RecordFailure(name + " (threw " + exception.GetType().Name + ")");
        }
    }

    public void DoesNotThrow(string name, Action action)
    {
        _assertionCount++;

        try
        {
            action();
        }
        catch (Exception exception)
        {
            RecordFailure(name + " (threw " + exception.GetType().Name + ": " + exception.Message + ")");
        }
    }

    public int Complete()
    {
        _total.Stop();

        if (_failureCount == 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Final stability sweep passed: {_suiteCount} suites, {_assertionCount:N0} assertions, {_total.Elapsed.TotalSeconds:F1} s.");
            return 0;
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine(
            $"Final stability sweep failed: {_failureCount} of {_assertionCount:N0} assertions failed.");
        foreach (var failure in _reportedFailures)
        {
            Console.Error.WriteLine("- " + failure);
        }

        if (_failureCount > _reportedFailures.Count)
        {
            Console.Error.WriteLine(
                $"- ... {_failureCount - _reportedFailures.Count:N0} additional failures were suppressed.");
        }

        return 1;
    }

    private void RecordFailure(string failure)
    {
        _failureCount++;
        if (_reportedFailures.Count < MaximumReportedFailures)
        {
            _reportedFailures.Add(failure);
        }
    }
}
