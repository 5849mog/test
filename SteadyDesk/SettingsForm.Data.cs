namespace SteadyDesk;

internal sealed partial class SettingsForm
{
    private void WireLivePreviewEvents()
    {
        foreach (var textBox in new[] { _titleBox, _eyebrowBox, _countdownTitleBox, _quotesBox })
        {
            textBox.TextChanged += (_, _) => NotifyDraftChanged();
        }
        foreach (var textBox in _weekdayInputs)
        {
            textBox.TextChanged += (_, _) => NotifyDraftChanged();
        }
        foreach (var textBox in _themeInputs.Values)
        {
            textBox.TextChanged += (_, _) => NotifyDraftChanged();
        }
        foreach (var trackBar in new[] { _schedulePosition, _countdownPosition, _countdownWidth })
        {
            trackBar.ValueChanged += (_, _) => NotifyDraftChanged();
        }
        foreach (var checkBox in new[] { _showClock, _showProgress, _showQuote, _autoStart })
        {
            checkBox.CheckedChanged += (_, _) => NotifyDraftChanged();
        }

        _preparationStartDate.ValueChanged += (_, _) => NotifyDraftChanged();
        _refreshInterval.ValueChanged += (_, _) => NotifyDraftChanged();

        WireGridChanges(_eventsGrid);
        WireGridChanges(_scheduleGrid);
        WireGridChanges(_dateOverridesGrid);

        _previewPane.OptionsChanged += (_, _) =>
        {
            _previewPane.ShowRefreshing();
            _previewCoordinator.RequestRefresh();
        };
        _previewPane.RefreshRequested += (_, _) => _previewCoordinator.RefreshNow();
    }

