namespace CountdownWallpaper;

internal sealed class SettingsForm : Form
{
    private static readonly Color Paper = Color.FromArgb(246, 239, 228);
    private static readonly Color PaperBright = Color.FromArgb(255, 251, 243);
    private static readonly Color Wine = Color.FromArgb(109, 31, 42);
    private static readonly Color Ink = Color.FromArgb(39, 31, 27);
    private static readonly Color Gold = Color.FromArgb(186, 151, 91);

    private const int MinimumHalfPercent = 6;
    private const int MaximumHalfPercent = 50;
    private const int DefaultHalfPercent = 43;

    private readonly TrackBar _positionBar;
    private readonly Label _positionValue;
    private readonly Label _backgroundValue;
    private readonly CheckBox _autoStartCheckBox;
    private readonly string _initialBackgroundPath;

    public string SelectedBackgroundPath { get; private set; }
    public float SelectedXPercent => _positionBar.Value / 2f;
    public bool AutoStartEnabled => _autoStartCheckBox.Checked;
    public bool RestoreOriginalRequested { get; private set; }
    public bool ExitRequested { get; private set; }

    public SettingsForm(AppSettings settings, bool autoStartEnabled)
    {
        SelectedBackgroundPath = settings.BackgroundPath;
        _initialBackgroundPath = settings.BackgroundPath;

        Text = "郑老师中考倒计时 · 启动设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(760, 540);
        BackColor = Paper;
        ForeColor = Ink;
        Font = new Font("Microsoft YaHei", 10f, FontStyle.Regular, GraphicsUnit.Point);
        Padding = new Padding(22);

        _positionBar = new TrackBar
        {
            Minimum = MinimumHalfPercent,
            Maximum = MaximumHalfPercent,
            TickFrequency = 4,
            SmallChange = 1,
            LargeChange = 4,
            Value = Math.Clamp((int)Math.Round(settings.ScheduleXPercent * 2f), MinimumHalfPercent, MaximumHalfPercent),
            Location = new Point(14, 88),
            Size = new Size(320, 42),
            BackColor = PaperBright
        };
        _positionBar.ValueChanged += (_, _) => UpdatePositionLabel();

        _positionValue = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(14, 127),
            Size = new Size(320, 25),
            ForeColor = Wine
        };

