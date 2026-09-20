using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CountdownWallpaper;

internal static class WallpaperRenderer
{
    private static readonly DateTime MockExamDate = new(2027, 1, 12);
    private static readonly DateTime SeniorHighExamDate = new(2027, 6, 19);

    private static readonly Color Wine = Color.FromArgb(109, 31, 42);
    private static readonly Color WineDeep = Color.FromArgb(84, 21, 30);
    private static readonly Color Paper = Color.FromArgb(250, 246, 238);
    private static readonly Color Ink = Color.FromArgb(36, 30, 27);
    private static readonly Color InkSoft = Color.FromArgb(95, 82, 75);
    private static readonly Color Gold = Color.FromArgb(182, 154, 104);
    private static readonly Color Champagne = Color.FromArgb(229, 203, 151);
    private static readonly DateTime PreparationStartDate = new(2026, 9, 1);

    private static readonly string[] Weekdays = ["周一", "周二", "周三", "周四", "周五"];

    private static readonly string[] EncouragementQuotes =
    [
        "每一个清晨的坚持，都在为 6 月的答案加分。\n今天，也要稳稳地走一步。",
        "不急着和昨天比较。\n今天多做一点，六月就多一分底气。",
        "把会做的题做稳，\n把不会的题一点点变成会。",
        "每一次按时出发，\n都在靠近想去的六月。",
        "成绩会记录努力，\n时间会回答坚持。",
        "先完成今天的目标，\n再把目光放远一点。",
        "不慌，不乱，不停步。\n稳住节奏，稳住自己。",
        "现在的每一页，\n都会成为考场上的底牌。",
        "把基础打牢，把心态放稳，\n答案自然会越来越清楚。",
        "早起一点，专注一点，\n今天也会有新的收获。"
    ];

    private static readonly ScheduleRow[] Rows =
    [
        new("第一节课", "08:00–08:40", ["语文", "英语", "道法", "化学", "语文"]),
        new("第二节课", "08:55–09:35", ["语文", "英语", "数学", "语文", "语文"]),
        new("大课间", "09:35–10:05", ["做操 / 运动 / 学习", "", "", "", ""], true),
        new("第三节课", "10:05–10:45", ["道法", "数学", "化学", "英语", "物理"]),
        new("第四节课", "11:00–11:40", ["英语", "体育", "体育", "英语", "物理"]),
        new("第五节课", "11:55–12:35", ["英语", "数学", "英语", "美术（单）\n音乐（双）", "生物（单）\n地理（双）"]),
        new("第六节课", "12:40–13:20", ["—", "—", "—", "—", "13:20–14:00\n体育"]),
        new("第七节课", "13:35–14:15", ["物理", "语文", "语文", "体育", "14:15–14:55\n历史"]),
        new("第八节课", "14:30–15:10", ["历史", "化学", "语文", "物理", "15:10–15:50\n数学"]),
        new("第九节课", "15:25–16:05", ["数学", "化学", "历史", "数学", "16:00–16:40\n英语"]),
        new("第十节课", "16:15–16:55", ["数学", "物理", "道法", "数学", "16:50–17:30\n班会"]),
        new("第十一节课", "17:10–18:10\n晚托走班", ["物理（单）\n数学（双）", "体活", "化学（单）\n语文（双）", "历史/道法（单）\n英语（双）", "—"]),
        new("延时服务", "18:40–20:30", ["语文", "18:30–20:30\n物理（单）\n化学（双）", "数学", "英语", "—"])
    ];