    private void WireGridChanges(DataGridView grid)
    {
        grid.CellValueChanged += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0)
            {
                NotifyDraftChanged();
            }
        };
        grid.CellEndEdit += (_, _) => NotifyDraftChanged();
        grid.RowsAdded += (_, _) => NotifyDraftChanged();
        grid.RowsRemoved += (_, _) => NotifyDraftChanged();
    }

    private void LoadDraftIntoControls(bool markAsChanged = false)
    {
        var draft = _draftController.Snapshot();
        _loadingControls = true;
        try
        {
            _schedulePosition.Value = Math.Clamp((int)Math.Round(draft.Wallpaper.ScheduleXPercent * 2f), 6, 50);
            _countdownPosition.Value = Math.Clamp((int)Math.Round(draft.Wallpaper.CountdownXPercent), 50, 70);
            _countdownWidth.Value = Math.Clamp((int)Math.Round(draft.Wallpaper.CountdownWidthPercent), 25, 44);
            _titleBox.Text = draft.Content.ScheduleTitle;
            _eyebrowBox.Text = draft.Content.Eyebrow;
            _countdownTitleBox.Text = draft.Content.CountdownTitle;

            var preparationDate = draft.Content.PreparationStartDate.Date;
            if (preparationDate < _preparationStartDate.MinDate)
            {
                preparationDate = _preparationStartDate.MinDate;
            }
            else if (preparationDate > _preparationStartDate.MaxDate)
            {
                preparationDate = _preparationStartDate.MaxDate;
            }

            _preparationStartDate.Value = preparationDate;
            _refreshInterval.Value = Math.Clamp(draft.Behavior.RefreshIntervalSeconds, 5, 300);
            _autoStart.Checked = draft.Behavior.AutoStart;
            _showClock.Checked = draft.Wallpaper.ShowClock;
            _showProgress.Checked = draft.Wallpaper.ShowProgress;
            _showQuote.Checked = draft.Wallpaper.ShowQuote;
            _backgroundLabel.Text = GetBackgroundName(draft.Wallpaper.BackgroundPath);
            _quotesBox.Text = string.Join(
                Environment.NewLine + Environment.NewLine,
                draft.Content.Quotes);

            for (var index = 0; index < _weekdayInputs.Length; index++)
            {
                _weekdayInputs[index].Text = index < draft.Schedule.Weekdays.Count
                    ? draft.Schedule.Weekdays[index]
                    : string.Empty;
            }

            foreach (var themeName in ThemeNames)
            {
                _themeInputs[themeName].Text = GetThemeValue(draft.Theme, themeName);
            }

            LoadEventGrid(draft);
            LoadScheduleGrid(draft);
            LoadDateOverridesGrid(draft);
        }
        finally
        {
            _loadingControls = false;
        }

        _hasUnsavedChanges = markAsChanged;
        _statusLabel.Text = markAsChanged
            ? "配置已载入 · 更改尚未应用到桌面"
            : "设置已载入 · 右侧预览会随修改自动更新";
    }

    private void LoadEventGrid(AppConfig config)
    {
        _eventsGrid.Rows.Clear();
        foreach (var item in config.Events)
        {
            var rowIndex = _eventsGrid.Rows.Add(
                item.Title,
                item.Date.ToString("yyyy-MM-dd"),
                item.Visible,
                item.ShowProgress,
                item.Color);
            _eventsGrid.Rows[rowIndex].Tag = item.Id;
        }
    }

    private void LoadScheduleGrid(AppConfig config)
    {
        _scheduleGrid.Rows.Clear();
        _scheduleCellDrafts.Clear();

        for (var index = 0; index < 5; index++)
        {
            _scheduleGrid.Columns["day" + index].HeaderText = index < config.Schedule.Weekdays.Count
                ? config.Schedule.Weekdays[index]
                : "—";
        }

        foreach (var day in config.Schedule.Days)
        {
            foreach (var cell in day.Cells)
            {
                _scheduleCellDrafts[(cell.PeriodId, day.DayIndex)] = ScheduleEngine.CloneCell(cell);
            }
        }

        foreach (var period in config.Schedule.Periods)
        {
            var values = new object[9];
            values[0] = period.Label;
            values[1] = period.Start;
            values[2] = period.End;
            values[3] = period.Kind == ScheduleCellKind.Break;
            for (var dayIndex = 0; dayIndex < 5; dayIndex++)
            {
                values[4 + dayIndex] = _scheduleCellDrafts.TryGetValue((period.Id, dayIndex), out var cell)
                    && cell.Kind != ScheduleCellKind.Empty
                        ? cell.Course
                        : string.Empty;
            }

            var rowIndex = _scheduleGrid.Rows.Add(values);
            _scheduleGrid.Rows[rowIndex].Tag = period.Id;
        }

        _scheduleGrid.Invalidate();
    }

    private void LoadDateOverridesGrid(AppConfig config)
    {
        _dateOverridesGrid.Rows.Clear();
        foreach (var item in config.Schedule.DateOverrides.OrderBy(item => item.Date))
        {
            var baseDay = item.BaseDayIndex is >= 0 and < 5
                ? config.Schedule.Weekdays[item.BaseDayIndex.Value]
                : "当天";
            var rowIndex = _dateOverridesGrid.Rows.Add(
                item.Date.ToString("yyyy-MM-dd"),
                item.Label,
                item.IsDayOff,
                baseDay,
                item.Cells.Count + " 项");
            var row = _dateOverridesGrid.Rows[rowIndex];
            row.Tag = item.Cells.Select(ScheduleEngine.CloneCell).ToList();
            row.Cells["baseDay"].Tag = item.BaseDayIndex;
        }
    }

    private void PopulateConfigFromControls(AppConfig target)
    {
        target.Wallpaper.ScheduleXPercent = _schedulePosition.Value / 2f;
        target.Wallpaper.CountdownXPercent = _countdownPosition.Value;
        target.Wallpaper.CountdownWidthPercent = _countdownWidth.Value;
        target.Wallpaper.ShowClock = _showClock.Checked;
        target.Wallpaper.ShowProgress = _showProgress.Checked;
        target.Wallpaper.ShowQuote = _showQuote.Checked;
        target.Content.ScheduleTitle = _titleBox.Text.Trim();
        target.Content.Eyebrow = _eyebrowBox.Text.Trim();
        target.Content.CountdownTitle = _countdownTitleBox.Text.Trim();
        target.Content.PreparationStartDate = _preparationStartDate.Value.Date;
        target.Content.Quotes = ParseQuotes(_quotesBox.Text);
        target.Schedule.Weekdays = _weekdayInputs.Select(input => input.Text.Trim()).ToList();
        if (target.Schedule.Weekdays.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("工作日名称不能为空。");
        }
        if (target.Schedule.Weekdays.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 5)
        {
            throw new InvalidOperationException("五个工作日名称不能重复。");
        }

        target.Theme.Wine = ReadColor(_themeInputs[nameof(ThemeConfig.Wine)], "Wine");
        target.Theme.WineDeep = ReadColor(_themeInputs[nameof(ThemeConfig.WineDeep)], "WineDeep");
        target.Theme.Paper = ReadColor(_themeInputs[nameof(ThemeConfig.Paper)], "Paper");
        target.Theme.Ink = ReadColor(_themeInputs[nameof(ThemeConfig.Ink)], "Ink");
        target.Theme.InkSoft = ReadColor(_themeInputs[nameof(ThemeConfig.InkSoft)], "InkSoft");
        target.Theme.Gold = ReadColor(_themeInputs[nameof(ThemeConfig.Gold)], "Gold");
        target.Theme.Champagne = ReadColor(_themeInputs[nameof(ThemeConfig.Champagne)], "Champagne");

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
            if (!TryParseExactDate(dateText, out var date))
            {
                throw new InvalidOperationException("事件“" + title + "”的日期无效，请使用 yyyy-MM-dd 格式。");
            }

            var color = Convert.ToString(row.Cells["color"].Value)?.Trim();
            if (string.IsNullOrWhiteSpace(color))
            {
                color = "#B69A68";
            }
            ValidateColor(color, "事件“" + title + "”的颜色");

            var id = row.Tag as string;
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
                row.Tag = id;
            }
            events.Add(new CountdownEventConfig
            {
                Id = id,
                Title = title,
                Date = date.Date,
                Visible = Convert.ToBoolean(row.Cells["visible"].Value ?? true),
                ShowProgress = Convert.ToBoolean(row.Cells["progress"].Value ?? true),
                Color = color
            });
        }
        target.Events = events;

        ReadScheduleIntoConfig(target);
        ReadDateOverridesIntoConfig(target);
        target.Behavior.AutoStart = _autoStart.Checked;
        target.Behavior.RefreshIntervalSeconds = (int)_refreshInterval.Value;
    }

    private PreviewRenderRequest CreatePreviewRequest()
    {
        if (!_draftController.TryUpdate(
                PopulateConfigFromControls,
                out var snapshot,
                out var error))
        {
            throw new InvalidOperationException(error ?? "设置尚未完成。");
        }

        var options = _previewPane.Options;
        var scenario = PreviewScenarioFactory.Create(snapshot, options);
        return new PreviewRenderRequest(
            scenario.Config,
            options.Width,
            options.Height,
            scenario.Moment,
            scenario.Label);
    }

    private AppConfig SynchronizeDraftOrThrow()
    {
        CommitGridEdits();
        return _draftController.UpdateOrThrow(PopulateConfigFromControls);
    }

    private void CommitGridEdits()
    {
        _eventsGrid.EndEdit();
        _scheduleGrid.EndEdit();
        _dateOverridesGrid.EndEdit();
    }

    private void ApplyAndClose()
    {
        try
        {
            var config = SynchronizeDraftOrThrow();
            if (_pendingBackgroundPath is not null)
            {
                var committedPath = AppStorage.CommitBackground(_pendingBackgroundPath);
                _pendingBackgroundPath = null;
                config = _draftController.UpdateOrThrow(candidate =>
                    candidate.Wallpaper.BackgroundPath = committedPath);
            }

            EditedConfig = config.Clone();
            _hasUnsavedChanges = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "设置无效", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    private static bool TryParseExactDate(string? value, out DateTime date)
    {
        return DateTime.TryParseExact(
            value?.Trim(),
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out date);
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
}
