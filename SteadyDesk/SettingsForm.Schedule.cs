namespace SteadyDesk;

internal sealed partial class SettingsForm
{
    private void AddEvent()
    {
        var rowIndex = _eventsGrid.Rows.Add(
            "新事件",
            DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
            true,
            true,
            "#B69A68");
        _eventsGrid.Rows[rowIndex].Tag = Guid.NewGuid().ToString("N");
        NotifyDraftChanged();
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
        NotifyDraftChanged();
    }

    private void AddScheduleRow()
    {
        var periodId = "period-" + Guid.NewGuid().ToString("N");
        var rowIndex = _scheduleGrid.Rows.Add(
            "新课程",
            "08:00",
            "08:40",
            false,
            "",
            "",
            "",
            "",
            "");
        _scheduleGrid.Rows[rowIndex].Tag = periodId;
        for (var dayIndex = 0; dayIndex < 5; dayIndex++)
        {
            _scheduleCellDrafts[(periodId, dayIndex)] = new ScheduleCellConfig
            {
                PeriodId = periodId,
                Kind = ScheduleCellKind.Empty
            };
        }
        NotifyDraftChanged();
    }

    private void RemoveScheduleRow()
    {
        foreach (DataGridViewRow row in _scheduleGrid.SelectedRows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            if (row.Tag is string periodId)
            {
                for (var dayIndex = 0; dayIndex < 5; dayIndex++)
                {
                    _scheduleCellDrafts.Remove((periodId, dayIndex));
                }
            }

            _scheduleGrid.Rows.Remove(row);
        }
        NotifyDraftChanged();
    }

    private void ResetSchedule()
    {
        var snapshot = _draftController.UpdateOrThrow(config =>
            config.Schedule = ScheduleConfig.CreateDefault());
        _loadingControls = true;
        try
        {
            LoadScheduleGrid(snapshot);
            LoadDateOverridesGrid(snapshot);
        }
        finally
        {
            _loadingControls = false;
        }
        NotifyDraftChanged();
    }

    private void SyncScheduleRowKind(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _scheduleGrid.Rows.Count)
        {
            return;
        }

        var row = _scheduleGrid.Rows[rowIndex];
        if (row.Tag is not string periodId)
        {
            return;
        }

