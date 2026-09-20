namespace CountdownWallpaper;

internal sealed class SchedulePositionDialog : Form
{
    private const int MinimumHalfPercent = 6;
    private const int MaximumHalfPercent = 50;
    private const int DefaultHalfPercent = 43;

    private readonly TrackBar _trackBar;
    private readonly Label _valueLabel;

    public float SelectedXPercent => _trackBar.Value / 2f;

    public SchedulePositionDialog(float currentXPercent)
    {
        Text = "调整课表水平位置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(540, 230);
        Font = new Font("Microsoft YaHei", 10f, FontStyle.Regular, GraphicsUnit.Point);

        var title = new Label
        {
            AutoSize = false,
            Text = "左右移动整块课表",
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(24, 20),
            Size = new Size(492, 28)
        };

        var description = new Label
        {
            AutoSize = false,
            Text = "只调整 X 轴；纵向位置、课表大小和右侧倒计时保持不变。移动范围已限制，避免与倒计时重叠。",
            Location = new Point(24, 51),
            Size = new Size(492, 45)
        };

        _trackBar = new TrackBar
        {
            Minimum = MinimumHalfPercent,
            Maximum = MaximumHalfPercent,
            TickFrequency = 4,
            SmallChange = 1,
            LargeChange = 4,
            Value = Math.Clamp((int)Math.Round(currentXPercent * 2f), MinimumHalfPercent, MaximumHalfPercent),
            Location = new Point(24, 96),
            Size = new Size(492, 45)
        };
        _trackBar.ValueChanged += (_, _) => UpdateValueLabel();

        _valueLabel = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(24, 139),
            Size = new Size(492, 24)
        };

        var resetButton = new Button
        {
            Text = "恢复默认位置",
            Location = new Point(24, 180),
            Size = new Size(125, 32)
        };
        resetButton.Click += (_, _) => _trackBar.Value = DefaultHalfPercent;

        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(352, 180),
            Size = new Size(78, 32)
        };

        var applyButton = new Button
        {
            Text = "应用",
            DialogResult = DialogResult.OK,
            Location = new Point(438, 180),
            Size = new Size(78, 32)
        };

        AcceptButton = applyButton;
        CancelButton = cancelButton;
        Controls.AddRange([title, description, _trackBar, _valueLabel, resetButton, cancelButton, applyButton]);
        UpdateValueLabel();
    }

    private void UpdateValueLabel()
    {
        var percent = SelectedXPercent;
        var direction = percent < 21.5f ? "偏左" : percent > 21.5f ? "偏右" : "默认";
        _valueLabel.Text = $"课表左缘距屏幕左侧：{percent:0.0}%（{direction}）";
    }
}
