namespace SteadyDesk.ScheduleChecks;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2
            && string.Equals(args[0], "--render-settings-snapshots", StringComparison.OrdinalIgnoreCase))
        {
            SettingsSnapshotRenderer.Render(args[1]);
            return 0;
        }

        var runner = new StabilityTestRunner();

        ScheduleStabilityTests.Run(runner);
        ConfigurationStabilityTests.Run(runner);
        RenderingStabilityTests.Run(runner);
        UiStabilityTests.Run(runner);
        PreviewStabilityTests.Run(runner);
        UpdateStabilityTests.Run(runner);

        return runner.Complete();
    }
}