        var targetKind = Convert.ToBoolean(row.Cells["break"].Value ?? false)
            ? ScheduleCellKind.Break
            : ScheduleCellKind.Class;
        for (var dayIndex = 0; dayIndex < 5; dayIndex++)
        {
            if (_scheduleCellDrafts.TryGetValue((periodId, dayIndex), out var cell)
                && cell.Kind != ScheduleCellKind.Empty)
            {
                cell.Kind = targetKind;
            }
        }
    }

    private void EditScheduleCell(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0
            || rowIndex >= _scheduleGrid.Rows.Count
            || columnIndex < 0
            || columnIndex >= _scheduleGrid.Columns.Count)
        {
            return;
        }

        var columnName = _scheduleGrid.Columns[columnIndex].Name;
        if (!columnName.StartsWith("day", StringComparison.Ordinal)
            || !int.TryParse(columnName.AsSpan(3), out var dayIndex)
            || dayIndex is < 0 or > 4)
        {
            _statusLabel.Text = "请选择周一至周五中的一个课程格。";
            return;
        }

        var row = _scheduleGrid.Rows[rowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        var snapshot = _draftController.Snapshot();
        var period = CreatePeriodFromGridRow(row, snapshot.Schedule);
        var key = (period.Id, dayIndex);
        var source = _scheduleCellDrafts.TryGetValue(key, out var configuredCell)
            ? ScheduleEngine.CloneCell(configuredCell)
            : new ScheduleCellConfig { PeriodId = period.Id };

        var course = Convert.ToString(row.Cells[columnName].Value)?.Trim() ?? string.Empty;
        if (course is "—" or "-")
        {
            course = string.Empty;
        }

        source.Course = course;
        if (string.IsNullOrWhiteSpace(course))
        {
            source.Kind = ScheduleCellKind.Empty;
        }
        else if (source.Kind == ScheduleCellKind.Empty)
        {
            source.Kind = period.Kind == ScheduleCellKind.Break
                ? ScheduleCellKind.Break
                : ScheduleCellKind.Class;
        }

        var dayName = dayIndex < _weekdayInputs.Length
            ? _weekdayInputs[dayIndex].Text.Trim()
            : "工作日";
        using var dialog = new ScheduleCellEditorForm(source, period, dayName);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _scheduleCellDrafts[key] = ScheduleEngine.CloneCell(dialog.EditedCell);
        row.Cells[columnName].Value = dialog.EditedCell.Kind == ScheduleCellKind.Empty
            ? string.Empty
            : dialog.EditedCell.Course;
        _scheduleGrid.InvalidateCell(columnIndex, rowIndex);
        _statusLabel.Text = dialog.EditedCell.Kind == ScheduleCellKind.Empty
            ? dayName + "的该课程格已设为空课。"
            : dayName + "的课程格设置已更新。";
        NotifyDraftChanged();
    }

    private void FormatScheduleCell(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0
            || eventArgs.ColumnIndex < 0
            || eventArgs.RowIndex >= _scheduleGrid.Rows.Count)
        {
            return;
        }

        var columnName = _scheduleGrid.Columns[eventArgs.ColumnIndex].Name;
        if (!columnName.StartsWith("day", StringComparison.Ordinal)
            || !int.TryParse(columnName.AsSpan(3), out var dayIndex))
        {
            return;
        }

        var row = _scheduleGrid.Rows[eventArgs.RowIndex];
        if (row.Tag is not string periodId)
        {
            return;
        }

        var rawCourse = Convert.ToString(row.Cells[eventArgs.ColumnIndex].Value)?.Trim() ?? string.Empty;
        if (!_scheduleCellDrafts.TryGetValue((periodId, dayIndex), out var cell))
        {
            eventArgs.Value = string.IsNullOrWhiteSpace(rawCourse) ? "—" : rawCourse;
            eventArgs.FormattingApplied = true;
            return;
        }

        var gridCell = row.Cells[eventArgs.ColumnIndex];
        gridCell.ToolTipText = string.Empty;
        if (cell.Kind == ScheduleCellKind.Empty || string.IsNullOrWhiteSpace(rawCourse))
        {
            eventArgs.Value = "—";
            if (eventArgs.CellStyle is { } emptyStyle)
            {
                emptyStyle.ForeColor = Muted;
            }
        }
        else
        {
            if (eventArgs.CellStyle is { } filledStyle)
            {
                filledStyle.ForeColor = Ink;
            }

            var hasOverride = !string.IsNullOrWhiteSpace(cell.StartOverride)
                && !string.IsNullOrWhiteSpace(cell.EndOverride);
            eventArgs.Value = rawCourse + (hasOverride ? "  ⏱" : string.Empty);
            if (hasOverride)
            {
                gridCell.ToolTipText = "特殊时间 " + cell.StartOverride + "–" + cell.EndOverride;
            }
        }

        eventArgs.FormattingApplied = true;
    }

    private SchedulePeriodConfig CreatePeriodFromGridRow(
        DataGridViewRow row,
        ScheduleConfig sourceSchedule)
    {
        var periodId = row.Tag as string;
        if (string.IsNullOrWhiteSpace(periodId))
        {
            periodId = "period-" + Guid.NewGuid().ToString("N");
            row.Tag = periodId;
        }

        var label = Convert.ToString(row.Cells["label"].Value)?.Trim() ?? string.Empty;
        var start = Convert.ToString(row.Cells["start"].Value)?.Trim() ?? string.Empty;
        var end = Convert.ToString(row.Cells["end"].Value)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException("课节名称不能为空。");
        }
        if (!ScheduleEngine.TryParseTimeRange(start, end, out _, out _))
        {
            throw new InvalidOperationException("课节“" + label + "”的默认时间无效。");
        }

        var existing = sourceSchedule.Periods
            .FirstOrDefault(period => period.Id.Equals(periodId, StringComparison.OrdinalIgnoreCase));
        return new SchedulePeriodConfig
        {
            Id = periodId,
            Label = label,
            Start = start,
            End = end,
            Note = existing?.Note,
            Kind = Convert.ToBoolean(row.Cells["break"].Value ?? false)
                ? ScheduleCellKind.Break
                : ScheduleCellKind.Class
        };
    }

    private void ReadScheduleIntoConfig(AppConfig target)
    {
        var periodRows = new List<(SchedulePeriodConfig Period, DataGridViewRow Row)>();
        foreach (DataGridViewRow row in _scheduleGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var label = Convert.ToString(row.Cells["label"].Value)?.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            periodRows.Add((CreatePeriodFromGridRow(row, target.Schedule), row));
        }

        var days = Enumerable.Range(0, 5)
            .Select(dayIndex => new ScheduleDayConfig { DayIndex = dayIndex })
            .ToList();

        foreach (var (period, row) in periodRows)
        {
            for (var dayIndex = 0; dayIndex < 5; dayIndex++)
            {
                var key = (period.Id, dayIndex);
                var cell = _scheduleCellDrafts.TryGetValue(key, out var configuredCell)
                    ? ScheduleEngine.CloneCell(configuredCell)
                    : new ScheduleCellConfig { PeriodId = period.Id };

                var course = Convert.ToString(row.Cells["day" + dayIndex].Value)?.Trim() ?? string.Empty;
                if (course is "—" or "-")
                {
                    course = string.Empty;
                }

                cell.PeriodId = period.Id;
                cell.Course = course;
                if (string.IsNullOrWhiteSpace(course))
                {
                    cell.Kind = ScheduleCellKind.Empty;
                    cell.StartOverride = null;
                    cell.EndOverride = null;
                }
                else if (cell.Kind == ScheduleCellKind.Empty)
                {
                    cell.Kind = period.Kind == ScheduleCellKind.Break
                        ? ScheduleCellKind.Break
                        : ScheduleCellKind.Class;
                }

                days[dayIndex].Cells.Add(cell);
                _scheduleCellDrafts[key] = ScheduleEngine.CloneCell(cell);
            }
        }

        target.Schedule.Periods = periodRows.Select(item => item.Period).ToList();
        target.Schedule.Days = days;
        target.Schedule.Rows = null;
    }

    private void ReadDateOverridesIntoConfig(AppConfig target)
    {
        var overrides = new List<ScheduleDateOverrideConfig>();
        foreach (DataGridViewRow row in _dateOverridesGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var dateText = Convert.ToString(row.Cells["date"].Value)?.Trim();
            if (string.IsNullOrWhiteSpace(dateText))
            {
                continue;
            }
            if (!TryParseExactDate(dateText, out var date))
            {
                throw new InvalidOperationException("特殊日期“" + dateText + "”无效，请使用 yyyy-MM-dd 格式。");
            }

            var baseDayIndex = ReadBaseDayIndex(row, date);
            overrides.Add(new ScheduleDateOverrideConfig
            {
                Date = date.Date,
                Label = Convert.ToString(row.Cells["label"].Value)?.Trim() ?? string.Empty,
                IsDayOff = Convert.ToBoolean(row.Cells["dayOff"].Value ?? false),
                BaseDayIndex = baseDayIndex,
                Cells = row.Tag is List<ScheduleCellConfig> cells
                    ? cells.Select(ScheduleEngine.CloneCell).ToList()
                    : []
            });
        }

        target.Schedule.DateOverrides = overrides;
    }

    private void EditDateOverrideSchedule()
    {
        var row = _dateOverridesGrid.CurrentRow;
        if (row is null || row.IsNewRow)
        {
            _statusLabel.Text = "请先选择一条特殊日期规则。";
            return;
        }

        try
        {
            var dateText = Convert.ToString(row.Cells["date"].Value)?.Trim();
            if (!TryParseExactDate(dateText, out var date))
            {
                throw new InvalidOperationException("特殊日期“" + dateText + "”无效，请使用 yyyy-MM-dd 格式。");
            }
            if (Convert.ToBoolean(row.Cells["dayOff"].Value ?? false))
            {
                MessageBox.Show(
                    this,
                    "该日期已设为停课。若要编辑当天课程，请先取消“停课”。",
                    "当天无需课程覆盖",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var config = SynchronizeDraftOrThrow();
            var baseDayIndex = ReadBaseDayIndex(row, date);
            var sourceCells = row.Tag is List<ScheduleCellConfig> cells
                ? cells.Select(ScheduleEngine.CloneCell).ToList()
                : [];

            using var dialog = new ScheduleDateOverrideEditorForm(
                config.Schedule,
                date.Date,
                baseDayIndex,
                sourceCells);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            row.Tag = dialog.EditedCells.Select(ScheduleEngine.CloneCell).ToList();
            row.Cells["overrides"].Value = dialog.EditedCells.Count + " 项";
            _statusLabel.Text = date.ToString("yyyy-MM-dd") + " 的课程覆盖已更新。";
            NotifyDraftChanged();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "无法编辑当天课程", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int? ReadBaseDayIndex(DataGridViewRow row, DateTime date)
    {
        var baseDayCell = row.Cells["baseDay"];
        var baseDayText = Convert.ToString(baseDayCell.Value)?.Trim();
        if (string.IsNullOrWhiteSpace(baseDayText) || baseDayText == "当天")
        {
            return null;
        }
        if (baseDayCell.Tag is int storedIndex && storedIndex is >= 0 and < 5)
        {
            return storedIndex;
        }

        var names = _weekdayInputs.Select(input => input.Text.Trim()).ToList();
        var matchedIndex = names.FindIndex(name =>
            name.Equals(baseDayText, StringComparison.OrdinalIgnoreCase));
        if (matchedIndex < 0)
        {
            var canonicalNames = new[] { "周一", "周二", "周三", "周四", "周五" };
            matchedIndex = Array.FindIndex(canonicalNames, name =>
                name.Equals(baseDayText, StringComparison.OrdinalIgnoreCase));
        }
        if (matchedIndex < 0)
        {
            throw new InvalidOperationException(
                "特殊日期“" + date.ToString("yyyy-MM-dd")
                + "”的套用星期无效，请填写“当天”或当前工作日名称。");
        }

        return matchedIndex;
    }

    private void AddDateOverride()
    {
        var usedDates = new HashSet<DateTime>();
        foreach (DataGridViewRow row in _dateOverridesGrid.Rows)
        {
            if (!row.IsNewRow
                && TryParseExactDate(Convert.ToString(row.Cells["date"].Value), out var existingDate))
            {
                usedDates.Add(existingDate.Date);
            }
        }

        var date = DateTime.Today.AddDays(1);
        while (usedDates.Contains(date.Date))
        {
            date = date.AddDays(1);
        }

        var rowIndex = _dateOverridesGrid.Rows.Add(
            date.ToString("yyyy-MM-dd"),
            "临时安排",
            false,
            "当天",
            "0 项");
        _dateOverridesGrid.Rows[rowIndex].Tag = new List<ScheduleCellConfig>();
        NotifyDraftChanged();
    }

    private void RemoveDateOverride()
    {
        foreach (DataGridViewRow row in _dateOverridesGrid.SelectedRows)
        {
            if (!row.IsNewRow)
            {
                _dateOverridesGrid.Rows.Remove(row);
            }
        }
        NotifyDraftChanged();
    }
}
