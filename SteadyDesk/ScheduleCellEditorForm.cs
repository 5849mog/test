namespace SteadyDesk;

internal sealed class ScheduleCellEditorForm : Form
{
    private readonly TextBox _course;
    private readonly ComboBox _kind;
    private readonly CheckBox _customTime;
    private readonly DateTimePicker _start;
    private readonly DateTimePicker _end;
    private readonly Label _defaultTime;
    private readonly string _periodId;

    public ScheduleCellConfig EditedCell { get; private set; }

    public ScheduleCellEditorForm(
        ScheduleCellConfig source,
        SchedulePeriodConfig period,
        string dayName)
    {
        _periodId = period.Id;
        EditedCell = ScheduleEngine.CloneCell(source);

        Text = dayName + " · " + period.Label;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(430, 300);
        BackColor = Color.FromArgb(246, 239, 228);
        ForeColor = Color.FromArgb(39, 31, 27);
        Font = new Font("Microsoft YaHei", 10f);

        _course = new TextBox { Width = 250, Text = source.Course };
        _kind = new ComboBox
        {
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _kind.Items.AddRange(["课程", "课间", "空课"]);
        _kind.SelectedIndex = source.Kind switch
        {
            ScheduleCellKind.Break => 1,
            ScheduleCellKind.Empty => 2,
            _ => 0
        };

        _customTime = new CheckBox
        {
            Text = "为这一天使用特殊时间",
            AutoSize = true,
            Checked = !string.IsNullOrWhiteSpace(source.StartOverride)
                || !string.IsNullOrWhiteSpace(source.EndOverride)
        };
        _start = CreateTimePicker();
        _end = CreateTimePicker();
        _defaultTime = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(105, 88, 77),
            Text = "默认时间：" + period.Start + "–" + period.End
        };

        if (ScheduleEngine.TryParseTime(source.StartOverride, out var start))
        {
            _start.Value = DateTime.Today.Add(start.ToTimeSpan());
        }
        else if (ScheduleEngine.TryParseTime(period.Start, out start))
        {
            _start.Value = DateTime.Today.Add(start.ToTimeSpan());
        }

        if (ScheduleEngine.TryParseTime(source.EndOverride, out var end))
        {
            _end.Value = DateTime.Today.Add(end.ToTimeSpan());
        }
        else if (ScheduleEngine.TryParseTime(period.End, out end))
        {
            _end.Value = DateTime.Today.Add(end.ToTimeSpan());
        }

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(22)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(form, 0, "课程内容", _course);
        AddRow(form, 1, "课程格类型", _kind);
        form.Controls.Add(_defaultTime, 1, 2);
        form.Controls.Add(_customTime, 1, 3);

        var times = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        times.Controls.Add(_start);
        times.Controls.Add(new Label { Text = "至", AutoSize = true, Margin = new Padding(8, 8, 8, 0) });
        times.Controls.Add(_end);
        AddRow(form, 4, "特殊时间", times);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var save = CreateButton("保存", true);
        save.Click += (_, _) => SaveAndClose();
        var cancel = CreateButton("取消", false);
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        form.Controls.Add(buttons, 0, 5);
        form.SetColumnSpan(buttons, 2);

        _customTime.CheckedChanged += (_, _) => UpdateEnabledState();
        _kind.SelectedIndexChanged += (_, _) => UpdateEnabledState();

        Controls.Add(form);
        AcceptButton = save;
        CancelButton = cancel;
        UpdateEnabledState();
    }

    private void SaveAndClose()
    {
        var kind = _kind.SelectedIndex switch
        {
            1 => ScheduleCellKind.Break,
            2 => ScheduleCellKind.Empty,
            _ => ScheduleCellKind.Class
        };
        var course = _course.Text.Trim();

        if (kind != ScheduleCellKind.Empty && string.IsNullOrWhiteSpace(course))
        {
            MessageBox.Show(this, "课程或课间名称不能为空。", "课程格无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? startOverride = null;
        string? endOverride = null;
        if (kind != ScheduleCellKind.Empty && _customTime.Checked)
        {
            var start = TimeOnly.FromDateTime(_start.Value);
            var end = TimeOnly.FromDateTime(_end.Value);
            if (end <= start)
            {
                MessageBox.Show(this, "特殊结束时间必须晚于开始时间。", "课程格无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            startOverride = start.ToString("HH:mm");
            endOverride = end.ToString("HH:mm");
        }

        EditedCell = new ScheduleCellConfig
        {
            PeriodId = _periodId,
            Course = kind == ScheduleCellKind.Empty ? string.Empty : course,
            Kind = kind,
            StartOverride = startOverride,
            EndOverride = endOverride
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateEnabledState()
    {
        var isEmpty = _kind.SelectedIndex == 2;
        _course.Enabled = !isEmpty;
        _customTime.Enabled = !isEmpty;
        _start.Enabled = !isEmpty && _customTime.Checked;
        _end.Enabled = !isEmpty && _customTime.Checked;
    }

    private static DateTimePicker CreateTimePicker()
    {
        return new DateTimePicker
        {
            Width = 86,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true
        };
    }

    private static void AddRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Color.FromArgb(105, 88, 77),
            Margin = new Padding(0, 8, 8, 8)
        }, 0, row);
        control.Margin = new Padding(0, 4, 0, 8);
        panel.Controls.Add(control, 1, row);
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(109, 31, 42) : Color.FromArgb(255, 251, 243),
            ForeColor = primary ? Color.FromArgb(255, 251, 243) : Color.FromArgb(39, 31, 27),
            Padding = new Padding(12, 5, 12, 5),
            Margin = new Padding(4),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary
            ? Color.FromArgb(109, 31, 42)
            : Color.FromArgb(186, 151, 91);
        return button;
    }
}
