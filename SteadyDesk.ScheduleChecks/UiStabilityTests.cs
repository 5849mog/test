using System.Windows.Forms;
using SteadyDesk;

namespace SteadyDesk.ScheduleChecks;

internal static class UiStabilityTests
{
    public static void Run(StabilityTestRunner runner)
    {
        runner.Suite("设置中心无显示构造检查", () => VerifySettingsForm(runner));
        runner.Suite("课表编辑器无显示构造检查", () => VerifyScheduleEditors(runner));
    }

    private static void VerifySettingsForm(StabilityTestRunner runner)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var form = new SettingsForm(AppConfig.CreateDefault(), false);
        _ = form.Handle;
        form.PerformLayout();

        var controls = Descendants(form).ToList();
        var tabs = controls.OfType<TabControl>().ToList();
        var grids = controls.OfType<DataGridView>().ToList();
        var splitters = controls.OfType<SplitContainer>().ToList();
        var previewPanes = controls.OfType<SettingsPreviewPane>().ToList();
        var buttons = controls.OfType<Button>().Select(button => button.Text).ToHashSet();

        runner.Equal("设置中心标题", "稳序桌面 · 控制中心", form.Text);
        runner.Check("设置中心支持调整大小", form.FormBorderStyle == FormBorderStyle.Sizable);
        runner.Check("设置中心最小尺寸合理", form.MinimumSize.Width >= 980 && form.MinimumSize.Height >= 680);
        runner.Check("设置中心包含标签页容器", tabs.Count >= 1);
        runner.Check("设置中心至少七个功能页", tabs.Sum(tab => tab.TabPages.Count) >= 7);
        runner.Check("设置中心包含事件、周课表和特殊日期表格", grids.Count >= 3);
        runner.Equal("设置中心只有一个固定实时预览", 1, previewPanes.Count);
        runner.Check(
            "实时预览与设置页并排存在",
            splitters.Any(splitter =>
                Descendants(splitter.Panel1).OfType<TabControl>().Any()
                && Descendants(splitter.Panel2).OfType<SettingsPreviewPane>().Any()));
        var workspace = splitters.SingleOrDefault(splitter => splitter.Name == "SettingsPreviewWorkspace");
        if (workspace is not null)
        {
            form.ClientSize = new Size(1100, 700);
            form.PerformLayout();
            runner.Equal("窄窗口自动上下布局", Orientation.Horizontal, workspace.Orientation);

            form.ClientSize = new Size(1380, 820);
            form.PerformLayout();
            runner.Equal("宽窗口恢复左右布局", Orientation.Vertical, workspace.Orientation);
        }
        runner.Check(
            "实时预览提供场景与分辨率选项",
            Descendants(previewPanes[0]).OfType<ComboBox>().Count() >= 2);
        runner.Check(
            "实时预览提供导出与放大查看",
            buttons.Contains("导出预览") && buttons.Contains("放大查看"));
        var exitInvoked = false;
        using (var exitButton = SettingsPreviewPane.CreateFullScreenExitButton(() => exitInvoked = true))
        {
            runner.Equal("全屏退出按钮文字", "退出全屏", exitButton.Text);
            runner.Equal("全屏退出按钮辅助名称", "退出全屏预览", exitButton.AccessibleName);
            runner.Check(
                "全屏退出按钮满足触控尺寸",
                exitButton.Width >= 120 && exitButton.Height >= 48);
            exitButton.PerformClick();
        }
        runner.Check("全屏退出按钮可触发关闭动作", exitInvoked);
        runner.Check("设置中心保留最终确认按钮", buttons.Contains("保存并应用"));
        runner.Check("设置中心保留导入入口", buttons.Contains("导入配置"));
        runner.Check("设置中心保留导出入口", buttons.Contains("导出全部配置"));
        runner.Check("设置中心保留恢复默认入口", buttons.Contains("恢复全部默认设置"));
    }

    private static void VerifyScheduleEditors(StabilityTestRunner runner)
    {
        var schedule = ScheduleConfig.CreateDefault();
        var period = schedule.Periods[0];
        var source = schedule.Days[0].Cells.Single(cell => cell.PeriodId == period.Id);

        using (var cellEditor = new ScheduleCellEditorForm(source, period, "周一"))
        {
            _ = cellEditor.Handle;
            cellEditor.PerformLayout();
            var textBoxes = Descendants(cellEditor).OfType<TextBox>().ToList();
            runner.Check("课程格编辑器支持多行课程", textBoxes.Any(box => box.Multiline));
            runner.Check(
                "课程格编辑器保留课程内容",
                textBoxes.Any(box => box.Text == source.Course));
            runner.Check(
                "课程格编辑器包含特殊时间开关",
                Descendants(cellEditor).OfType<CheckBox>()
                    .Any(box => box.Text.Contains("特殊时间", StringComparison.Ordinal)));
        }

        using var dateEditor = new ScheduleDateOverrideEditorForm(
            schedule,
            new DateTime(2026, 9, 26),
            0,
            []);
        _ = dateEditor.Handle;
        dateEditor.PerformLayout();

        var grids = Descendants(dateEditor).OfType<DataGridView>().ToList();
        runner.Equal("特殊日期编辑器只有一个主表格", 1, grids.Count);
        runner.Equal("特殊日期编辑器列出全部课节", schedule.Periods.Count, grids[0].Rows.Count);
        runner.Check(
            "特殊日期编辑器保留继承恢复入口",
            Descendants(dateEditor).OfType<Button>()
                .Any(button => button.Text == "恢复继承"));
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
