namespace SteadyDesk;

internal sealed partial class SettingsForm
{
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

        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PaperBright,
            Padding = new Padding(0, 0, 0, 4)
        };
        header.Paint += (_, eventArgs) =>
        {
            using var hairline = new Pen(Color.FromArgb(86, Line), 1f);
            using var accent = new SolidBrush(Color.FromArgb(185, Wine));
            eventArgs.Graphics.DrawLine(hairline, 0, header.Height - 1, header.Width, header.Height - 1);
            eventArgs.Graphics.FillRectangle(accent, 0, header.Height - 2, 96, 2);
        };

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
            Text = "设置随改随看，确认后再写入桌面。",
            AutoSize = true,
            ForeColor = Muted,
            Location = new Point(165, 30)
        };
        header.Controls.AddRange([eyebrow, title, subtitle]);

        var tabs = _settingsTabs = new TabControl
        {
            Name = "SettingsCategories",
            Dock = DockStyle.Fill,
            Padding = new Point(18, 8),
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(116, 34),
            SizeMode = TabSizeMode.Fixed,
            BackColor = Paper
        };
        tabs.DrawItem += DrawTab;
        tabs.TabPages.Add(BuildOverviewPage());
        tabs.TabPages.Add(BuildEventsPage());
        tabs.TabPages.Add(BuildSchedulePage());
        tabs.TabPages.Add(BuildDateOverridesPage());
        tabs.TabPages.Add(BuildAppearancePage());
        tabs.TabPages.Add(BuildContentPage());
        tabs.TabPages.Add(BuildSystemPage());

        var workspace = _previewWorkspace = new SplitContainer
        {
            Name = "SettingsPreviewWorkspace",
            AccessibleName = "设置与固定实时预览",
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = Paper,
            BorderStyle = BorderStyle.None,
            SplitterWidth = 8,
            Size = new Size(1340, 690),
            SplitterDistance = 820,
            Panel1MinSize = 560,
            Panel2MinSize = 350
        };
        workspace.Panel1.Padding = new Padding(0, 0, 8, 0);
        workspace.Panel2.Padding = new Padding(8, 0, 0, 0);
        workspace.Panel1.Controls.Add(tabs);
        workspace.Panel2.Controls.Add(_previewPane);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = PaperBright,
            Padding = new Padding(0, 5, 0, 0)
        };
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
        var apply = MakeButton("保存并应用", true);
        apply.Click += (_, _) => ApplyAndClose();
        var cancel = MakeButton("取消", false);
        cancel.DialogResult = DialogResult.Cancel;
        buttonPanel.Controls.Add(apply);
        buttonPanel.Controls.Add(cancel);
        footer.Controls.Add(statusPanel, 0, 0);
        footer.Controls.Add(buttonPanel, 1, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(workspace, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
        Resize += (_, _) => ApplyResponsiveLayout();
        ApplyResponsiveLayout();
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private void ApplyResponsiveLayout()
    {
        if (_previewWorkspace is not SplitContainer workspace)
        {
            return;
        }

        var compact = ClientSize.Width < 1220;
        if (compact == _compactPreviewLayout
            && workspace.Orientation == (compact ? Orientation.Horizontal : Orientation.Vertical))
        {
            return;
        }

        _compactPreviewLayout = compact;
        workspace.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
        if (compact)
        {
            workspace.Panel1MinSize = 250;
            workspace.Panel2MinSize = 210;
            workspace.Panel1.Padding = new Padding(0, 0, 0, 6);
            workspace.Panel2.Padding = new Padding(0, 6, 0, 0);
        }
        else
        {
            workspace.Panel1MinSize = 560;
            workspace.Panel2MinSize = 350;
            workspace.Panel1.Padding = new Padding(0, 0, 8, 0);
            workspace.Panel2.Padding = new Padding(8, 0, 0, 0);
        }

        var available = compact ? workspace.Height : workspace.Width;
        var minimumTotal = workspace.Panel1MinSize + workspace.Panel2MinSize + workspace.SplitterWidth;
        if (available >= minimumTotal)
        {
            var preferred = (int)(available * (compact ? 0.56f : 0.61f));
            workspace.SplitterDistance = Math.Clamp(
                preferred,
                workspace.Panel1MinSize,
                available - workspace.SplitterWidth - workspace.Panel2MinSize);
        }
    }

    private TabPage BuildOverviewPage()
    {
        var page = CreatePage("总览");
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(18),
            BackColor = PaperBright
        };

        panel.Controls.Add(SectionTitle("实时预览已经固定在右侧"));
        panel.Controls.Add(MutedLabel(
            "修改文字、位置、颜色、课表或倒计时后，右侧会自动更新。预览不会改动系统壁纸，只有点击“保存并应用”才会正式生效。",
            690,
            62));
        panel.Controls.Add(SectionTitle("当前底图"));
        panel.Controls.Add(_backgroundLabel);
        var backgroundButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var choose = MakeButton("更换底图", false);
        choose.Click += (_, _) => ChooseBackground();
        var restore = MakeButton("恢复默认底图", false);
        restore.Click += (_, _) => RestoreDefaultBackground();
        backgroundButtons.Controls.Add(choose);
        backgroundButtons.Controls.Add(restore);
        panel.Controls.Add(backgroundButtons);

        panel.Controls.Add(SectionTitle("预览场景"));
        panel.Controls.Add(MutedLabel(
            "右侧可以模拟上课、课间、星期五特殊作息、停课、调课和多倒计时。所有模拟都只存在于预览，不会写入你的课表。",
            690,
            62));
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildEventsPage()
    {
        var page = CreatePage("倒计时");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Paper };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        _eventsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "title",
            HeaderText = "事件名称",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _eventsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "date", HeaderText = "日期", Width = 130 });
        _eventsGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "visible", HeaderText = "显示", Width = 70 });
        _eventsGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "progress", HeaderText = "进度条", Width = 80 });
        _eventsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "color", HeaderText = "颜色", Width = 100 });
        root.Controls.Add(_eventsGrid, 0, 0);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
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
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            BackColor = Paper
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        root.Controls.Add(MutedLabel(
            "直接编辑课程名称；双击周一至周五的课程格，可设置空课、课间或这一天的特殊时间。",
            900,
            36), 0, 0);

        _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "label", HeaderText = "节次", Width = 105 });
        _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "start", HeaderText = "默认开始", Width = 85 });
        _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "end", HeaderText = "默认结束", Width = 85 });
        _scheduleGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "break", HeaderText = "课间", Width = 58 });
        for (var index = 0; index < 5; index++)
        {
            _scheduleGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "day" + index,
                HeaderText = _draftController.Snapshot().Schedule.Weekdays[index],
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
        }

        root.Controls.Add(_scheduleGrid, 0, 1);
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var add = MakeButton("添加课表行", false);
        add.Click += (_, _) => AddScheduleRow();
        var remove = MakeButton("删除选中", false);
        remove.Click += (_, _) => RemoveScheduleRow();
        var edit = MakeButton("编辑选中课程格", false);
        edit.Click += (_, _) =>
        {
            if (_scheduleGrid.CurrentCell is not null)
            {
                EditScheduleCell(_scheduleGrid.CurrentCell.RowIndex, _scheduleGrid.CurrentCell.ColumnIndex);
            }
        };
        var reset = MakeButton("恢复默认课表", false);
        reset.Click += (_, _) => ResetSchedule();
        bar.Controls.Add(add);
        bar.Controls.Add(remove);
        bar.Controls.Add(edit);
        bar.Controls.Add(reset);
        root.Controls.Add(bar, 0, 2);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildDateOverridesPage()
    {
        var page = CreatePage("特殊日期");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            BackColor = Paper
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        root.Controls.Add(MutedLabel(
            "具体日期可设为停课、套用任意工作日，或逐节覆盖课程、空课和时间；日期规则始终优先于普通周课表。",
            900,
            50), 0, 0);

        _dateOverridesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "date", HeaderText = "日期", Width = 130 });
        _dateOverridesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "label",
            HeaderText = "说明",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _dateOverridesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "dayOff", HeaderText = "停课", Width = 70 });
        _dateOverridesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "baseDay", HeaderText = "套用星期", Width = 120 });
        _dateOverridesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "overrides",
            HeaderText = "课程覆盖",
            Width = 92,
            ReadOnly = true
        });
        root.Controls.Add(_dateOverridesGrid, 0, 1);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var add = MakeButton("添加特殊日期", false);
        add.Click += (_, _) => AddDateOverride();
        var editCourses = MakeButton("编辑当天课程", false);
        editCourses.Click += (_, _) => EditDateOverrideSchedule();
        var remove = MakeButton("删除选中", false);
        remove.Click += (_, _) => RemoveDateOverride();
        bar.Controls.Add(add);
        bar.Controls.Add(editCourses);
        bar.Controls.Add(remove);
        root.Controls.Add(bar, 0, 2);
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
        panel.Controls.Add(MutedLabel(
            "程序会驻留在任务栏右下角，并在日期、分钟和屏幕分辨率变化时刷新壁纸。",
            700,
            44));

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
}
