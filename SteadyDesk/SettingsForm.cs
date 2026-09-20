using System.Text.Json;

namespace SteadyDesk;

internal sealed class SettingsForm : Form
{
    private static readonly Color Paper = Color.FromArgb(246, 239, 228);
    private static readonly Color PaperBright = Color.FromArgb(255, 251, 243);
    private static readonly Color Wine = Color.FromArgb(109, 31, 42);
    private static readonly Color Ink = Color.FromArgb(39, 31, 27);
    private static readonly Color Muted = Color.FromArgb(105, 88, 77);
    private static readonly Color Gold = Color.FromArgb(186, 151, 91);

    private AppConfig _draft;
    private readonly PictureBox _preview;
    private readonly TrackBar _schedulePosition;
    private readonly TextBox _titleBox;
    private readonly TextBox _eyebrowBox;
    private readonly TextBox _countdownTitleBox;
    private readonly CheckBox _showClock;
    private readonly CheckBox _showProgress;
    private readonly CheckBox _showQuote;
    private readonly CheckBox _autoStart;
    private readonly Label _backgroundLabel;
    private readonly Label _statusLabel;
    private readonly DataGridView _eventsGrid;
    private readonly DataGridView _scheduleGrid;
    private readonly TrackBar _countdownPosition;
    private readonly TrackBar _countdownWidth;
    private readonly DateTimePicker _preparationStartDate;
    private readonly NumericUpDown _refreshInterval;
    private readonly TextBox _quotesBox;
    private readonly TextBox[] _weekdayInputs;
    private readonly Dictionary<string, TextBox> _themeInputs = new(StringComparer.OrdinalIgnoreCase);
    private string? _pendingBackgroundPath;

    private const string DefaultBackgroundMarker = "__STEADY_DESK_DEFAULT_BACKGROUND__";
    private static readonly string[] ThemeNames =
    [
        nameof(ThemeConfig.Wine),
        nameof(ThemeConfig.WineDeep),
        nameof(ThemeConfig.Paper),
        nameof(ThemeConfig.Ink),
        nameof(ThemeConfig.InkSoft),
        nameof(ThemeConfig.Gold),
        nameof(ThemeConfig.Champagne)
    ];

    private sealed class ConfigPackage
    {
        public int PackageVersion { get; set; } = 1;
        public AppConfig Config { get; set; } = AppConfig.CreateDefault();
        public string? BackgroundFileName { get; set; }
    }

    public AppConfig EditedConfig { get; private set; }
    public bool AutoStartEnabled => _autoStart.Checked;
    public bool RestoreOriginalRequested { get; private set; }
    public bool ExitRequested { get; private set; }

