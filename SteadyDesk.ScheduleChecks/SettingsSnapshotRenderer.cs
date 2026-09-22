using System.Drawing.Imaging;
using System.Windows.Forms;
using SteadyDesk;

namespace SteadyDesk.ScheduleChecks;

internal static class SettingsSnapshotRenderer
{
    private static readonly string[] PageNames =
    [
        "01-overview",
        "02-countdown",
        "03-schedule",
        "04-special-dates",
        "05-appearance",
        "06-content",
        "07-system"
    ];

    public static void Render(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var form = new SettingsForm(AppConfig.CreateDefault(), false)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(0, 0),
            ClientSize = new Size(1440, 900),
            ShowInTaskbar = false
        };

        form.Show();
        PumpUi();
        form.RenderPreviewForSnapshot();
        PumpUi();

        var tabs = Descendants(form).OfType<TabControl>().Single();
        if (tabs.TabPages.Count != PageNames.Length)
        {
            throw new InvalidOperationException(
                $"设置中心页面数量发生变化：期望 {PageNames.Length}，实际 {tabs.TabPages.Count}。请同步更新视觉快照名称。");
        }

        for (var index = 0; index < tabs.TabPages.Count; index++)
        {
            tabs.SelectedIndex = index;
            form.PerformLayout();
            PumpUi();
            Capture(form, Path.Combine(outputDirectory, PageNames[index] + ".png"));
        }

        form.Hide();
    }

    private static void Capture(Form form, string path)
    {
        var size = form.ClientSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void PumpUi()
    {
        for (var index = 0; index < 12; index++)
        {
            Application.DoEvents();
            Thread.Sleep(30);
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
