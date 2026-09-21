namespace SteadyDesk;

internal enum PreviewScenario
{
    Current,
    ClassInProgress,
    BreakInProgress,
    FridaySpecial,
    DayOff,
    RescheduledDay,
    ManyCountdowns
}

internal sealed record PreviewResolution(string Label, int Width, int Height)
{
    public override string ToString() => Label;
}

internal sealed record PreviewScenarioChoice(string Label, PreviewScenario Scenario)
{
    public override string ToString() => Label;
}

internal sealed record PreviewOptions(
    int Width,
    int Height,
    DateTime Moment,
    PreviewScenario Scenario);

internal sealed class SettingsPreviewPane : UserControl
{
    private readonly PictureBox _picture;
    private readonly Label _stateLabel;
    private readonly Label _summaryLabel;
    private readonly ComboBox _scenario;
    private readonly ComboBox _resolution;
    private readonly CheckBox _useCurrentTime;
    private readonly DateTimePicker _date;
    private readonly DateTimePicker _time;
    private readonly System.Windows.Forms.Timer _clockTimer;
    private bool _suppressEvents;

    public event EventHandler? OptionsChanged;
    public event EventHandler? RefreshRequested;

    public PreviewOptions Options
    {
        get
        {
            var resolution = _resolution.SelectedItem as PreviewResolution
                ?? new PreviewResolution("高清 · 1280×720", 1280, 720);
            var choice = _scenario.SelectedItem as PreviewScenarioChoice
                ?? new PreviewScenarioChoice("当前状态", PreviewScenario.Current);
            var moment = _useCurrentTime.Checked
                ? DateTime.Now
                : _date.Value.Date.Add(_time.Value.TimeOfDay);
            return new PreviewOptions(
                resolution.Width,
                resolution.Height,
                moment,
                choice.Scenario);
        }
    }