    public SettingsForm(AppConfig source, bool autoStartEnabled)
    {
        _draft = source.Clone();
        _draft.Behavior.AutoStart = autoStartEnabled;
        EditedConfig = _draft.Clone();

        Text = "稳序桌面 · 控制中心";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(980, 680);
        ClientSize = new Size(1180, 760);
        BackColor = Paper;
        ForeColor = Ink;
        Font = new Font("Microsoft YaHei", 10f);

        _preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(34, 28, 25),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };
        _schedulePosition = new TrackBar { Minimum = 6, Maximum = 50, TickFrequency = 4, SmallChange = 1, LargeChange = 4, Width = 410 };
        _countdownPosition = new TrackBar { Minimum = 50, Maximum = 70, TickFrequency = 5, SmallChange = 1, LargeChange = 5, Width = 410 };
        _countdownWidth = new TrackBar { Minimum = 25, Maximum = 44, TickFrequency = 4, SmallChange = 1, LargeChange = 4, Width = 410 };
        _preparationStartDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 160
        };
        _refreshInterval = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 300,
            Increment = 5,
            Width = 100
        };
        _quotesBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Width = 620,
            Height = 180
        };
        _weekdayInputs = Enumerable.Range(0, 5)
            .Select(_ => new TextBox { Width = 70 })
            .ToArray();
        foreach (var themeName in ThemeNames)
        {
            _themeInputs[themeName] = new TextBox { Width = 180 };
        }
        _titleBox = new TextBox { Width = 330 };
        _eyebrowBox = new TextBox { Width = 330 };
        _countdownTitleBox = new TextBox { Width = 330 };
        _showClock = new CheckBox { Text = "显示实时钟", AutoSize = true };
        _showProgress = new CheckBox { Text = "显示进度条", AutoSize = true };
        _showQuote = new CheckBox { Text = "显示激励语", AutoSize = true };
        _autoStart = new CheckBox { Text = "开机自动启动", AutoSize = true, Checked = autoStartEnabled };
        _backgroundLabel = new Label { AutoSize = false, AutoEllipsis = true, Width = 290, Height = 28, ForeColor = Wine };
        _statusLabel = new Label { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };

        _eventsGrid = CreateGrid();
        _scheduleGrid = CreateGrid();

        BuildUi();
        LoadDraftIntoControls();
        Shown += (_, _) => RefreshPreview();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Paper,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill };
        var eyebrow = new Label
        {
            Text = "STEADY DESK / CONTROL CENTER",
            AutoSize = true,
            ForeColor = Gold,
            Font = new Font("Georgia", 9f)
        };
        var title = new Label
        {
            Text = "稳序桌面",
            AutoSize = true,
            ForeColor = Wine,
            Font = new Font("SimSun", 25f, FontStyle.Bold),
            Location = new Point(0, 20)
        };
        var subtitle = new Label
        {
            Text = "一个看似简单，实际上可以被完整定制的学习桌面。",
            AutoSize = true,
            ForeColor = Muted,
            Location = new Point(165, 30)
        };
        header.Controls.AddRange([eyebrow, title, subtitle]);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 8) };
        tabs.TabPages.Add(BuildOverviewPage());
        tabs.TabPages.Add(BuildEventsPage());
        tabs.TabPages.Add(BuildSchedulePage());
        tabs.TabPages.Add(BuildAppearancePage());
        tabs.TabPages.Add(BuildContentPage());
        tabs.TabPages.Add(BuildSystemPage());

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var statusPanel = new Panel { Dock = DockStyle.Fill };
        statusPanel.Controls.Add(_statusLabel);
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true
        };
        var apply = MakeButton("应用设置", true);
        apply.Click += (_, _) => ApplyAndClose();
        var cancel = MakeButton("取消", false);
        cancel.DialogResult = DialogResult.Cancel;
        buttonPanel.Controls.Add(apply);
        buttonPanel.Controls.Add(cancel);
        footer.Controls.Add(statusPanel, 0, 0);
        footer.Controls.Add(buttonPanel, 1, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private TabPage BuildOverviewPage()
    {
        var page = CreatePage("总览");
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 720, BackColor = Paper };
        split.Panel1.Padding = new Padding(0, 0, 16, 0);
        split.Panel1.Controls.Add(_preview);

        var side = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8),
            BackColor = PaperBright
        };
        side.Controls.Add(SectionTitle("实时预览"));
        side.Controls.Add(MutedLabel("预览不会立即修改系统壁纸，点击“应用设置”后才会生效。", 340, 42));
        var refresh = MakeButton("刷新预览", true);
        refresh.Click += (_, _) => RefreshPreview();
        side.Controls.Add(refresh);
        side.Controls.Add(SectionTitle("当前底图"));
        side.Controls.Add(_backgroundLabel);
        var choose = MakeButton("更换底图", false);
        choose.Click += (_, _) => ChooseBackground();
        var restore = MakeButton("恢复默认底图", false);
        restore.Click += (_, _) => RestoreDefaultBackground();
        side.Controls.Add(choose);
        side.Controls.Add(restore);
        side.Controls.Add(SectionTitle("当前状态"));
        side.Controls.Add(MutedLabel("课表、倒计时、时钟、进度条和激励语都可以在其他标签页中调整。", 340, 60));
        split.Panel2.Controls.Add(side);
        page.Controls.Add(split);
        return page;
    }

    private TabPage BuildEventsPage()
    {
        var page = CreatePage("倒计时");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Paper };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        _eventsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "title", HeaderText = "事件名称", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _eventsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "date", HeaderText = "日期", Width = 130 });
        _eventsGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "visible", HeaderText = "显示", Width = 70 });
        _eventsGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "progress", HeaderText = "进度条", Width = 80 });
        _eventsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "color", HeaderText = "颜色", Width = 100 });
        root.Controls.Add(_eventsGrid, 0, 0);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var add = MakeButton("添加事件", false);
        add.Click += (_, _) => AddEvent();
        var remove = MakeButton("删除选中", false);
        remove.Click += (_, _) => RemoveEvent();
        bar.Controls.Add(add);
        bar.Controls.Add(remove);
        root.Controls.Add(bar, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildSchedulePage()
    {
        var page = CreatePage("课表");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Paper };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "label", HeaderText = "节次", Width = 100 });
        _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "time", HeaderText = "显示时间", Width = 120 });
        _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "start", HeaderText = "开始", Width = 75 });
        _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "end", HeaderText = "结束", Width = 75 });
        _scheduleGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "break", HeaderText = "课间", Width = 60 });
        for (var i = 0; i < 5; i++)
        {
            _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "day" + i,
                HeaderText = _draft.Schedule.Weekdays[i],
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
        }

        root.Controls.Add(_scheduleGrid, 0, 0);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var add = MakeButton("添加课表行", false);
        add.Click += (_, _) => AddScheduleRow();
        var remove = MakeButton("删除选中", false);
        remove.Click += (_, _) => RemoveScheduleRow();
        var reset = MakeButton("恢复默认课表", false);
        reset.Click += (_, _) =>
        {
            _draft.Schedule = ScheduleConfig.CreateDefault();
            LoadScheduleGrid();
        };
        bar.Controls.Add(add);
        bar.Controls.Add(remove);
        bar.Controls.Add(reset);
        root.Controls.Add(bar, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildAppearancePage()
    {
        var page = CreatePage("外观与组件");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(18),
            BackColor = PaperBright
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(panel, "课表水平位置", _schedulePosition);
        AddRow(panel, "倒计时水平位置", _countdownPosition);
        AddRow(panel, "倒计时宽度", _countdownWidth);
        AddRow(panel, "课表标题", _titleBox);
        AddRow(panel, "倒计时眉题", _eyebrowBox);
        AddRow(panel, "倒计时标题", _countdownTitleBox);

        var widgets = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        widgets.Controls.Add(_showClock);
        widgets.Controls.Add(_showProgress);
        widgets.Controls.Add(_showQuote);
        AddRow(panel, "显示组件", widgets);

        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildSystemPage()
    {
        var page = CreatePage("系统与数据");
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(18),
            BackColor = PaperBright
        };
        panel.Controls.Add(SectionTitle("系统行为"));
        panel.Controls.Add(_autoStart);
        panel.Controls.Add(MutedLabel("程序会驻留在任务栏右下角，并在日期、分钟和屏幕分辨率变化时刷新壁纸。", 700, 44));

        var export = MakeButton("导出全部配置", false);
        export.Click += (_, _) => ExportConfig();
        var import = MakeButton("导入配置", false);
        import.Click += (_, _) => ImportConfig();
        var reset = MakeButton("恢复全部默认设置", false);
        reset.Click += (_, _) => ResetAll();
        var folder = MakeButton("打开数据文件夹", false);
        folder.Click += (_, _) => OpenDataFolder();
        var restore = MakeButton("停用并恢复原壁纸", false);
        restore.ForeColor = Wine;
        restore.Click += (_, _) =>
        {
            RestoreOriginalRequested = true;
            DialogResult = DialogResult.Abort;
            Close();
        };
        var exit = MakeButton("退出并保留当前壁纸", false);
        exit.Click += (_, _) =>
        {
            ExitRequested = true;
            DialogResult = DialogResult.Abort;
            Close();
        };

        panel.Controls.Add(SectionTitle("数据管理"));
        panel.Controls.Add(export);
        panel.Controls.Add(import);
        panel.Controls.Add(reset);
        panel.Controls.Add(folder);
        panel.Controls.Add(SectionTitle("退出选项"));
        panel.Controls.Add(restore);
        panel.Controls.Add(exit);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildContentPage()
    {
        var page = CreatePage("内容与主题");
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Paper
        };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(18),
            BackColor = PaperBright
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(panel, "准备起始日期", _preparationStartDate);
        AddRow(panel, "刷新间隔（秒）", _refreshInterval);

        var weekdays = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        for (var index = 0; index < _weekdayInputs.Length; index++)
        {
            weekdays.Controls.Add(new Label
            {
                Text = (index + 1) + "：",
                AutoSize = true,
                Margin = new Padding(3, 8, 1, 3)
            });
            weekdays.Controls.Add(_weekdayInputs[index]);
        }

        AddRow(panel, "工作日名称", weekdays);
        AddRow(panel, "激励语（空行分隔）", _quotesBox);

        foreach (var themeName in ThemeNames)
        {
            AddRow(panel, "主题色 · " + themeName, _themeInputs[themeName]);
        }

        scroll.Controls.Add(panel);
        page.Controls.Add(scroll);
        return page;
    }

    private void LoadDraftIntoControls()
    {
        _schedulePosition.Value = Math.Clamp((int)Math.Round(_draft.Wallpaper.ScheduleXPercent * 2f), 6, 50);
        _countdownPosition.Value = Math.Clamp((int)Math.Round(_draft.Wallpaper.CountdownXPercent), 50, 70);
        _countdownWidth.Value = Math.Clamp((int)Math.Round(_draft.Wallpaper.CountdownWidthPercent), 25, 44);
        _titleBox.Text = _draft.Content.ScheduleTitle;
        _eyebrowBox.Text = _draft.Content.Eyebrow;
        _countdownTitleBox.Text = _draft.Content.CountdownTitle;

        var preparationDate = _draft.Content.PreparationStartDate.Date;
        if (preparationDate < _preparationStartDate.MinDate)
        {
            preparationDate = _preparationStartDate.MinDate;
        }
        else if (preparationDate > _preparationStartDate.MaxDate)
        {
            preparationDate = _preparationStartDate.MaxDate;
        }

        _preparationStartDate.Value = preparationDate;
        _refreshInterval.Value = Math.Clamp(_draft.Behavior.RefreshIntervalSeconds, 5, 300);
        _autoStart.Checked = _draft.Behavior.AutoStart;
        _showClock.Checked = _draft.Wallpaper.ShowClock;
        _showProgress.Checked = _draft.Wallpaper.ShowProgress;
        _showQuote.Checked = _draft.Wallpaper.ShowQuote;
        _backgroundLabel.Text = GetBackgroundName(_draft.Wallpaper.BackgroundPath);
        _quotesBox.Text = string.Join(
            Environment.NewLine + Environment.NewLine,
            _draft.Content.Quotes);

        for (var index = 0; index < _weekdayInputs.Length; index++)
        {
            _weekdayInputs[index].Text = index < _draft.Schedule.Weekdays.Count
                ? _draft.Schedule.Weekdays[index]
                : string.Empty;
        }

        foreach (var themeName in ThemeNames)
        {
            _themeInputs[themeName].Text = GetThemeValue(_draft.Theme, themeName);
        }

        LoadEventGrid();
        LoadScheduleGrid();
    }

    private void LoadEventGrid()
    {
        _eventsGrid.Rows.Clear();
        foreach (var item in _draft.Events)
        {
            _eventsGrid.Rows.Add(item.Title, item.Date.ToString("yyyy-MM-dd"), item.Visible, item.ShowProgress, item.Color);
        }
    }

    private void LoadScheduleGrid()
    {
        _scheduleGrid.Rows.Clear();
        for (var index = 0; index < 5; index++)
        {
            _scheduleGrid.Columns["day" + index].HeaderText = index < _draft.Schedule.Weekdays.Count
                ? _draft.Schedule.Weekdays[index]
                : "—";
        }

        foreach (var row in _draft.Schedule.Rows)
        {
            var values = new object[10];
            values[0] = row.Label;
            values[1] = row.Time;
            values[2] = row.Start;
            values[3] = row.End;
            values[4] = row.IsBreak;
            for (var i = 0; i < 5; i++)
            {
                values[5 + i] = i < row.Courses.Count ? row.Courses[i] : string.Empty;
            }

            _scheduleGrid.Rows.Add(values);
        }
    }

    private void ReadControlsIntoDraft()
    {
        _draft.Wallpaper.ScheduleXPercent = _schedulePosition.Value / 2f;
        _draft.Wallpaper.CountdownXPercent = _countdownPosition.Value;
        _draft.Wallpaper.CountdownWidthPercent = _countdownWidth.Value;
        _draft.Wallpaper.ShowClock = _showClock.Checked;
        _draft.Wallpaper.ShowProgress = _showProgress.Checked;
        _draft.Wallpaper.ShowQuote = _showQuote.Checked;
        _draft.Content.ScheduleTitle = _titleBox.Text.Trim();
        _draft.Content.Eyebrow = _eyebrowBox.Text.Trim();
        _draft.Content.CountdownTitle = _countdownTitleBox.Text.Trim();
        _draft.Content.PreparationStartDate = _preparationStartDate.Value.Date;
        _draft.Content.Quotes = ParseQuotes(_quotesBox.Text);
        _draft.Schedule.Weekdays = _weekdayInputs
            .Select(input => input.Text.Trim())
            .ToList();
        if (_draft.Schedule.Weekdays.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("工作日名称不能为空。");
        }

        _draft.Theme.Wine = ReadColor(_themeInputs[nameof(ThemeConfig.Wine)], "Wine");
        _draft.Theme.WineDeep = ReadColor(_themeInputs[nameof(ThemeConfig.WineDeep)], "WineDeep");
        _draft.Theme.Paper = ReadColor(_themeInputs[nameof(ThemeConfig.Paper)], "Paper");
        _draft.Theme.Ink = ReadColor(_themeInputs[nameof(ThemeConfig.Ink)], "Ink");
        _draft.Theme.InkSoft = ReadColor(_themeInputs[nameof(ThemeConfig.InkSoft)], "InkSoft");
        _draft.Theme.Gold = ReadColor(_themeInputs[nameof(ThemeConfig.Gold)], "Gold");
        _draft.Theme.Champagne = ReadColor(_themeInputs[nameof(ThemeConfig.Champagne)], "Champagne");

        var events = new List<CountdownEventConfig>();
        foreach (DataGridViewRow row in _eventsGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var title = Convert.ToString(row.Cells["title"].Value)?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var dateText = Convert.ToString(row.Cells["date"].Value)?.Trim();
            if (!DateTime.TryParse(dateText, out var date))
            {
                throw new InvalidOperationException("事件“" + title + "”的日期无效，请使用 yyyy-MM-dd 格式。");
            }

            var color = Convert.ToString(row.Cells["color"].Value)?.Trim();
            if (string.IsNullOrWhiteSpace(color))
            {
                color = "#B69A68";
            }
            ValidateColor(color, "事件“" + title + "”的颜色");

            events.Add(new CountdownEventConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Date = date.Date,
                Visible = Convert.ToBoolean(row.Cells["visible"].Value ?? true),
                ShowProgress = Convert.ToBoolean(row.Cells["progress"].Value ?? true),
                Color = color
            });
        }
        _draft.Events = events;

        var schedule = new List<ScheduleRowConfig>();
        foreach (DataGridViewRow row in _scheduleGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var item = new ScheduleRowConfig
            {
                Label = Convert.ToString(row.Cells["label"].Value)?.Trim() ?? string.Empty,
                Time = Convert.ToString(row.Cells["time"].Value)?.Trim() ?? string.Empty,
                Start = Convert.ToString(row.Cells["start"].Value)?.Trim() ?? string.Empty,
                End = Convert.ToString(row.Cells["end"].Value)?.Trim() ?? string.Empty,
                IsBreak = Convert.ToBoolean(row.Cells["break"].Value ?? false),
                Courses = []
            };

            for (var index = 0; index < 5; index++)
            {
                item.Courses.Add(Convert.ToString(row.Cells["day" + index].Value) ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(item.Label))
            {
                continue;
            }

            if (!TimeSpan.TryParse(item.Start, out var start)
                || !TimeSpan.TryParse(item.End, out var end)
                || end <= start)
            {
                throw new InvalidOperationException(
                    "课表“" + item.Label + "”的开始/结束时间无效。");
            }

            schedule.Add(item);
        }
        _draft.Schedule.Rows = schedule;

        _draft.Behavior.AutoStart = _autoStart.Checked;
        _draft.Behavior.RefreshIntervalSeconds = (int)_refreshInterval.Value;
        _draft.Normalize();
    }

    private void ApplyAndClose()
    {
        try
        {
            ReadControlsIntoDraft();
            if (_pendingBackgroundPath is not null)
            {
                _draft.Wallpaper.BackgroundPath = AppStorage.CommitBackground(_pendingBackgroundPath);
                _pendingBackgroundPath = null;
            }

            EditedConfig = _draft.Clone();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "设置无效", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshPreview()
    {
        try
        {
            ReadControlsIntoDraft();
            WallpaperRenderer.Render(_draft, AppStorage.PreviewWallpaperPath, 1280, 720, DateTime.Now);
            using var stream = File.OpenRead(AppStorage.PreviewWallpaperPath);
            using var temporary = Image.FromStream(stream);
            _preview.Image?.Dispose();
            _preview.Image = new Bitmap(temporary);
            _statusLabel.Text = "预览已更新：" + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "预览失败：" + exception.Message;
        }
    }

    private void ChooseBackground()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择新的底图",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var stagedPath = AppStorage.CreateTemporaryBackgroundPath();
        try
        {
            AppStorage.ImportBackground(dialog.FileName, stagedPath);
            AppStorage.DeleteIfExists(_pendingBackgroundPath);
            _pendingBackgroundPath = stagedPath;
            _draft.Wallpaper.BackgroundPath = stagedPath;
            _backgroundLabel.Text = GetBackgroundName(stagedPath);
            RefreshPreview();
        }
        catch (Exception exception)
        {
            AppStorage.DeleteIfExists(stagedPath);
            MessageBox.Show(this, exception.Message, "更换底图失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreDefaultBackground()
    {
        AppStorage.DeleteIfExists(_pendingBackgroundPath);
        _pendingBackgroundPath = null;
        _draft.Wallpaper.BackgroundPath = AppStorage.DefaultBackgroundPath;
        _backgroundLabel.Text = GetBackgroundName(_draft.Wallpaper.BackgroundPath);
        RefreshPreview();
    }

    private void AddEvent()
    {
        _eventsGrid.Rows.Add("新事件", DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"), true, true, "#B69A68");
    }

    private void RemoveEvent()
    {
        foreach (DataGridViewRow row in _eventsGrid.SelectedRows)
        {
            if (!row.IsNewRow)
            {
                _eventsGrid.Rows.Remove(row);
            }
        }
    }

    private void AddScheduleRow()
    {
        _scheduleGrid.Rows.Add("新课程", "08:00–08:40", "08:00", "08:40", false, "", "", "", "", "");
    }

    private void RemoveScheduleRow()
    {
        foreach (DataGridViewRow row in _scheduleGrid.SelectedRows)
        {
            if (!row.IsNewRow)
            {
                _scheduleGrid.Rows.Remove(row);
            }
        }
    }

    private void ExportConfig()
    {
        try
        {
            ReadControlsIntoDraft();
            using var dialog = new SaveFileDialog
            {
                Title = "导出稳序桌面配置",
                Filter = "稳序桌面配置|*.steady.json|JSON 文件|*.json",
                FileName = "稳序桌面配置.steady.json"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var exportConfig = _draft.Clone();
            exportConfig.Wallpaper.OriginalWallpaperPath = null;
            exportConfig.Wallpaper.OriginalWallpaperStyle = null;
            exportConfig.Wallpaper.OriginalTileWallpaper = null;

            var directory = Path.GetDirectoryName(dialog.FileName) ?? ".";
            var backgroundFileName = DefaultBackgroundMarker;
            var source = exportConfig.Wallpaper.BackgroundPath;
            if (!IsDefaultBackground(source) && File.Exists(source))
            {
                backgroundFileName = Path.GetFileNameWithoutExtension(dialog.FileName) + ".background.png";
                var destination = Path.Combine(directory, backgroundFileName);
                if (!Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(source, destination, true);
                }

                exportConfig.Wallpaper.BackgroundPath = backgroundFileName;
            }
            else
            {
                exportConfig.Wallpaper.BackgroundPath = DefaultBackgroundMarker;
            }

            var package = new ConfigPackage
            {
                Config = exportConfig,
                BackgroundFileName = backgroundFileName
            };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(package, ConfigStore.Options));
            _statusLabel.Text = "配置和底图已导出。";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportConfig()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入稳序桌面配置",
            Filter = "稳序桌面配置|*.steady.json;*.json|JSON 文件|*.json",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            AppConfig imported;
            string? packageBackground = null;

            using (var document = JsonDocument.Parse(json))
            {
                if (document.RootElement.TryGetProperty("Config", out _))
                {
                    var package = JsonSerializer.Deserialize<ConfigPackage>(json, ConfigStore.Options)
                        ?? throw new InvalidOperationException("配置包为空。");
                    imported = package.Config;
                    packageBackground = package.BackgroundFileName;
                }
                else
                {
                    imported = JsonSerializer.Deserialize<AppConfig>(json, ConfigStore.Options)
                        ?? throw new InvalidOperationException("配置文件为空或格式不正确。");
                }
            }

            imported.Normalize();

            var currentOriginalPath = _draft.Wallpaper.OriginalWallpaperPath;
            var currentOriginalStyle = _draft.Wallpaper.OriginalWallpaperStyle;
            var currentOriginalTile = _draft.Wallpaper.OriginalTileWallpaper;
            var configDirectory = Path.GetDirectoryName(dialog.FileName) ?? ".";
            var importedBackgroundPath = imported.Wallpaper.BackgroundPath;
            string? sourceBackground = null;
            string? warning = null;

            if (!string.Equals(importedBackgroundPath, DefaultBackgroundMarker, StringComparison.OrdinalIgnoreCase))
            {
                var candidate = packageBackground;
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    candidate = importedBackgroundPath;
                }

                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    sourceBackground = Path.IsPathRooted(candidate)
                        ? candidate
                        : Path.Combine(configDirectory, candidate);
                }
            }

            AppStorage.DeleteIfExists(_pendingBackgroundPath);
            _pendingBackgroundPath = null;
            if (!string.IsNullOrWhiteSpace(sourceBackground) && File.Exists(sourceBackground))
            {
                var stagedPath = AppStorage.CreateTemporaryBackgroundPath();
                AppStorage.ImportBackground(sourceBackground, stagedPath);
                _pendingBackgroundPath = stagedPath;
                imported.Wallpaper.BackgroundPath = stagedPath;
            }
            else
            {
                if (!string.Equals(importedBackgroundPath, DefaultBackgroundMarker, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(importedBackgroundPath))
                {
                    warning = "配置中的底图文件未找到，已改用内置默认底图。";
                }
                imported.Wallpaper.BackgroundPath = AppStorage.DefaultBackgroundPath;
            }

            imported.Wallpaper.OriginalWallpaperPath = currentOriginalPath;
            imported.Wallpaper.OriginalWallpaperStyle = currentOriginalStyle;
            imported.Wallpaper.OriginalTileWallpaper = currentOriginalTile;
            imported.Normalize();
            _draft = imported;
            _autoStart.Checked = imported.Behavior.AutoStart;
            LoadDraftIntoControls();
            RefreshPreview();
            _statusLabel.Text = warning is null
                ? "配置和底图已导入，点击“应用设置”后生效。"
                : warning + " 点击“应用设置”后生效。";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetAll()
    {
        if (MessageBox.Show(this, "确定恢复全部默认设置吗？", "恢复默认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        AppStorage.DeleteIfExists(_pendingBackgroundPath);
        _pendingBackgroundPath = null;
        _draft = AppConfig.CreateDefault();
        _draft.Behavior.AutoStart = _autoStart.Checked;
        LoadDraftIntoControls();
        RefreshPreview();
    }

    private static void OpenDataFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppStorage.RootDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "打开数据文件夹失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static List<string> ParseQuotes(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();
    }

    private static string ReadColor(TextBox box, string name)
    {
        var value = box.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("主题色“" + name + "”不能为空。");
        }

        ValidateColor(value, "主题色“" + name + "”");
        return value;
    }

    private static void ValidateColor(string value, string name)
    {
        try
        {
            ColorTranslator.FromHtml(value);
        }
        catch
        {
            throw new InvalidOperationException(name + "无效，请使用例如 #6D1F2A 的格式。");
        }
    }

    private static string GetThemeValue(ThemeConfig theme, string name)
    {
        return name switch
        {
            nameof(ThemeConfig.Wine) => theme.Wine,
            nameof(ThemeConfig.WineDeep) => theme.WineDeep,
            nameof(ThemeConfig.Paper) => theme.Paper,
            nameof(ThemeConfig.Ink) => theme.Ink,
            nameof(ThemeConfig.InkSoft) => theme.InkSoft,
            nameof(ThemeConfig.Gold) => theme.Gold,
            nameof(ThemeConfig.Champagne) => theme.Champagne,
            _ => string.Empty
        };
    }

    private static bool IsDefaultBackground(string path)
    {
        return string.Equals(path, DefaultBackgroundMarker, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, AppStorage.DefaultBackgroundPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBackgroundName(string path)
    {
        return IsDefaultBackground(path)
            ? "内置默认底图"
            : Path.GetFileName(path);
    }

    private static TabPage CreatePage(string text)
    {
        return new TabPage(text) { BackColor = Paper, Padding = new Padding(10) };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _preview.Image?.Dispose();
            AppStorage.DeleteIfExists(_pendingBackgroundPath);
            _pendingBackgroundPath = null;
        }

        base.Dispose(disposing);
    }
}
