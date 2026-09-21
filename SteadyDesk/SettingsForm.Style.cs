namespace SteadyDesk;

internal sealed partial class SettingsForm
{
    private static TabPage CreatePage(string text)
    {
        return new TabPage(text)
        {
            BackColor = Paper,
            Padding = new Padding(12)
        };
    }

    private static void DrawTab(object? sender, DrawItemEventArgs eventArgs)
    {
        if (sender is not TabControl tabs
            || eventArgs.Index < 0
            || eventArgs.Index >= tabs.TabPages.Count)
        {
            return;
        }

        var selected = eventArgs.Index == tabs.SelectedIndex;
        var bounds = eventArgs.Bounds;
        using var fill = new SolidBrush(selected ? PaperBright : Paper);
        using var hairline = new Pen(selected ? Color.FromArgb(90, Line) : Color.FromArgb(36, Line), 1f);
        using var accent = new SolidBrush(Color.FromArgb(190, Wine));

        eventArgs.Graphics.FillRectangle(fill, bounds);
        eventArgs.Graphics.DrawLine(hairline, bounds.Left + 8, bounds.Bottom - 1, bounds.Right - 8, bounds.Bottom - 1);
        if (selected)
        {
            eventArgs.Graphics.FillRectangle(accent, bounds.Left + 18, bounds.Bottom - 3, bounds.Width - 36, 2);
        }

        TextRenderer.DrawText(
            eventArgs.Graphics,
            tabs.TabPages[eventArgs.Index].Text,
            tabs.Font,
            bounds,
            selected ? Wine : Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static void ApplyControlTheme(Control control)
    {
        switch (control)
        {
            case TextBox textBox:
                textBox.BackColor = PaperBright;
                textBox.ForeColor = Ink;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.Padding = new Padding(6, 3, 6, 3);
                break;
            case ComboBox comboBox:
                comboBox.BackColor = PaperBright;
                comboBox.ForeColor = Ink;
                break;
            case DateTimePicker datePicker:
                datePicker.BackColor = PaperBright;
                datePicker.ForeColor = Ink;
                datePicker.CalendarMonthBackground = PaperBright;
                datePicker.CalendarForeColor = Ink;
                datePicker.CalendarTitleBackColor = Wine;
                datePicker.CalendarTitleForeColor = PaperBright;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = PaperBright;
                numeric.ForeColor = Ink;
                numeric.BorderStyle = BorderStyle.FixedSingle;
                break;
            case TrackBar trackBar:
                trackBar.BackColor = PaperBright;
                break;
            case CheckBox checkBox:
                checkBox.BackColor = Color.Transparent;
                checkBox.ForeColor = Ink;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyControlTheme(child);
        }
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = PaperBright,
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Line,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AllowUserToResizeRows = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeight = 34,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            EnableHeadersVisualStyles = false,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = PaperInset,
                ForeColor = Wine,
                Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 6, 0)
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = PaperBright,
                ForeColor = Ink,
                SelectionBackColor = Color.FromArgb(232, 220, 201),
                SelectionForeColor = Ink,
                Font = new Font("Microsoft YaHei", 9.2f),
                Padding = new Padding(6, 3, 6, 3)
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(252, 248, 240)
            }
        };
        grid.RowTemplate.Height = 30;
        grid.DataError += (_, eventArgs) => eventArgs.ThrowException = false;
        return grid;
    }

    private static Label SectionTitle(string text)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 11.5f, FontStyle.Bold),
            ForeColor = Wine,
            Padding = new Padding(10, 0, 0, 0),
            Margin = new Padding(3, 12, 3, 6)
        };
        label.Paint += (_, eventArgs) =>
        {
            using var accent = new SolidBrush(Color.FromArgb(170, Gold));
            using var line = new Pen(Color.FromArgb(42, Line), 1f);
            eventArgs.Graphics.FillRectangle(accent, 0, 4, 3, Math.Max(8, label.Height - 8));
            eventArgs.Graphics.DrawLine(line, 10, label.Height - 1, label.Width, label.Height - 1);
        };
        return label;
    }

    private static Label MutedLabel(string text, int width, int height)
    {
        return new Label
        {
            Text = text,
            Width = width,
            Height = height,
            ForeColor = Muted,
            Font = new Font("Microsoft YaHei", 9.2f),
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
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? Wine : Gold;
        button.FlatAppearance.BorderSize = primary ? 2 : 1;
        button.FlatAppearance.MouseOverBackColor = primary
            ? Color.FromArgb(118, 36, 47)
            : Color.FromArgb(247, 241, 232);
        button.FlatAppearance.MouseDownBackColor = primary
            ? Color.FromArgb(92, 27, 35)
            : Color.FromArgb(238, 226, 207);
        return button;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        var row = panel.RowCount;
        panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = new Label
        {
            Text = label,
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9.2f),
            ForeColor = Muted,
            Margin = new Padding(4, 9, 12, 8)
        };
        control.Margin = new Padding(4, 5, 4, 5);
        panel.Controls.Add(title, 0, row);
        panel.Controls.Add(control, 1, row);
    }
}