        _backgroundValue = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Text = DisplayBackgroundName(SelectedBackgroundPath),
            Location = new Point(104, 50),
            Size = new Size(214, 28),
            ForeColor = Wine
        };

        _autoStartCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "开机自动启动",
            Checked = autoStartEnabled,
            Location = new Point(14, 125),
            ForeColor = Ink,
            BackColor = PaperBright
        };

        BuildControls();
        UpdatePositionLabel();
        Paint += OnPaintForm;
    }

    private void BuildControls()
    {
        var eyebrow = new Label
        {
            AutoSize = false,
            Text = "WALLPAPER CONTROL / 01",
            Font = new Font("Georgia", 9f, FontStyle.Regular),
            ForeColor = Gold,
            Location = new Point(42, 25),
            Size = new Size(400, 22)
        };

        var title = new Label
        {
            AutoSize = false,
            Text = "启动设置",
            Font = new Font("SimSun", 26f, FontStyle.Bold),
            ForeColor = Wine,
            Location = new Point(40, 44),
            Size = new Size(300, 44)
        };

        var subtitle = new Label
        {
            AutoSize = false,
            Text = "把每天的桌面，调成你愿意抬头看见的样子。",
            Location = new Point(42, 88),
            Size = new Size(560, 26),
            ForeColor = Color.FromArgb(105, 88, 77)
        };

        var layoutPanel = CreatePanel(new Rectangle(28, 125, 350, 250));
        layoutPanel.Controls.Add(CreateSectionTitle("课表位置", new Point(14, 12)));
        layoutPanel.Controls.Add(CreateLabel("只改变水平位置，纵向高度与大小保持锁定。", new Point(14, 47), new Size(320, 36), 9.5f, Color.FromArgb(105, 88, 77)));
        layoutPanel.Controls.Add(_positionBar);
        layoutPanel.Controls.Add(_positionValue);
        var resetButton = CreateButton("恢复默认", new Point(14, 205), new Size(100, 30), false);
        resetButton.Click += (_, _) => _positionBar.Value = DefaultHalfPercent;
        layoutPanel.Controls.Add(resetButton);

        var appearancePanel = CreatePanel(new Rectangle(394, 125, 338, 250));
        appearancePanel.Controls.Add(CreateSectionTitle("底图与启动", new Point(14, 12)));
        appearancePanel.Controls.Add(CreateLabel("当前底图", new Point(14, 50), new Size(90, 25), 9.5f, Color.FromArgb(105, 88, 77)));
        appearancePanel.Controls.Add(_backgroundValue);
        var chooseButton = CreateButton("更换底图", new Point(14, 88), new Size(108, 32), false);
        chooseButton.Click += (_, _) => ChooseBackground();
        appearancePanel.Controls.Add(chooseButton);
        var defaultButton = CreateButton("恢复默认底图", new Point(132, 88), new Size(124, 32), false);
        defaultButton.Click += (_, _) => SetDefaultBackground();
        appearancePanel.Controls.Add(defaultButton);
        appearancePanel.Controls.Add(_autoStartCheckBox);
        appearancePanel.Controls.Add(CreateLabel("课表、倒计时和排版不会随底图改变。", new Point(14, 184), new Size(300, 32), 9.5f, Color.FromArgb(105, 88, 77)));

        var infoPanel = CreatePanel(new Rectangle(28, 390, 704, 70));
        infoPanel.Controls.Add(CreateLabel("每天自动更新", new Point(16, 12), new Size(120, 24), 10f, Wine, true));
        infoPanel.Controls.Add(CreateLabel("HH:mm 实时时间 · 倒计时数字 · 进度条 · 当天星期高亮 · 10 句激励语轮换", new Point(142, 12), new Size(530, 24), 10f, Ink));
        infoPanel.Controls.Add(CreateLabel("右侧倒计时区域和课表是固定设计层，只有最底层背景可以替换。", new Point(16, 37), new Size(660, 22), 9f, Color.FromArgb(105, 88, 77)));

        var folderButton = CreateButton("打开壁纸文件夹", new Point(28, 486), new Size(132, 32), false);
        folderButton.Click += (_, _) => OpenWallpaperDirectory();
        var restoreButton = CreateButton("停用并恢复原壁纸", new Point(170, 486), new Size(152, 32), false);
        restoreButton.ForeColor = Wine;
        restoreButton.Click += (_, _) =>
        {
            RestoreOriginalRequested = true;
            DialogResult = DialogResult.Abort;
            Close();
        };
        var exitButton = CreateButton("退出并保留壁纸", new Point(332, 486), new Size(142, 32), false);
        exitButton.Click += (_, _) =>
        {
            ExitRequested = true;
            DialogResult = DialogResult.Abort;
            Close();
        };
        var cancelButton = CreateButton("取消", new Point(536, 486), new Size(80, 32), false);
        cancelButton.DialogResult = DialogResult.Cancel;
        var applyButton = CreateButton("应用设置", new Point(624, 486), new Size(108, 32), true);
        applyButton.DialogResult = DialogResult.OK;

        Controls.AddRange([
            eyebrow, title, subtitle, layoutPanel, appearancePanel, infoPanel,
            folderButton, restoreButton, exitButton, cancelButton, applyButton
        ]);
        AcceptButton = applyButton;
        CancelButton = cancelButton;
    }

    private void ChooseBackground()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择新的底图",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp|JPEG 图片|*.jpg;*.jpeg|PNG 图片|*.png|BMP 图片|*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SelectedBackgroundPath = AppStorage.ImportBackground(dialog.FileName);
            _backgroundValue.Text = DisplayBackgroundName(SelectedBackgroundPath);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "更换底图失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetDefaultBackground()
    {
        SelectedBackgroundPath = AppStorage.DefaultBackgroundPath;
        _backgroundValue.Text = DisplayBackgroundName(SelectedBackgroundPath);
    }

    private void UpdatePositionLabel()
    {
        var percent = SelectedXPercent;
        var direction = percent < 21.5f ? "偏左" : percent > 21.5f ? "偏右" : "默认";
        _positionValue.Text = $"左缘距屏幕：{percent:0.0}% · {direction}";
    }

    private static Panel CreatePanel(Rectangle bounds)
    {
        return new Panel
        {
            Bounds = bounds,
            BackColor = PaperBright,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static Label CreateSectionTitle(string text, Point location)
    {
        return new Label
        {
            AutoSize = false,
            Text = text,
            Font = new Font("Microsoft YaHei", 12f, FontStyle.Bold),
            ForeColor = Wine,
            Location = location,
            Size = new Size(260, 28)
        };
    }

    private static Label CreateLabel(string text, Point location, Size size, float fontSize, Color color, bool bold = false)
    {
        return new Label
        {
            AutoSize = false,
            Text = text,
            Font = new Font("Microsoft YaHei", fontSize, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color,
            Location = location,
            Size = size
        };
    }

    private static Button CreateButton(string text, Point location, Size size, bool primary)
    {
        var button = new Button
        {
            AutoSize = false,
            Text = text,
            Location = location,
            Size = size,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Wine : PaperBright,
            ForeColor = primary ? PaperBright : Ink,
            Font = new Font("Microsoft YaHei", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
            TabStop = true,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? Wine : Gold;
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(132, 40, 55) : Color.FromArgb(250, 239, 216);
        return button;
    }

    private static string DisplayBackgroundName(string path)
    {
        if (path.Equals(AppStorage.DefaultBackgroundPath, StringComparison.OrdinalIgnoreCase))
        {
            return "内置默认底图";
        }

        return Path.GetFileName(path);
    }

    private static void OpenWallpaperDirectory()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AppStorage.RootDirectory,
            UseShellExecute = true
        });
    }

    private void OnPaintForm(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var shadowBrush = new SolidBrush(Color.FromArgb(34, 39, 31, 27));
        using var borderPen = new Pen(Wine, 3f);
        using var accentPen = new Pen(Gold, 2f);
        e.Graphics.FillRectangle(shadowBrush, 8, 8, ClientSize.Width - 20, ClientSize.Height - 20);
        e.Graphics.DrawRectangle(borderPen, 2, 2, ClientSize.Width - 6, ClientSize.Height - 6);
        e.Graphics.DrawLine(accentPen, 42, 113, 180, 113);
    }
}
