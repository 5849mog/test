namespace SteadyDesk;

internal sealed class ScheduleDateOverrideEditorForm : Form
{
    private static readonly Color Paper = Color.FromArgb(246, 239, 228);
    private static readonly Color PaperBright = Color.FromArgb(255, 251, 243);
    private static readonly Color Wine = Color.FromArgb(109, 31, 42);
    private static readonly Color Ink = Color.FromArgb(39, 31, 27);
    private static readonly Color Muted = Color.FromArgb(105, 88, 77);
    private static readonly Color Gold = Color.FromArgb(186, 151, 91);
    private static readonly Color Line = Color.FromArgb(226, 216, 202);

    private readonly ScheduleConfig _schedule;
    private readonly DateTime _date;
    private readonly int? _baseDayIndex;
    private readonly DataGridView _grid;
    private readonly Dictionary<string, ScheduleCellConfig> _overrides;

    public List<ScheduleCellConfig> EditedCells { get; private set; }

    public ScheduleDateOverrideEditorForm(
        ScheduleConfig schedule,
        DateTime date,
        int? baseDayIndex,
        IEnumerable<ScheduleCellConfig> sourceCells)
    {
        _schedule = schedule;
        _date = date.Date;
        _baseDayIndex = baseDayIndex;
        _overrides = sourceCells
            .Where(cell => !string.IsNullOrWhiteSpace(cell.PeriodId))
            .GroupBy(cell => cell.PeriodId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => ScheduleEngine.CloneCell(group.Last()),
                StringComparer.OrdinalIgnoreCase);
        EditedCells = _overrides.Values
            .Select(ScheduleEngine.CloneCell)
            .ToList();

        Text = _date.ToString("yyyy-MM-dd") + " · 当天课程覆盖";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 540);
        ClientSize = new Size(900, 620);
        BackColor = Paper;
        ForeColor = Ink;
        Font = new Font("Microsoft YaHei", 10f);

