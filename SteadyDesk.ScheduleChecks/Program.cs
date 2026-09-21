namespace SteadyDesk.ScheduleChecks;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var runner = new StabilityTestRunner();

        ScheduleStabilityTests.Run(runner);
        ConfigurationStabilityTests.Run(runner);
        RenderingStabilityTests.Run(runner);
        UiStabilityTests.Run(runner);
        PreviewStabilityTests.Run(runner);

        return runner.Complete();
    }
}