    public static void Render(
        string backgroundPath,
        string outputPath,
        int width,
        int height,
        DateTime today,
        float scheduleXPercent = 21.5f,
        DateTime? nowOverride = null)
    {
        if (width < 800 || height < 450)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "屏幕分辨率过低，无法清晰显示完整课表。");
        }

        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(96, 96);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        DrawBackground(graphics, backgroundPath, width, height);
        DrawSchedule(graphics, width, height, scheduleXPercent, today, nowOverride ?? DateTime.Now);
        DrawCountdown(graphics, width, height, today.Date, nowOverride ?? DateTime.Now);
        SaveJpeg(bitmap, outputPath, 95L);
    }

    private static void DrawBackground(Graphics graphics, string backgroundPath, int width, int height)
    {
        graphics.Clear(Color.FromArgb(242, 235, 223));
        if (!File.Exists(backgroundPath))
        {
            return;
        }

        using var source = Image.FromFile(backgroundPath);
        var scale = Math.Max((float)width / source.Width, (float)height / source.Height);
        var cropWidth = width / scale;
        var cropHeight = height / scale;
        var sourceRect = new RectangleF(
            (source.Width - cropWidth) / 2f,
            (source.Height - cropHeight) / 2f,
            cropWidth,
            cropHeight);
        graphics.DrawImage(source, new Rectangle(0, 0, width, height), sourceRect, GraphicsUnit.Pixel);

        using var veil = new LinearGradientBrush(
            new Rectangle(0, 0, width, height),
            Color.FromArgb(20, Paper),
            Color.FromArgb(8, WineDeep),
            LinearGradientMode.Horizontal);
        graphics.FillRectangle(veil, 0, 0, width, height);
    }

    private static void DrawSchedule(Graphics graphics, int width, int height, float scheduleXPercent, DateTime today, DateTime now)
    {
        var scale = Math.Min(width / 1920f, height / 1080f);
        var scheduleX = Math.Clamp(scheduleXPercent, 3f, 25f) / 100f;
        // 课表与右侧倒计时面板上下对齐，同时略微收窄，给桌面图标留下更宽的安全区。
        var card = new RectangleF(width * scheduleX, height * .045f, width * .455f, height * .91f);
        var radius = 6f * scale;

        using (var shadowPath = RoundedRectangle(new RectangleF(card.X + 7f * scale, card.Y + 9f * scale, card.Width, card.Height), radius))
        using (var shadowBrush = new SolidBrush(Color.FromArgb(35, 73, 45, 33)))
        {
            graphics.FillPath(shadowBrush, shadowPath);
        }

        using (var cardPath = RoundedRectangle(card, radius))
        using (var cardBrush = new SolidBrush(Color.FromArgb(252, Paper)))
        using (var cardPen = new Pen(Color.FromArgb(48, Wine), Math.Max(1f, scale)))
        {
            graphics.FillPath(cardBrush, cardPath);
            graphics.DrawPath(cardPen, cardPath);
        }

        var padding = 28f * scale;
        var content = RectangleF.Inflate(card, -padding, -padding);
        var headerHeight = 105f * scale;
        var table = new RectangleF(content.X, content.Y + headerHeight, content.Width, content.Height - headerHeight);

        using var yearFont = MakeFont("Microsoft YaHei", 15.5f * scale, FontStyle.Regular);
        using var titleFont = MakeFont("SimSun", 38f * scale, FontStyle.Regular);
        using var yearBrush = new SolidBrush(Wine);
        using var titleBrush = new SolidBrush(Ink);
        using var centered = CenterFormat();
        graphics.DrawString("2026—2027 学年", yearFont, yearBrush, new RectangleF(content.X, content.Y, content.Width, 25f * scale), centered);
        graphics.DrawString("课程安排", titleFont, titleBrush, new RectangleF(content.X, content.Y + 24f * scale, content.Width, 55f * scale), centered);

        DrawScheduleTable(graphics, table, scale, today, now);
    }

    private static void DrawScheduleTable(Graphics graphics, RectangleF table, float scale, DateTime today, DateTime now)
    {
        var firstColumnWidth = table.Width * .20f;
        var dayColumnWidth = (table.Width - firstColumnWidth) / 5f;
        var rowHeight = table.Height / (Rows.Length + 1);
        var lineWidth = Math.Max(1f, scale * .7f);

        using var gridPen = new Pen(Color.FromArgb(46, InkSoft), lineWidth);
        using var borderPen = new Pen(Color.FromArgb(66, Wine), lineWidth);
        using var headerBrush = new SolidBrush(Color.FromArgb(22, Wine));
        using var leftBrush = new SolidBrush(Color.FromArgb(17, Gold));
        using var alternateBrush = new SolidBrush(Color.FromArgb(22, Gold));
        using var fridayBrush = new SolidBrush(Color.FromArgb(17, Wine));
        using var breakBrush = new SolidBrush(Color.FromArgb(24, Wine));
        using var headerFont = MakeFont("Microsoft YaHei", 17.5f * scale, FontStyle.Bold);
        using var firstHeaderFont = MakeFont("Microsoft YaHei", 15.5f * scale, FontStyle.Bold);
        using var courseFont = MakeFont("Microsoft YaHei", 17.5f * scale, FontStyle.Regular);
        using var courseSmallFont = MakeFont("Microsoft YaHei", 15.2f * scale, FontStyle.Regular);
        using var periodFont = MakeFont("Microsoft YaHei", 15.5f * scale, FontStyle.Bold);
        using var timeFont = MakeFont("Microsoft YaHei", 13.5f * scale, FontStyle.Regular);
        using var headerTextBrush = new SolidBrush(Wine);
        using var activeHeaderFill = new SolidBrush(Wine);
        using var activeHeaderBrush = new SolidBrush(Color.FromArgb(255, 235, 198));
        using var activeColumnBrush = new SolidBrush(Color.FromArgb(11, Champagne));
        using var activeCellBrush = new SolidBrush(Color.FromArgb(48, Champagne));
        using var activeCellPen = new Pen(Color.FromArgb(230, 188, 116), Math.Max(2f, 2.3f * scale));
        using var activeCellCornerPen = new Pen(Color.FromArgb(145, Wine), Math.Max(1f, 1.2f * scale));
        using var courseBrush = new SolidBrush(Ink);
        using var timeBrush = new SolidBrush(Wine);
        using var emptyBrush = new SolidBrush(Color.FromArgb(120, InkSoft));
        using var center = CenterFormat();

        var activeDay = GetWeekdayIndex(today);
        var activeRow = GetActiveRowIndex(now);
        var activeColumnX = activeDay >= 0
            ? table.X + firstColumnWidth + activeDay * dayColumnWidth
            : 0f;

        graphics.FillRectangle(headerBrush, table.X, table.Y, table.Width, rowHeight);
        graphics.FillRectangle(fridayBrush, table.Right - dayColumnWidth, table.Y, dayColumnWidth, rowHeight);
        if (activeDay >= 0)
        {
            graphics.FillRectangle(activeColumnBrush, activeColumnX, table.Y, dayColumnWidth, rowHeight);
        }
        graphics.DrawString("节次 · 时间", firstHeaderFont, headerTextBrush, new RectangleF(table.X, table.Y, firstColumnWidth, rowHeight), center);
        for (var day = 0; day < Weekdays.Length; day++)
        {
            var cell = new RectangleF(table.X + firstColumnWidth + day * dayColumnWidth, table.Y, dayColumnWidth, rowHeight);
            if (day == activeDay)
            {
                var activeHeaderRect = RectangleF.Inflate(cell, -9f * scale, -7f * scale);
                using var activeHeaderPath = RoundedRectangle(activeHeaderRect, 4f * scale);
                graphics.FillPath(activeHeaderFill, activeHeaderPath);
                graphics.DrawString(Weekdays[day], headerFont, activeHeaderBrush, activeHeaderRect, center);
            }
            else
            {
                graphics.DrawString(Weekdays[day], headerFont, headerTextBrush, cell, center);
            }
        }

        DrawVerticalLines(graphics, gridPen, table.X, table.Y, rowHeight, firstColumnWidth, dayColumnWidth, 6);

        for (var rowIndex = 0; rowIndex < Rows.Length; rowIndex++)
        {
            var row = Rows[rowIndex];
            var y = table.Y + rowHeight * (rowIndex + 1);
            var rowRect = new RectangleF(table.X, y, table.Width, rowHeight);

            if (row.IsBreak)
            {
                graphics.FillRectangle(breakBrush, rowRect);
            }
            else if (rowIndex % 2 == 1)
            {
                graphics.FillRectangle(alternateBrush, rowRect);
            }

            graphics.FillRectangle(leftBrush, table.X, y, firstColumnWidth, rowHeight);
            if (!row.IsBreak)
            {
                graphics.FillRectangle(fridayBrush, table.Right - dayColumnWidth, y, dayColumnWidth, rowHeight);
            }
            if (activeDay >= 0)
            {
                graphics.FillRectangle(activeColumnBrush, activeColumnX, y, dayColumnWidth, rowHeight);
            }
            if (activeRow == rowIndex)
            {
                var activeCell = row.IsBreak
                    ? new RectangleF(table.X + firstColumnWidth, y, table.Width - firstColumnWidth, rowHeight)
                    : activeDay >= 0
                        ? new RectangleF(table.X + firstColumnWidth + activeDay * dayColumnWidth, y, dayColumnWidth, rowHeight)
                        : RectangleF.Empty;
                if (!activeCell.IsEmpty)
                {
                    DrawActiveCourseCell(graphics, activeCell, scale, activeCellBrush, activeCellPen, activeCellCornerPen);
                }
            }

            DrawPeriodCell(graphics, new RectangleF(table.X, y, firstColumnWidth, rowHeight), row, periodFont, timeFont, courseBrush, timeBrush, scale);

            if (row.IsBreak)
            {
                var merged = new RectangleF(table.X + firstColumnWidth, y, table.Width - firstColumnWidth, rowHeight);
                graphics.DrawString(row.Courses[0], courseFont, timeBrush, merged, center);
                graphics.DrawLine(gridPen, table.X + firstColumnWidth, y, table.X + firstColumnWidth, y + rowHeight);
            }
            else
            {
                for (var day = 0; day < 5; day++)
                {
                    var cell = new RectangleF(table.X + firstColumnWidth + day * dayColumnWidth, y, dayColumnWidth, rowHeight);
                    DrawCourseCell(graphics, cell, row.Courses[day], courseFont, courseSmallFont, courseBrush, timeBrush, emptyBrush, center, scale);
                }

                DrawVerticalLines(graphics, gridPen, table.X, y, rowHeight, firstColumnWidth, dayColumnWidth, 6);
            }

            graphics.DrawLine(gridPen, table.X, y, table.Right, y);
        }

        graphics.DrawRectangle(borderPen, table.X, table.Y, table.Width, table.Height);
    }

    private static void DrawPeriodCell(
        Graphics graphics,
        RectangleF cell,
        ScheduleRow row,
        Font labelFont,
        Font timeFont,
        Brush labelBrush,
        Brush timeBrush,
        float scale)
    {
        using var center = CenterFormat();
        var timeLines = row.Time.Split('\n');
        var labelHeight = timeLines.Length > 1 ? 17f * scale : 21f * scale;
        var gap = 1f * scale;
        var timeHeight = timeLines.Length > 1 ? 28f * scale : 19f * scale;
        var total = labelHeight + gap + timeHeight;
        var top = cell.Y + (cell.Height - total) / 2f;
        graphics.DrawString(row.Label, labelFont, labelBrush, new RectangleF(cell.X, top, cell.Width, labelHeight), center);
        graphics.DrawString(row.Time, timeFont, timeBrush, new RectangleF(cell.X + 2f * scale, top + labelHeight + gap, cell.Width - 4f * scale, timeHeight), center);
    }

    private static void DrawCourseCell(
        Graphics graphics,
        RectangleF cell,
        string text,
        Font normalFont,
        Font smallFont,
        Brush normalBrush,
        Brush accentBrush,
        Brush emptyBrush,
        StringFormat center,
        float scale)
    {
        if (text == "—")
        {
            graphics.DrawString(text, normalFont, emptyBrush, cell, center);
            return;
        }

        var lines = text.Split('\n');
        if (lines.Length > 1 && IsTimeLine(lines[0]))
        {
            var firstHeight = 16f * scale;
            var remainingHeight = Math.Min(cell.Height - firstHeight, (lines.Length - 1) * 19f * scale);
            var total = firstHeight + remainingHeight;
            var top = cell.Y + (cell.Height - total) / 2f;
            graphics.DrawString(lines[0], smallFont, accentBrush, new RectangleF(cell.X, top, cell.Width, firstHeight), center);
            graphics.DrawString(string.Join('\n', lines.Skip(1)), smallFont, normalBrush, new RectangleF(cell.X, top + firstHeight, cell.Width, remainingHeight), center);
            return;
        }

        var font = lines.Length > 1 ? smallFont : normalFont;
        graphics.DrawString(text, font, normalBrush, RectangleF.Inflate(cell, -2f * scale, 0), center);
    }

    private static bool IsTimeLine(string line) => line.Length >= 5 && char.IsDigit(line[0]) && line.Contains(':');

    private static void DrawVerticalLines(
        Graphics graphics,
        Pen pen,
        float x,
        float y,
        float rowHeight,
        float firstColumnWidth,
        float dayColumnWidth,
        int columnCount)
    {
        graphics.DrawLine(pen, x + firstColumnWidth, y, x + firstColumnWidth, y + rowHeight);
        for (var column = 1; column < columnCount - 1; column++)
        {
            var lineX = x + firstColumnWidth + dayColumnWidth * column;
            graphics.DrawLine(pen, lineX, y, lineX, y + rowHeight);
        }
    }

    private static void DrawCountdown(Graphics graphics, int width, int height, DateTime today, DateTime now)
    {
        var scale = Math.Min(width / 1920f, height / 1080f);
        var panel = new RectangleF(width * .731f, height * .045f, width * .255f, height * .91f);
        using (var panelPath = RoundedRectangle(panel, 5f * scale))
        using (var panelBrush = new LinearGradientBrush(panel, Color.FromArgb(126, 38, 54), WineDeep, LinearGradientMode.ForwardDiagonal))
        using (var borderPen = new Pen(Color.FromArgb(44, Paper), Math.Max(1f, scale)))
        {
            graphics.FillPath(panelBrush, panelPath);
            graphics.DrawPath(borderPen, panelPath);
        }

        using (var glowPath = RoundedRectangle(panel, 5f * scale))
        using (var glowBrush = new LinearGradientBrush(panel, Color.FromArgb(18, Champagne), Color.FromArgb(0, Champagne), LinearGradientMode.Vertical))
        {
            graphics.FillPath(glowBrush, glowPath);
        }

        using var eyebrowFont = MakeFont("Georgia", 13f * scale, FontStyle.Regular);
        using var titleFont = MakeFont("SimSun", 29f * scale, FontStyle.Regular);
        using var clockFont = MakeFont("Georgia", 27f * scale, FontStyle.Regular);
        using var labelFont = MakeFont("Microsoft YaHei", 21f * scale, FontStyle.Regular);
        using var numberFont = MakeFont("Georgia", 104f * scale, FontStyle.Regular);
        using var unitFont = MakeFont("SimSun", 30f * scale, FontStyle.Regular);
        using var dateFont = MakeFont("Georgia", 14.5f * scale, FontStyle.Regular);
        using var sealFont = MakeFont("KaiTi", 31f * scale, FontStyle.Regular);
        using var quoteFont = MakeFont("KaiTi", 18.5f * scale, FontStyle.Regular);
        using var whiteBrush = new SolidBrush(Color.FromArgb(244, Paper));
        using var mutedBrush = new SolidBrush(Color.FromArgb(176, Paper));
        using var goldBrush = new SolidBrush(Champagne);
        using var labelBrush = new SolidBrush(Color.FromArgb(236, 220, 184));
        using var linePen = new Pen(Color.FromArgb(72, Paper), Math.Max(1f, scale));
        using var accentPen = new Pen(Color.FromArgb(185, Champagne), Math.Max(1.2f, 1.2f * scale));
        using var center = CenterFormat();
        using var clockFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

        var inner = RectangleF.Inflate(panel, -34f * scale, -34f * scale);
        using (var motifPen = new Pen(Color.FromArgb(65, Champagne), Math.Max(1f, scale)))
        {
            graphics.DrawArc(motifPen, panel.Right - 130f * scale, panel.Top + 5f * scale, 120f * scale, 120f * scale, 190f, 100f);
            graphics.DrawArc(motifPen, panel.Right - 88f * scale, panel.Top + 28f * scale, 70f * scale, 70f * scale, 190f, 100f);
        }
        graphics.DrawString("COUNTDOWN", eyebrowFont, goldBrush, new RectangleF(inner.X, inner.Y, inner.Width, 20f * scale), center);
        graphics.DrawString("2027 · 奔赴六月", titleFont, whiteBrush, new RectangleF(inner.X, inner.Y + 21f * scale, inner.Width, 38f * scale), center);
        graphics.DrawString(now.ToString("HH:mm"), clockFont, goldBrush, new RectangleF(inner.X, inner.Y + 4f * scale, 90f * scale, 30f * scale), clockFormat);
        graphics.DrawLine(accentPen, inner.X + inner.Width * .28f, inner.Y + 68f * scale, inner.Right - inner.Width * .28f, inner.Y + 68f * scale);

        var firstBlock = new RectangleF(inner.X, inner.Y + 88f * scale, inner.Width, 225f * scale);
        DrawCountdownCard(graphics, firstBlock, scale, "01");
        DrawCountdownBlock(graphics, firstBlock, "距 2027 年中考", SeniorHighExamDate, today, labelFont, numberFont, unitFont, dateFont, whiteBrush, labelBrush, mutedBrush, goldBrush, center, scale);

        var ornamentY = inner.Y + 340f * scale;
        var sealSize = 48f * scale;
        graphics.DrawLine(linePen, inner.X, ornamentY + sealSize / 2f, inner.X + inner.Width * .36f, ornamentY + sealSize / 2f);
        graphics.DrawLine(linePen, inner.Right - inner.Width * .36f, ornamentY + sealSize / 2f, inner.Right, ornamentY + sealSize / 2f);
        var sealRect = new RectangleF(inner.X + (inner.Width - sealSize) / 2f, ornamentY, sealSize, sealSize);
        using (var sealPath = RoundedRectangle(sealRect, 4f * scale))
        using (var sealBrush = new SolidBrush(Color.FromArgb(44, Champagne)))
        {
            graphics.FillPath(sealBrush, sealPath);
            graphics.DrawPath(accentPen, sealPath);
        }
        graphics.DrawString("稳", sealFont, whiteBrush, new RectangleF(inner.X + (inner.Width - sealSize) / 2f, ornamentY, sealSize, sealSize), center);

        var secondBlock = new RectangleF(inner.X, inner.Y + 420f * scale, inner.Width, 225f * scale);
        DrawCountdownCard(graphics, secondBlock, scale, "02");
        DrawCountdownBlock(graphics, secondBlock, "距一模考试", MockExamDate, today, labelFont, numberFont, unitFont, dateFont, whiteBrush, labelBrush, mutedBrush, goldBrush, center, scale);

        var quoteTop = inner.Bottom - 138f * scale;
        graphics.DrawLine(linePen, inner.X, quoteTop, inner.Right, quoteTop);
        using var quoteMarkFont = MakeFont("Georgia", 42f * scale, FontStyle.Regular);
        graphics.DrawString("“", quoteMarkFont, goldBrush, new RectangleF(inner.X - 4f * scale, quoteTop + 12f * scale, 28f * scale, 40f * scale), center);
        graphics.DrawString(
            EncouragementQuotes[GetQuoteIndex(today)],
            quoteFont,
            whiteBrush,
            new RectangleF(inner.X + 20f * scale, quoteTop + 22f * scale, inner.Width - 20f * scale, 100f * scale),
            center);
    }

    private static void DrawCountdownCard(Graphics graphics, RectangleF bounds, float scale, string index)
    {
        using var cardPath = RoundedRectangle(bounds, 4f * scale);
        using var cardBrush = new SolidBrush(Color.FromArgb(18, Paper));
        using var cardPen = new Pen(Color.FromArgb(42, Paper), Math.Max(1f, scale));
        using var accentPen = new Pen(Color.FromArgb(185, Champagne), Math.Max(1.2f, 1.2f * scale));
        using var indexFont = MakeFont("Georgia", 42f * scale, FontStyle.Regular);
        using var indexBrush = new SolidBrush(Color.FromArgb(52, Champagne));
        using var indexFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
        graphics.FillPath(cardBrush, cardPath);
        graphics.DrawPath(cardPen, cardPath);
        graphics.DrawLine(accentPen, bounds.X, bounds.Y, bounds.X + bounds.Width * .28f, bounds.Y);
        graphics.DrawString(index, indexFont, indexBrush, new RectangleF(bounds.X, bounds.Y + 7f * scale, bounds.Width - 12f * scale, 52f * scale), indexFormat);
    }

    private static void DrawCountdownBlock(
        Graphics graphics,
        RectangleF bounds,
        string label,
        DateTime target,
        DateTime today,
        Font labelFont,
        Font numberFont,
        Font unitFont,
        Font dateFont,
        Brush mainBrush,
        Brush labelBrush,
        Brush mutedBrush,
        Brush unitBrush,
        StringFormat center,
        float scale)
    {
        graphics.DrawString(label, labelFont, labelBrush, new RectangleF(bounds.X, bounds.Y + 17f * scale, bounds.Width, 34f * scale), center);
        var remaining = (target.Date - today.Date).Days;
        var value = remaining > 0 ? remaining.ToString() : remaining == 0 ? "今天" : "已结束";
        var valueFont = remaining > 0 ? numberFont : labelFont;
        DrawNumberWithUnit(graphics, new RectangleF(bounds.X, bounds.Y + 54f * scale, bounds.Width, 120f * scale), value, remaining > 0 ? "天" : "", valueFont, unitFont, mainBrush, unitBrush, center, scale);
        graphics.DrawString(target.ToString("yyyy.MM.dd"), dateFont, mutedBrush, new RectangleF(bounds.X, bounds.Y + 169f * scale, bounds.Width, 22f * scale), center);
        DrawProgressBar(graphics, bounds, target, today, dateFont, unitBrush, mutedBrush, scale);
    }

    private static void DrawProgressBar(Graphics graphics, RectangleF bounds, DateTime target, DateTime today, Font textFont, Brush fillBrush, Brush mutedBrush, float scale)
    {
        var totalDays = Math.Max(1, (target.Date - PreparationStartDate.Date).Days);
        var elapsedDays = (today.Date - PreparationStartDate.Date).Days;
        var progress = Math.Clamp(elapsedDays / (float)totalDays, 0f, 1f);
        var left = bounds.X + 20f * scale;
        var right = bounds.Right - 20f * scale;
        var labelRect = new RectangleF(left, bounds.Bottom - 36f * scale, right - left, 14f * scale);
        var lineY = bounds.Bottom - 14f * scale;
        using var trackPen = new Pen(Color.FromArgb(60, Paper), Math.Max(2f, 3f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var progressPen = new Pen(Color.FromArgb(220, Champagne), Math.Max(2f, 3f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        graphics.DrawString($"进度 {progress:P0}", textFont, mutedBrush, labelRect, format);
        graphics.DrawLine(trackPen, left, lineY, right, lineY);
        if (progress > 0f)
        {
            graphics.DrawLine(progressPen, left, lineY, left + (right - left) * progress, lineY);
        }
    }

    private static int GetWeekdayIndex(DateTime date)
    {
        return date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday
            ? (int)date.DayOfWeek - (int)DayOfWeek.Monday
            : -1;
    }

    private static int GetActiveRowIndex(DateTime now)
    {
        var weekday = GetWeekdayIndex(now.Date);
        if (weekday < 0)
        {
            return -1;
        }

        var time = now.TimeOfDay;
        if (weekday == 4)
        {
            if (IsWithin(time, 8, 0, 8, 40)) return 0;
            if (IsWithin(time, 8, 55, 9, 35)) return 1;
            if (IsWithin(time, 9, 35, 10, 5)) return 2;
            if (IsWithin(time, 10, 5, 10, 45)) return 3;
            if (IsWithin(time, 11, 0, 11, 40)) return 4;
            if (IsWithin(time, 11, 55, 12, 35)) return 5;
            if (IsWithin(time, 12, 40, 14, 0)) return 6;
            if (IsWithin(time, 14, 15, 14, 55)) return 7;
            if (IsWithin(time, 15, 10, 15, 50)) return 8;
            if (IsWithin(time, 16, 0, 16, 40)) return 9;
            if (IsWithin(time, 16, 50, 17, 30)) return 10;
            return -1;
        }

        if (IsWithin(time, 8, 0, 8, 40)) return 0;
        if (IsWithin(time, 8, 55, 9, 35)) return 1;
        if (IsWithin(time, 9, 35, 10, 5)) return 2;
        if (IsWithin(time, 10, 5, 10, 45)) return 3;
        if (IsWithin(time, 11, 0, 11, 40)) return 4;
        if (IsWithin(time, 11, 55, 12, 35)) return 5;
        if (IsWithin(time, 12, 40, 13, 20)) return 6;
        if (IsWithin(time, 13, 35, 14, 15)) return 7;
        if (IsWithin(time, 14, 30, 15, 10)) return 8;
        if (IsWithin(time, 15, 25, 16, 5)) return 9;
        if (IsWithin(time, 16, 15, 16, 55)) return 10;
        if (weekday == 1 && IsWithin(time, 18, 30, 20, 30)) return 12;
        if (weekday != 1 && IsWithin(time, 18, 40, 20, 30)) return 12;
        if (IsWithin(time, 17, 10, 18, 10)) return 11;
        return -1;
    }

    private static bool IsWithin(TimeSpan time, int startHour, int startMinute, int endHour, int endMinute)
    {
        var start = new TimeSpan(startHour, startMinute, 0);
        var end = new TimeSpan(endHour, endMinute, 0);
        return time >= start && time < end;
    }

    private static void DrawActiveCourseCell(Graphics graphics, RectangleF cell, float scale, Brush fillBrush, Pen borderPen, Pen cornerPen)
    {
        var activeRect = RectangleF.Inflate(cell, -2f * scale, -2f * scale);
        using var path = RoundedRectangle(activeRect, 4f * scale);
        graphics.FillPath(fillBrush, path);
        graphics.DrawPath(borderPen, path);

        var cornerLength = 12f * scale;
        graphics.DrawLine(cornerPen, activeRect.X, activeRect.Y + cornerLength, activeRect.X, activeRect.Y);
        graphics.DrawLine(cornerPen, activeRect.X, activeRect.Y, activeRect.X + cornerLength, activeRect.Y);
        graphics.DrawLine(cornerPen, activeRect.Right - cornerLength, activeRect.Bottom, activeRect.Right, activeRect.Bottom);
        graphics.DrawLine(cornerPen, activeRect.Right, activeRect.Bottom - cornerLength, activeRect.Right, activeRect.Bottom);
    }

    private static int GetQuoteIndex(DateTime date)
    {
        var day = (date.Date - PreparationStartDate.Date).Days;
        return ((day % EncouragementQuotes.Length) + EncouragementQuotes.Length) % EncouragementQuotes.Length;
    }

    private static void DrawNumberWithUnit(
        Graphics graphics,
        RectangleF bounds,
        string value,
        string unit,
        Font numberFont,
        Font unitFont,
        Brush brush,
        Brush unitBrush,
        StringFormat center,
        float scale)
    {
        if (string.IsNullOrEmpty(unit))
        {
            graphics.DrawString(value, numberFont, brush, bounds, center);
            return;
        }

        var valueSize = graphics.MeasureString(value, numberFont);
        var unitSize = graphics.MeasureString(unit, unitFont);
        var gap = 9f * scale;
        var totalWidth = valueSize.Width + gap + unitSize.Width;
        var startX = bounds.X + (bounds.Width - totalWidth) / 2f;
        var baselineTop = bounds.Y + (bounds.Height - valueSize.Height) / 2f;
        graphics.DrawString(value, numberFont, brush, startX, baselineTop);
        graphics.DrawString(unit, unitFont, unitBrush, startX + valueSize.Width + gap, baselineTop + valueSize.Height * .56f);
    }

    private static Font MakeFont(string preferredFamily, float size, FontStyle style)
    {
        try
        {
            return new Font(preferredFamily, Math.Max(6f, size), style, GraphicsUnit.Pixel);
        }
        catch
        {
            return new Font(FontFamily.GenericSansSerif, Math.Max(6f, size), style, GraphicsUnit.Pixel);
        }
    }

    private static StringFormat CenterFormat() => new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void SaveJpeg(Bitmap bitmap, string outputPath, long quality)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var codec = ImageCodecInfo.GetImageEncoders().First(encoder => encoder.MimeType == "image/jpeg");
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        bitmap.Save(outputPath, codec, parameters);
    }

    private sealed record ScheduleRow(string Label, string Time, string[] Courses, bool IsBreak = false);
}