        _grid = CreateGrid();
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "period",
            HeaderText = "节次",
            Width = 130,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "inherited",
            HeaderText = "继承安排",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "override",
            HeaderText = "当天覆盖",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        });
        _grid.CellDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0)
            {
                EditCurrent();
            }
        };

        var sourceDayIndex = _baseDayIndex ?? ScheduleEngine.GetWeekdayIndex(_date);
        var sourceName = sourceDayIndex is >= 0 and < 5
            ? _schedule.Weekdays.ElementAtOrDefault(sourceDayIndex) ?? "工作日"
            : "无基础课表";
        var description = new Label
        {
            Dock = DockStyle.Fill,
            Text = "当前继承：" + sourceName
                + "。仅有差异的课程格会被保存；选择“恢复继承”后，会继续跟随周课表变化。",
            ForeColor = Muted,
            Padding = new Padding(2, 8, 2, 8)
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var save = CreateButton("保存覆盖", true);
        save.Click += (_, _) => SaveAndClose();
        var cancel = CreateButton("取消", false);
        cancel.DialogResult = DialogResult.Cancel;
        var restore = CreateButton("恢复继承", false);
        restore.Click += (_, _) => RestoreCurrent();
        var edit = CreateButton("编辑选中课程格", false);
        edit.Click += (_, _) => EditCurrent();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(restore);
        buttons.Controls.Add(edit);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = new Padding(20),
            BackColor = Paper
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.Controls.Add(description, 0, 0);
        root.Controls.Add(_grid, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);

        AcceptButton = save;
        CancelButton = cancel;
        LoadRows();
    }

    private void LoadRows()
    {
        _grid.Rows.Clear();
        foreach (var period in _schedule.Periods)
        {
            var rowIndex = _grid.Rows.Add(period.Label, string.Empty, string.Empty);
            var row = _grid.Rows[rowIndex];
            row.Tag = period.Id;
            RefreshRow(row);
        }
    }

    private void RefreshRow(DataGridViewRow row)
    {
        if (row.Tag is not string periodId)
        {
            return;
        }

        var period = _schedule.Periods.First(item =>
            item.Id.Equals(periodId, StringComparison.OrdinalIgnoreCase));
        var inherited = GetInheritedCell(periodId);
        row.Cells["inherited"].Value = FormatCell(period, inherited);

        if (_overrides.TryGetValue(periodId, out var dateCell))
        {
            row.Cells["override"].Value = FormatCell(period, dateCell);
            row.Cells["override"].Style.ForeColor = Wine;
            row.Cells["override"].Style.Font = new Font("Microsoft YaHei", 9.2f, FontStyle.Bold);
        }
        else
        {
            row.Cells["override"].Value = "继承";
            row.Cells["override"].Style.ForeColor = Muted;
            row.Cells["override"].Style.Font = new Font("Microsoft YaHei", 9.2f, FontStyle.Italic);
        }
    }

    private ScheduleCellConfig GetInheritedCell(string periodId)
    {
        var sourceDayIndex = _baseDayIndex ?? ScheduleEngine.GetWeekdayIndex(_date);
        if (sourceDayIndex is >= 0 and < 5)
        {
            var sourceDay = _schedule.Days.FirstOrDefault(day => day.DayIndex == sourceDayIndex);
            var sourceCell = sourceDay?.Cells.LastOrDefault(cell =>
                cell.PeriodId.Equals(periodId, StringComparison.OrdinalIgnoreCase));
            if (sourceCell is not null)
            {
                return ScheduleEngine.CloneCell(sourceCell);
            }
        }

        return new ScheduleCellConfig
        {
            PeriodId = periodId,
            Kind = ScheduleCellKind.Empty
        };
    }

    private void EditCurrent()
    {
        var row = _grid.CurrentRow;
        if (row?.Tag is not string periodId)
        {
            return;
        }

        var period = _schedule.Periods.First(item =>
            item.Id.Equals(periodId, StringComparison.OrdinalIgnoreCase));
        var source = _overrides.TryGetValue(periodId, out var configured)
            ? ScheduleEngine.CloneCell(configured)
            : GetInheritedCell(periodId);

        using var dialog = new ScheduleCellEditorForm(
            source,
            period,
            _date.ToString("MM-dd"));
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _overrides[periodId] = ScheduleEngine.CloneCell(dialog.EditedCell);
        RefreshRow(row);
    }

    private void RestoreCurrent()
    {
        var row = _grid.CurrentRow;
        if (row?.Tag is not string periodId)
        {
            return;
        }

        _overrides.Remove(periodId);
        RefreshRow(row);
    }

    private void SaveAndClose()
    {
        EditedCells = _schedule.Periods
            .Where(period => _overrides.ContainsKey(period.Id))
            .Select(period => ScheduleEngine.CloneCell(_overrides[period.Id]))
            .ToList();
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string FormatCell(
        SchedulePeriodConfig period,
        ScheduleCellConfig cell)
    {
        return ScheduleEngine.FormatCell(ScheduleEngine.ResolveEntry(period, cell))
            .Replace("\r\n", " / ")
            .Replace("\n", " / ");
    }

    private static DataGridView CreateGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = PaperBright,
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Line,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeight = 36,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = true,
            EnableHeadersVisualStyles = false,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(242, 234, 221),
                ForeColor = Wine,
                Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold),
                Padding = new Padding(6, 0, 6, 0)
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = PaperBright,
                ForeColor = Ink,
                SelectionBackColor = Color.FromArgb(232, 220, 201),
                SelectionForeColor = Ink,
                Font = new Font("Microsoft YaHei", 9.2f),
                Padding = new Padding(7, 5, 7, 5),
                WrapMode = DataGridViewTriState.True
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(252, 248, 240)
            }
        };
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Wine : PaperBright,
            ForeColor = primary ? PaperBright : Ink,
            Padding = new Padding(12, 5, 12, 5),
            Margin = new Padding(4),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? Wine : Gold;
        return button;
    }
}