    public SettingsPreviewPane()
    {
        Name = "LivePreviewPane";
        AccessibleName = "固定实时预览";
        Dock = DockStyle.Fill;
        MinimumSize = new Size(350, 0);
        BackColor = SettingsPalette.PaperBright;
        Padding = new Padding(14);

        _picture = new PictureBox
        {
            Name = "LivePreviewImage",
            AccessibleName = "桌面实时预览图",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(34, 28, 25),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };
        _stateLabel = new Label
        {
            Text = "实时预览",
            AutoSize = true,
            BackColor = Color.FromArgb(28, SettingsPalette.Gold),
            ForeColor = SettingsPalette.Wine,
            Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold),
            Padding = new Padding(9, 4, 9, 4),
            Margin = new Padding(0, 2, 0, 0)
        };
        _summaryLabel = new Label
        {
            Text = "预览不会修改系统壁纸",
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = SettingsPalette.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 5, 0, 0)
        };

        _scenario = CreateComboBox("预览场景");
        _scenario.Items.AddRange(
        [
            new PreviewScenarioChoice("当前状态", PreviewScenario.Current),
            new PreviewScenarioChoice("上课状态", PreviewScenario.ClassInProgress),
            new PreviewScenarioChoice("课间状态", PreviewScenario.BreakInProgress),
            new PreviewScenarioChoice("星期五特殊作息", PreviewScenario.FridaySpecial),
            new PreviewScenarioChoice("停课日", PreviewScenario.DayOff),
            new PreviewScenarioChoice("调课日", PreviewScenario.RescheduledDay),
            new PreviewScenarioChoice("多倒计时", PreviewScenario.ManyCountdowns)
        ]);
        _scenario.SelectedIndex = 0;

        _resolution = CreateComboBox("预览分辨率");
        _resolution.Items.AddRange(
        [
            new PreviewResolution("高清 · 1280×720", 1280, 720),
            new PreviewResolution("全高清 · 1920×1080", 1920, 1080),
            new PreviewResolution("16:10 · 1920×1200", 1920, 1200),
            new PreviewResolution("超宽屏 · 2560×1080", 2560, 1080)
        ]);
        _resolution.SelectedIndex = 0;

        _useCurrentTime = new CheckBox
        {
            Text = "使用当前日期与时间",
            AutoSize = true,
            Checked = true,
            ForeColor = SettingsPalette.Ink,
            Margin = new Padding(3, 7, 3, 4)
        };
        _date = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 124,
            Enabled = false
        };
        _time = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 82,
            Enabled = false
        };
        _date.Value = DateTime.Today;
        _time.Value = DateTime.Today.Add(DateTime.Now.TimeOfDay);

        _clockTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _clockTimer.Tick += (_, _) =>
        {
            if (_useCurrentTime.Checked)
            {
                RaiseOptionsChanged();
            }
        };
        _clockTimer.Start();

        BuildUi();
        WireEvents();
    }

    public void ShowPreview(Image image, string summary)
    {
        var previous = _picture.Image;
        _picture.Image = image;
        previous?.Dispose();
        _stateLabel.Text = "实时预览 · 已同步";
        _stateLabel.ForeColor = SettingsPalette.Wine;
        _summaryLabel.Text = summary + " · 尚未写入系统壁纸";
    }

    public void ShowRefreshing()
    {
        _stateLabel.Text = "实时预览 · 更新中";
        _stateLabel.ForeColor = SettingsPalette.Gold;
    }

    public void ShowPending(string message)
    {
        _stateLabel.Text = "保留上次有效预览";
        _stateLabel.ForeColor = Color.FromArgb(148, 100, 28);
        _summaryLabel.Text = message;
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = SettingsPalette.PaperBright
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 178));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = SettingsPalette.PaperBright
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new Label
        {
            Text = "桌面效果",
            Dock = DockStyle.Fill,
            ForeColor = SettingsPalette.Wine,
            Font = new Font("Microsoft YaHei", 12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        header.Controls.Add(_stateLabel, 1, 0);

        var imageFrame = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SettingsPalette.PaperInset,
            Padding = new Padding(7)
        };
        imageFrame.Controls.Add(_picture);

        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = SettingsPalette.PaperBright
        };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddPreviewRow(controls, 0, "场景", _scenario);
        AddPreviewRow(controls, 1, "分辨率", _resolution);
        controls.Controls.Add(_useCurrentTime, 1, 2);

        var timeRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        timeRow.Controls.Add(_date);
        timeRow.Controls.Add(_time);
        controls.Controls.Add(new Label
        {
            Text = "模拟时间",
            AutoSize = true,
            ForeColor = SettingsPalette.Muted,
            Margin = new Padding(3, 8, 8, 3)
        }, 0, 3);
        controls.Controls.Add(timeRow, 1, 3);

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var refresh = MakeSmallButton("立即刷新");
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        var reset = MakeSmallButton("恢复当前预览");
        reset.Click += (_, _) => ResetOptions();
        buttonRow.Controls.Add(refresh);
        buttonRow.Controls.Add(reset);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = SettingsPalette.PaperBright
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(_summaryLabel, 0, 0);
        footer.Controls.Add(buttonRow, 1, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(imageFrame, 0, 1);
        root.Controls.Add(controls, 0, 2);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);
    }

    private void WireEvents()
    {
        _scenario.SelectedIndexChanged += (_, _) => RaiseOptionsChanged();
        _resolution.SelectedIndexChanged += (_, _) => RaiseOptionsChanged();
        _date.ValueChanged += (_, _) => RaiseOptionsChanged();
        _time.ValueChanged += (_, _) => RaiseOptionsChanged();
        _useCurrentTime.CheckedChanged += (_, _) =>
        {
            _date.Enabled = !_useCurrentTime.Checked;
            _time.Enabled = !_useCurrentTime.Checked;
            RaiseOptionsChanged();
        };
    }

    private void ResetOptions()
    {
        _suppressEvents = true;
        try
        {
            _scenario.SelectedIndex = 0;
            _resolution.SelectedIndex = 0;
            _useCurrentTime.Checked = true;
            _date.Value = DateTime.Today;
            _time.Value = DateTime.Today.Add(DateTime.Now.TimeOfDay);
            _date.Enabled = false;
            _time.Enabled = false;
        }
        finally
        {
            _suppressEvents = false;
        }

        RaiseOptionsChanged();
    }

    private void RaiseOptionsChanged()
    {
        if (!_suppressEvents)
        {
            OptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static ComboBox CreateComboBox(string accessibleName)
    {
        return new ComboBox
        {
            AccessibleName = accessibleName,
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = SettingsPalette.PaperBright,
            ForeColor = SettingsPalette.Ink,
            IntegralHeight = false,
            MaxDropDownItems = 8
        };
    }

    private static void AddPreviewRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = SettingsPalette.Muted,
            Margin = new Padding(3, 8, 8, 3)
        }, 0, row);
        control.Margin = new Padding(0, 3, 0, 3);
        panel.Controls.Add(control, 1, row);
    }

    private static Button MakeSmallButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = SettingsPalette.PaperBright,
            ForeColor = SettingsPalette.Ink,
            Padding = new Padding(7, 2, 7, 2),
            Margin = new Padding(4, 7, 0, 4),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = SettingsPalette.Gold;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clockTimer.Stop();
            _clockTimer.Dispose();
            _picture.Image?.Dispose();
            _picture.Image = null;
        }

        base.Dispose(disposing);
    }
}
