namespace SteadyDesk;

internal sealed partial class SettingsForm : Form
{
    private static readonly Color Paper = SettingsPalette.Paper;
    private static readonly Color PaperBright = SettingsPalette.PaperBright;
    private static readonly Color Wine = SettingsPalette.Wine;
    private static readonly Color Ink = SettingsPalette.Ink;
    private static readonly Color Muted = SettingsPalette.Muted;
    private static readonly Color Gold = SettingsPalette.Gold;
    private static readonly Color Line = SettingsPalette.Line;
    private static readonly Color PaperInset = SettingsPalette.PaperInset;

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

    private readonly SettingsDraftController _draftController;
    private readonly SettingsPreviewPane _previewPane;
    private readonly PreviewCoordinator _previewCoordinator;
    private readonly TrackBar _schedulePosition;
    private readonly TrackBar _countdownPosition;
    private readonly TrackBar _countdownWidth;
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
    private readonly DataGridView _dateOverridesGrid;
    private readonly Dictionary<(string PeriodId, int DayIndex), ScheduleCellConfig> _scheduleCellDrafts = [];
    private readonly DateTimePicker _preparationStartDate;
    private readonly NumericUpDown _refreshInterval;
    private readonly TextBox _quotesBox;
    private readonly TextBox[] _weekdayInputs;
    private readonly Dictionary<string, TextBox> _themeInputs = new(StringComparer.OrdinalIgnoreCase);

    private string? _pendingBackgroundPath;
    private bool _loadingControls;
    private bool _hasUnsavedChanges;

    private sealed class ConfigPackage
    {
        public int PackageVersion { get; set; } = 2;
        public AppConfig Config { get; set; } = AppConfig.CreateDefault();
        public string? BackgroundFileName { get; set; }
    }

    public AppConfig EditedConfig { get; private set; }
    public bool AutoStartEnabled => _autoStart.Checked;
    public bool RestoreOriginalRequested { get; private set; }
    public bool ExitRequested { get; private set; }

    public SettingsForm(AppConfig source, bool autoStartEnabled)
    {
        var initial = source.Clone();
        initial.Behavior.AutoStart = autoStartEnabled;
        _draftController = new SettingsDraftController(initial);
        EditedConfig = initial.Clone();

        Text = "稳序桌面 · 控制中心";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1060, 680);
        ClientSize = new Size(1380, 820);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Paper;
        ForeColor = Ink;
        Font = new Font("Microsoft YaHei", 10f);

        _previewPane = new SettingsPreviewPane();
        _schedulePosition = new TrackBar
        {
            Minimum = 6,
            Maximum = 50,
            TickFrequency = 4,
            SmallChange = 1,
            LargeChange = 4,
            Width = 410
        };
        _countdownPosition = new TrackBar
        {
            Minimum = 50,
            Maximum = 70,
            TickFrequency = 5,
            SmallChange = 1,
            LargeChange = 5,
            Width = 410
        };
        _countdownWidth = new TrackBar
        {
            Minimum = 25,
            Maximum = 44,
            TickFrequency = 4,
            SmallChange = 1,
            LargeChange = 4,
            Width = 410
        };
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
        _backgroundLabel = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Width = 290,
            Height = 30,
            ForeColor = Wine,
            BackColor = PaperInset,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(7, 5, 7, 4)
        };
        _statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Muted,
            BackColor = PaperBright,
            Padding = new Padding(9, 0, 9, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _eventsGrid = CreateGrid();
        _scheduleGrid = CreateGrid();
        _dateOverridesGrid = CreateGrid();

        ConfigureGridBehavior();
        BuildUi();
        ApplyControlTheme(this);

        _previewCoordinator = new PreviewCoordinator(
            CreatePreviewRequest,
            result =>
            {
                _previewPane.ShowPreview(result.Image, result.Summary);
                _statusLabel.Text = _hasUnsavedChanges
                    ? "实时预览已更新 · 更改尚未应用到桌面"
                    : "实时预览已更新";
            },
            message =>
            {
                _previewPane.ShowPending(message);
                _statusLabel.Text = message;
            });

        WireLivePreviewEvents();
        LoadDraftIntoControls();
        Shown += (_, _) => _previewCoordinator.RefreshNow();
    }

    private void ConfigureGridBehavior()
    {
        _scheduleGrid.CellDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0 && eventArgs.ColumnIndex >= 0)
            {
                EditScheduleCell(eventArgs.RowIndex, eventArgs.ColumnIndex);
            }
        };
        _scheduleGrid.CellFormatting += FormatScheduleCell;
        _scheduleGrid.CurrentCellDirtyStateChanged += (_, _) => CommitDirtyCheckBox(_scheduleGrid);
        _eventsGrid.CurrentCellDirtyStateChanged += (_, _) => CommitDirtyCheckBox(_eventsGrid);
        _dateOverridesGrid.CurrentCellDirtyStateChanged += (_, _) => CommitDirtyCheckBox(_dateOverridesGrid);

        _scheduleGrid.CellValueChanged += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0
                && eventArgs.ColumnIndex >= 0
                && _scheduleGrid.Columns[eventArgs.ColumnIndex].Name == "break")
            {
                SyncScheduleRowKind(eventArgs.RowIndex);
            }
        };
        _dateOverridesGrid.CellEndEdit += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0
                && eventArgs.ColumnIndex >= 0
                && _dateOverridesGrid.Columns[eventArgs.ColumnIndex].Name == "baseDay")
            {
                _dateOverridesGrid.Rows[eventArgs.RowIndex].Cells[eventArgs.ColumnIndex].Tag = null;
            }
        };
    }

    private static void CommitDirtyCheckBox(DataGridView grid)
    {
        if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
        {
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void NotifyDraftChanged()
    {
        if (_loadingControls || IsDisposed)
        {
            return;
        }

        _hasUnsavedChanges = true;
        _statusLabel.Text = "有未应用的更改 · 正在更新预览…";
        _previewPane.ShowRefreshing();
        _previewCoordinator.RequestRefresh();
    }

    internal void RenderPreviewForSnapshot()
    {
        _previewCoordinator.RefreshNow();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewCoordinator.Dispose();
            AppStorage.DeleteIfExists(_pendingBackgroundPath);
            _pendingBackgroundPath = null;
        }

        base.Dispose(disposing);
    }
}
