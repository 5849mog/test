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

    public AppConfig EditedConfig { get; private set; }
    public bool AutoStartEnabled => _autoStart.Checked;
    public bool RestoreOriginalRequested { get; private set; }
    public bool ExitRequested { get; private set; }

    public SettingsForm(AppConfig source, bool autoStartEnabled)
    {
        _draft = source.Clone();
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

    private void LoadDraftIntoControls()
    {
        _schedulePosition.Value = Math.Clamp((int)Math.Round(_draft.Wallpaper.ScheduleXPercent * 2f), 6, 50);
        _titleBox.Text = _draft.Content.ScheduleTitle;
        _eyebrowBox.Text = _draft.Content.Eyebrow;
        _countdownTitleBox.Text = _draft.Content.CountdownTitle;
        _showClock.Checked = _draft.Wallpaper.ShowClock;
        _showProgress.Checked = _draft.Wallpaper.ShowProgress;
        _showQuote.Checked = _draft.Wallpaper.ShowQuote;
        _backgroundLabel.Text = GetBackgroundName(_draft.Wallpaper.BackgroundPath);
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
        _draft.Wallpaper.ShowClock = _showClock.Checked;
        _draft.Wallpaper.ShowProgress = _showProgress.Checked;
        _draft.Wallpaper.ShowQuote = _showQuote.Checked;
        _draft.Content.ScheduleTitle = _titleBox.Text.Trim();
        _draft.Content.Eyebrow = _eyebrowBox.Text.Trim();
        _draft.Content.CountdownTitle = _countdownTitleBox.Text.Trim();

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

            var dateText = Convert.ToString(row.Cells["date"].Value);
            if (!DateTime.TryParse(dateText, out var date))
            {
                date = DateTime.Today.AddDays(30);
            }

            events.Add(new CountdownEventConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Date = date.Date,
                Visible = Convert.ToBoolean(row.Cells["visible"].Value ?? true),
                ShowProgress = Convert.ToBoolean(row.Cells["progress"].Value ?? true),
                Color = Convert.ToString(row.Cells["color"].Value) ?? "#B69A68"
            });
        }

        if (events.Count > 0)
        {
            _draft.Events = events;
        }

        var schedule = new List<ScheduleRowConfig>();
        foreach (DataGridViewRow row in _scheduleGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var item = new ScheduleRowConfig
            {
                Label = Convert.ToString(row.Cells["label"].Value) ?? string.Empty,
                Time = Convert.ToString(row.Cells["time"].Value) ?? string.Empty,
                Start = Convert.ToString(row.Cells["start"].Value) ?? string.Empty,
                End = Convert.ToString(row.Cells["end"].Value) ?? string.Empty,
                IsBreak = Convert.ToBoolean(row.Cells["break"].Value ?? false),
                Courses = []
            };

            for (var i = 0; i < 5; i++)
            {
                item.Courses.Add(Convert.ToString(row.Cells["day" + i].Value) ?? string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(item.Label))
            {
                schedule.Add(item);
            }
        }

        if (schedule.Count > 0)
        {
            _draft.Schedule.Rows = schedule;
        }

        _draft.Behavior.AutoStart = _autoStart.Checked;
        _draft.Normalize();
    }

    private void ApplyAndClose()
    {
        try
        {
            ReadControlsIntoDraft();
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

        try
        {
            _draft.Wallpaper.BackgroundPath = AppStorage.ImportBackground(dialog.FileName);
            _backgroundLabel.Text = GetBackgroundName(_draft.Wallpaper.BackgroundPath);
            RefreshPreview();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "更换底图失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreDefaultBackground()
    {
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
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_draft, ConfigStore.Options));
                _statusLabel.Text = "配置已导出。";
            }
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
            var imported = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(dialog.FileName), ConfigStore.Options)
                ?? throw new InvalidOperationException("配置文件为空或格式不正确。");
            imported.Normalize();
            _draft = imported;
            LoadDraftIntoControls();
            RefreshPreview();
            _statusLabel.Text = "配置已导入，点击“应用设置”后生效。";
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

        _draft = AppConfig.CreateDefault();
        _draft.Behavior.AutoStart = _autoStart.Checked;
        LoadDraftIntoControls();
        RefreshPreview();
    }

    private static void OpenDataFolder()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AppStorage.RootDirectory,
            UseShellExecute = true
        });
    }

    private static TabPage CreatePage(string text)
    {
        return new TabPage(text) { BackColor = Paper, Padding = new Padding(10) };
    }

    private static DataGridView CreateGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = PaperBright,
            BorderStyle = BorderStyle.FixedSingle,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true
        };
    }

    private static Label SectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 12f, FontStyle.Bold),
            ForeColor = Wine,
            Margin = new Padding(3, 12, 3, 6)
        };
    }

    private static Label MutedLabel(string text, int width, int height)
    {
        return new Label
        {
            Text = text,
            Width = width,
            Height = height,
            ForeColor = Muted,
            Margin = new Padding(3, 3, 3, 10)
        };
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Wine : PaperBright,
            ForeColor = primary ? PaperBright : Ink,
            Font = new Font("Microsoft YaHei", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
            Margin = new Padding(4),
            Padding = new Padding(12, 5, 12, 5),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? Wine : Gold;
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.MouseOverBackColor = primary
            ? Color.FromArgb(132, 40, 55)
            : Color.FromArgb(250, 239, 216);
        return button;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Muted,
            Margin = new Padding(4, 9, 12, 8)
        };
        control.Margin = new Padding(4, 5, 4, 5);
        panel.Controls.Add(title, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private static string GetBackgroundName(string path)
    {
        return path.Equals(AppStorage.DefaultBackgroundPath, StringComparison.OrdinalIgnoreCase)
            ? "内置默认底图"
            : Path.GetFileName(path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _preview.Image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
