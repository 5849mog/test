using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace SteadyDesk;

internal static class WallpaperRenderer
{
    public static void Render(
        AppConfig config,
        string outputPath,
        int width,
        int height,
        DateTime? nowOverride = null)
    {
        config.Normalize();
        if (width < 480 || height < 270)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "屏幕分辨率过低，无法生成壁纸。");
        }

        var now = nowOverride ?? DateTime.Now;
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(96, 96);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        DrawBackground(graphics, config, width, height);
        DrawSchedule(graphics, config, width, height, now.Date, now);
        DrawCountdown(graphics, config, width, height, now.Date, now);
        SaveJpeg(bitmap, outputPath, 95L);
    }

    private static void DrawBackground(Graphics graphics, AppConfig config, int width, int height)
    {
        var theme = config.Theme;
        graphics.Clear(ParseColor(theme.Paper, Color.FromArgb(242, 235, 223)));
        var backgroundPath = File.Exists(config.Wallpaper.BackgroundPath)
            ? config.Wallpaper.BackgroundPath
            : AppStorage.DefaultBackgroundPath;
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
            Color.FromArgb(28, ParseColor(theme.Paper, Color.White)),
            Color.FromArgb(12, ParseColor(theme.WineDeep, Color.DarkRed)),
            LinearGradientMode.Horizontal);
        graphics.FillRectangle(veil, 0, 0, width, height);
    }

    private static void DrawSchedule(
        Graphics graphics,
        AppConfig config,
        int width,
        int height,
        DateTime today,
        DateTime now)
    {
        var scale = Math.Min(width / 1920f, height / 1080f);
        var theme = config.Theme;
        var scheduleX = Math.Clamp(config.Wallpaper.ScheduleXPercent, 3f, 25f) / 100f;
        var card = new RectangleF(width * scheduleX, height * .045f, width * .46f, height * .91f);
        var radius = 7f * scale;

        using (var shadowPath = RoundedRectangle(new RectangleF(card.X + 7f * scale, card.Y + 9f * scale, card.Width, card.Height), radius))
        using (var shadowBrush = new SolidBrush(Color.FromArgb(42, 40, 30, 24)))
        {
            graphics.FillPath(shadowBrush, shadowPath);
        }

        using (var cardPath = RoundedRectangle(card, radius))
        using (var cardBrush = new SolidBrush(Color.FromArgb(242, ParseColor(theme.Paper, Color.White))))
        using (var cardPen = new Pen(Color.FromArgb(60, ParseColor(theme.Wine, Color.DarkRed)), Math.Max(1f, scale)))
        {
            graphics.FillPath(cardBrush, cardPath);
            graphics.DrawPath(cardPen, cardPath);
        }

        var innerCard = RectangleF.Inflate(card, -10f * scale, -10f * scale);
        using (var innerCardPath = RoundedRectangle(innerCard, Math.Max(2f, radius - 3f * scale)))
        using (var innerCardPen = new Pen(Color.FromArgb(34, ParseColor(theme.Gold, Color.Goldenrod)), Math.Max(.6f, .7f * scale)))
        {
            graphics.DrawPath(innerCardPen, innerCardPath);
        }

        var padding = 28f * scale;
        var content = RectangleF.Inflate(card, -padding, -padding);
        var headerHeight = 105f * scale;
        var table = new RectangleF(content.X, content.Y + headerHeight, content.Width, content.Height - headerHeight);

        using var yearFont = MakeFont("Microsoft YaHei", 15.5f * scale, FontStyle.Regular);
        using var titleFont = MakeFont("SimSun", 38f * scale, FontStyle.Regular);
        using var yearBrush = new SolidBrush(ParseColor(theme.Wine, Color.DarkRed));
        using var titleBrush = new SolidBrush(ParseColor(theme.Ink, Color.Black));
        using var centered = CenterFormat();

        var academicStartYear = today.Month >= 8 ? today.Year : today.Year - 1;
        var academicYear = academicStartYear + "—" + (academicStartYear + 1) + " 学年";
        graphics.DrawString(academicYear, yearFont, yearBrush,
            new RectangleF(content.X, content.Y, content.Width, 25f * scale), centered);
        graphics.DrawString(config.Content.ScheduleTitle, titleFont, titleBrush,
            new RectangleF(content.X, content.Y + 24f * scale, content.Width, 55f * scale), centered);

        using var headerRulePen = new Pen(Color.FromArgb(66, ParseColor(theme.Gold, Color.Goldenrod)), Math.Max(.7f, scale));
        var headerRuleLeft = content.X + content.Width * .18f;
        var headerRuleRight = content.Right - content.Width * .18f;
        graphics.DrawLine(headerRulePen, headerRuleLeft, content.Y + 88f * scale, headerRuleRight, content.Y + 88f * scale);

        DrawScheduleTable(graphics, config, table, scale, today, now);
    }

    private static void DrawScheduleTable(
        Graphics graphics,
        AppConfig config,
        RectangleF table,
        float scale,
        DateTime today,
        DateTime now)
    {
        var schedule = config.Schedule;
        var theme = config.Theme;
        var periods = schedule.Periods;
        var firstColumnWidth = table.Width * .20f;
        var dayColumnWidth = (table.Width - firstColumnWidth) / 5f;
        var rowHeight = table.Height / Math.Max(1, periods.Count + 1);
        var lineWidth = Math.Max(.8f, scale * .65f);

        var resolvedToday = ScheduleEngine.ResolveDay(schedule, today);
        var activeDay = resolvedToday.DisplayDayIndex ?? -1;
        var activeEntry = ScheduleEngine.GetActiveEntry(resolvedToday, TimeOnly.FromDateTime(now));
        var displayDays = Enumerable.Range(0, 5)
            .Select(dayIndex => ScheduleEngine.ResolveTemplateDay(schedule, dayIndex))
            .ToArray();
        if (activeDay is >= 0 and < 5 && !resolvedToday.IsDayOff)
        {
            displayDays[activeDay] = resolvedToday;
        }

        using var gridPen = new Pen(Color.FromArgb(34, ParseColor(theme.InkSoft, Color.Gray)), lineWidth);
        using var borderPen = new Pen(Color.FromArgb(58, ParseColor(theme.Wine, Color.DarkRed)), lineWidth);
        using var headerBrush = new SolidBrush(Color.FromArgb(18, ParseColor(theme.Wine, Color.DarkRed)));
        using var leftBrush = new SolidBrush(Color.FromArgb(14, ParseColor(theme.Gold, Color.Goldenrod)));
        using var alternateBrush = new SolidBrush(Color.FromArgb(16, ParseColor(theme.Gold, Color.Goldenrod)));
        using var fridayBrush = new SolidBrush(Color.FromArgb(14, ParseColor(theme.Wine, Color.DarkRed)));
        using var breakBrush = new SolidBrush(Color.FromArgb(20, ParseColor(theme.WineDeep, Color.DarkRed)));
        using var headerFont = MakeFont("Microsoft YaHei", 17.5f * scale, FontStyle.Bold);
        using var firstHeaderFont = MakeFont("Microsoft YaHei", 15.5f * scale, FontStyle.Bold);
        using var courseFont = MakeFont("Microsoft YaHei", 17.5f * scale, FontStyle.Regular);
        using var courseSmallFont = MakeFont("Microsoft YaHei", 14.8f * scale, FontStyle.Regular);
        using var periodFont = MakeFont("Microsoft YaHei", 15.5f * scale, FontStyle.Bold);
        using var timeFont = MakeFont("Microsoft YaHei", 13.5f * scale, FontStyle.Regular);
        using var headerTextBrush = new SolidBrush(ParseColor(theme.Wine, Color.DarkRed));
        using var activeHeaderFill = new SolidBrush(ParseColor(theme.Wine, Color.DarkRed));
        using var activeHeaderBrush = new SolidBrush(Color.FromArgb(255, ParseColor(theme.Paper, Color.White)));
        using var activeColumnBrush = new SolidBrush(Color.FromArgb(9, ParseColor(theme.Champagne, Color.Goldenrod)));
        using var activeCellBrush = new SolidBrush(Color.FromArgb(36, ParseColor(theme.Champagne, Color.Goldenrod)));
        using var activeCellPen = new Pen(Color.FromArgb(195, ParseColor(theme.Gold, Color.Goldenrod)), Math.Max(2f, 2.3f * scale));
        using var activeCellCornerPen = new Pen(Color.FromArgb(120, ParseColor(theme.Wine, Color.DarkRed)), Math.Max(1f, 1.2f * scale));
        using var courseBrush = new SolidBrush(ParseColor(theme.Ink, Color.Black));
        using var timeBrush = new SolidBrush(ParseColor(theme.Wine, Color.DarkRed));
        using var emptyBrush = new SolidBrush(Color.FromArgb(120, ParseColor(theme.InkSoft, Color.Gray)));
        using var center = CenterFormat();

        graphics.FillRectangle(headerBrush, table.X, table.Y, table.Width, rowHeight);
        graphics.FillRectangle(fridayBrush, table.Right - dayColumnWidth, table.Y, dayColumnWidth, rowHeight);
        if (activeDay >= 0)
        {
            graphics.FillRectangle(activeColumnBrush, table.X + firstColumnWidth + activeDay * dayColumnWidth, table.Y, dayColumnWidth, rowHeight);
        }

        graphics.DrawString("节次 · 时间", firstHeaderFont, headerTextBrush,
            new RectangleF(table.X, table.Y, firstColumnWidth, rowHeight), center);

        for (var day = 0; day < 5; day++)
        {
            var label = day < schedule.Weekdays.Count ? schedule.Weekdays[day] : "—";
            var cell = new RectangleF(table.X + firstColumnWidth + day * dayColumnWidth, table.Y, dayColumnWidth, rowHeight);
            if (day == activeDay)
            {
                var activeHeaderRect = RectangleF.Inflate(cell, -9f * scale, -7f * scale);
                using var activeHeaderPath = RoundedRectangle(activeHeaderRect, 4f * scale);
                graphics.FillPath(activeHeaderFill, activeHeaderPath);
                graphics.DrawString(label, headerFont, activeHeaderBrush, activeHeaderRect, center);
            }
            else
            {
                graphics.DrawString(label, headerFont, headerTextBrush, cell, center);
            }
        }

        DrawVerticalLines(graphics, gridPen, table.X, table.Y, rowHeight, firstColumnWidth, dayColumnWidth, 6);

        for (var rowIndex = 0; rowIndex < periods.Count; rowIndex++)
        {
            var period = periods[rowIndex];
            var y = table.Y + rowHeight * (rowIndex + 1);
            var rowRect = new RectangleF(table.X, y, table.Width, rowHeight);
            var isBreak = period.Kind == ScheduleCellKind.Break;

            if (isBreak)
            {
                graphics.FillRectangle(breakBrush, rowRect);
            }
            else if (rowIndex % 2 == 1)
            {
                graphics.FillRectangle(alternateBrush, rowRect);
            }

            graphics.FillRectangle(leftBrush, table.X, y, firstColumnWidth, rowHeight);
            if (!isBreak)
            {
                graphics.FillRectangle(fridayBrush, table.Right - dayColumnWidth, y, dayColumnWidth, rowHeight);
            }

            if (activeDay >= 0)
            {
                graphics.FillRectangle(activeColumnBrush, table.X + firstColumnWidth + activeDay * dayColumnWidth, y, dayColumnWidth, rowHeight);
            }

            if (activeEntry?.PeriodId.Equals(period.Id, StringComparison.OrdinalIgnoreCase) == true)
            {
                var activeCell = activeEntry.Kind == ScheduleCellKind.Break || isBreak
                    ? new RectangleF(table.X + firstColumnWidth, y, table.Width - firstColumnWidth, rowHeight)
                    : activeDay >= 0
                        ? new RectangleF(table.X + firstColumnWidth + activeDay * dayColumnWidth, y, dayColumnWidth, rowHeight)
                        : RectangleF.Empty;

                if (!activeCell.IsEmpty)
                {
                    DrawActiveCourseCell(graphics, activeCell, scale, activeCellBrush, activeCellPen, activeCellCornerPen);
                }
            }

            DrawPeriodCell(
                graphics,
                new RectangleF(table.X, y, firstColumnWidth, rowHeight),
                period,
                periodFont,
                timeFont,
                courseBrush,
                timeBrush);

            if (isBreak)
            {
                var merged = new RectangleF(table.X + firstColumnWidth, y, table.Width - firstColumnWidth, rowHeight);
                var breakEntry = activeDay >= 0
                    ? displayDays[activeDay].GetEntry(period.Id)
                    : displayDays.Select(day => day.GetEntry(period.Id))
                        .FirstOrDefault(entry => entry?.Kind != ScheduleCellKind.Empty);
                graphics.DrawString(ScheduleEngine.FormatCell(breakEntry), courseFont, timeBrush, merged, center);
                graphics.DrawLine(gridPen, table.X + firstColumnWidth, y, table.X + firstColumnWidth, y + rowHeight);
            }
            else
            {
                for (var day = 0; day < 5; day++)
                {
                    var cell = new RectangleF(table.X + firstColumnWidth + day * dayColumnWidth, y, dayColumnWidth, rowHeight);
                    var course = ScheduleEngine.FormatCell(displayDays[day].GetEntry(period.Id));
                    var font = course.Contains('\n') ? courseSmallFont : courseFont;
                    var brush = course == "—" ? emptyBrush : courseBrush;
                    graphics.DrawString(course, font, brush, cell, center);
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
        SchedulePeriodConfig period,
        Font labelFont,
        Font timeFont,
        Brush labelBrush,
        Brush timeBrush)
    {
        using var labelFormat = CenterFormat();
        var labelRect = new RectangleF(cell.X, cell.Y + 2f, cell.Width, cell.Height * .48f);
        var timeRect = new RectangleF(cell.X, cell.Y + cell.Height * .45f, cell.Width, cell.Height * .53f);
        graphics.DrawString(period.Label, labelFont, labelBrush, labelRect, labelFormat);
        graphics.DrawString(ScheduleEngine.FormatPeriodTime(period), timeFont, timeBrush, timeRect, labelFormat);
    }

    private static void DrawVerticalLines(
        Graphics graphics,
        Pen pen,
        float x,
        float y,
        float height,
        float firstColumnWidth,
        float dayColumnWidth,
        int count)
    {
        graphics.DrawLine(pen, x + firstColumnWidth, y, x + firstColumnWidth, y + height);
        for (var index = 1; index < count; index++)
        {
            var lineX = x + firstColumnWidth + index * dayColumnWidth;
            graphics.DrawLine(pen, lineX, y, lineX, y + height);
        }
    }

    private static void DrawCountdown(
        Graphics graphics,
        AppConfig config,
        int width,
        int height,
        DateTime today,
        DateTime now)
    {
        var scale = Math.Min(width / 1920f, height / 1080f);
        var theme = config.Theme;
        var x = Math.Clamp(config.Wallpaper.CountdownXPercent, 50f, 70f) / 100f;
        var panel = new RectangleF(
            width * x,
            height * .045f,
            width * Math.Clamp(config.Wallpaper.CountdownWidthPercent, 25f, 44f) / 100f,
            height * .91f);

        using var panelPath = RoundedRectangle(panel, 8f * scale);
        using var panelBrush = new LinearGradientBrush(
            panel,
            Color.FromArgb(234, ParseColor(theme.WineDeep, Color.DarkRed)),
            Color.FromArgb(222, ParseColor(theme.Wine, Color.DarkRed)),
            LinearGradientMode.Vertical);
        using var panelPen = new Pen(Color.FromArgb(92, ParseColor(theme.Champagne, Color.Goldenrod)), Math.Max(1f, scale));
        graphics.FillPath(panelBrush, panelPath);
        graphics.DrawPath(panelPen, panelPath);

        var innerPanel = RectangleF.Inflate(panel, -10f * scale, -10f * scale);
        using (var innerPanelPath = RoundedRectangle(innerPanel, 5f * scale))
        using (var innerPanelPen = new Pen(Color.FromArgb(24, ParseColor(theme.Champagne, Color.Goldenrod)), Math.Max(.6f, .7f * scale)))
        {
            graphics.DrawPath(innerPanelPen, innerPanelPath);
        }

        var inner = RectangleF.Inflate(panel, -34f * scale, -34f * scale);
        using var eyebrowFont = MakeFont("Georgia", 13f * scale, FontStyle.Regular);
        using var titleFont = MakeFont("SimSun", 28f * scale, FontStyle.Regular);
        using var clockFont = MakeFont("Georgia", 26f * scale, FontStyle.Regular);
        using var whiteBrush = new SolidBrush(Color.FromArgb(244, ParseColor(theme.Paper, Color.White)));
        using var mutedBrush = new SolidBrush(Color.FromArgb(176, ParseColor(theme.Paper, Color.White)));
        using var goldBrush = new SolidBrush(ParseColor(theme.Champagne, Color.Goldenrod));
        using var overflowFont = MakeFont("Microsoft YaHei", 12f * scale, FontStyle.Regular);
        using var center = CenterFormat();

        graphics.DrawString(config.Content.Eyebrow, eyebrowFont, goldBrush,
            new RectangleF(inner.X, inner.Y, inner.Width, 22f * scale), center);
        graphics.DrawString(config.Content.CountdownTitle, titleFont, whiteBrush,
            new RectangleF(inner.X, inner.Y + 24f * scale, inner.Width, 40f * scale), center);

        if (config.Wallpaper.ShowClock)
        {
            using var near = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            graphics.DrawString(now.ToString("HH:mm"), clockFont, goldBrush,
                new RectangleF(inner.X, inner.Y + 4f * scale, 90f * scale, 30f * scale), near);
        }

        using var linePen = new Pen(Color.FromArgb(58, ParseColor(theme.Paper, Color.White)), Math.Max(1f, scale));
        graphics.DrawLine(linePen, inner.X + inner.Width * .28f, inner.Y + 72f * scale,
            inner.Right - inner.Width * .28f, inner.Y + 72f * scale);

        var allVisibleEvents = config.Events.Where(item => item.Visible).ToList();
        var events = allVisibleEvents.Take(3).ToList();
        var hiddenEventCount = allVisibleEvents.Count - events.Count;
        var cardTop = inner.Y + 92f * scale;
        var availableHeight = inner.Height - 250f * scale;
        var cardHeight = events.Count == 0 ? 180f * scale : Math.Min(205f * scale, availableHeight / events.Count - 12f * scale);

        for (var index = 0; index < events.Count; index++)
        {
            var card = new RectangleF(inner.X, cardTop + index * (cardHeight + 12f * scale), inner.Width, cardHeight);
            DrawEventCard(graphics, config, events[index], card, today, scale);
        }

        if (hiddenEventCount > 0)
        {
            graphics.DrawString(
                "还有 " + hiddenEventCount + " 个事件未显示",
                overflowFont,
                mutedBrush,
                new RectangleF(inner.X, inner.Bottom - 162f * scale, inner.Width, 22f * scale),
                center);
        }

        if (events.Count == 0)
        {
            graphics.DrawString("还没有可显示的倒计时事件", titleFont, mutedBrush,
                new RectangleF(inner.X, cardTop, inner.Width, 100f * scale), center);
        }

        if (config.Wallpaper.ShowQuote && config.Content.Quotes.Count > 0)
        {
            var quoteTop = inner.Bottom - 132f * scale;
            graphics.DrawLine(linePen, inner.X, quoteTop, inner.Right, quoteTop);
            using var quoteFont = MakeFont("KaiTi", 18f * scale, FontStyle.Regular);
            var days = (today.Date - config.Content.PreparationStartDate.Date).Days;
            var quoteIndex = ((days % config.Content.Quotes.Count) + config.Content.Quotes.Count) % config.Content.Quotes.Count;
            graphics.DrawString(config.Content.Quotes[quoteIndex], quoteFont, whiteBrush,
                new RectangleF(inner.X + 8f * scale, quoteTop + 20f * scale, inner.Width - 16f * scale, 100f * scale), center);
        }
    }

    private static void DrawEventCard(
        Graphics graphics,
        AppConfig config,
        CountdownEventConfig item,
        RectangleF bounds,
        DateTime today,
        float scale)
    {
        var theme = config.Theme;
        using var cardPath = RoundedRectangle(bounds, 5f * scale);
        using var cardBrush = new SolidBrush(Color.FromArgb(18, ParseColor(theme.Paper, Color.White)));
        using var cardPen = new Pen(Color.FromArgb(44, ParseColor(theme.Paper, Color.White)), Math.Max(1f, scale));
        using var accentPen = new Pen(ParseColor(item.Color, ParseColor(theme.Champagne, Color.Goldenrod)), Math.Max(1.5f, 1.8f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var center = CenterFormat();
        using var labelFont = MakeFont("Microsoft YaHei", 18f * scale, FontStyle.Regular);
        using var numberFont = MakeFont("Georgia", Math.Max(24f, 78f * scale), FontStyle.Regular);
        using var unitFont = MakeFont("SimSun", 25f * scale, FontStyle.Regular);
        using var dateFont = MakeFont("Georgia", 13f * scale, FontStyle.Regular);
        using var labelBrush = new SolidBrush(Color.FromArgb(238, ParseColor(theme.Champagne, Color.Goldenrod)));
        using var mainBrush = new SolidBrush(Color.FromArgb(248, ParseColor(theme.Paper, Color.White)));
        using var mutedBrush = new SolidBrush(Color.FromArgb(150, ParseColor(theme.Paper, Color.White)));

        graphics.FillPath(cardBrush, cardPath);
        graphics.DrawPath(cardPen, cardPath);
        graphics.DrawLine(accentPen, bounds.X, bounds.Y, bounds.X + bounds.Width * .30f, bounds.Y);
        graphics.DrawString(item.Title, labelFont, labelBrush,
            new RectangleF(bounds.X, bounds.Y + 12f * scale, bounds.Width, 28f * scale), center);

        var remaining = (item.Date.Date - today.Date).Days;
        var value = remaining > 0 ? remaining.ToString() : remaining == 0 ? "今天" : "已结束";
        var unit = remaining > 0 ? "天" : string.Empty;
        var valueFont = remaining > 0 ? numberFont : labelFont;
        var valueRect = new RectangleF(bounds.X, bounds.Y + 45f * scale, bounds.Width, 82f * scale);

        if (string.IsNullOrEmpty(unit))
        {
            graphics.DrawString(value, valueFont, mainBrush, valueRect, center);
        }
        else
        {
            var valueSize = graphics.MeasureString(value, valueFont);
            var unitSize = graphics.MeasureString(unit, unitFont);
            var totalWidth = valueSize.Width + unitSize.Width + 8f * scale;
            var startX = bounds.X + (bounds.Width - totalWidth) / 2f;
            var top = valueRect.Y + (valueRect.Height - valueSize.Height) / 2f;
            graphics.DrawString(value, valueFont, mainBrush, startX, top);
            graphics.DrawString(unit, unitFont, labelBrush, startX + valueSize.Width + 8f * scale, top + valueSize.Height * .55f);
        }

        graphics.DrawString(item.Date.ToString("yyyy.MM.dd"), dateFont, mutedBrush,
            new RectangleF(bounds.X, bounds.Bottom - 43f * scale, bounds.Width, 20f * scale), center);

        if (config.Wallpaper.ShowProgress && item.ShowProgress)
        {
            DrawProgressBar(graphics, bounds, config.Theme, config.Content.PreparationStartDate, item.Date, today, scale);
        }
    }

    private static void DrawProgressBar(
        Graphics graphics,
        RectangleF bounds,
        ThemeConfig theme,
        DateTime preparationStart,
        DateTime target,
        DateTime today,
        float scale)
    {
        var totalDays = Math.Max(1, (target.Date - preparationStart.Date).Days);
        var elapsedDays = (today.Date - preparationStart.Date).Days;
        var progress = Math.Clamp(elapsedDays / (float)totalDays, 0f, 1f);
        var left = bounds.X + 20f * scale;
        var right = bounds.Right - 20f * scale;
        var lineY = bounds.Bottom - 15f * scale;

        using var trackPen = new Pen(Color.FromArgb(48, ParseColor(theme.Paper, Color.White)), Math.Max(2f, 3f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var progressPen = new Pen(Color.FromArgb(190, ParseColor(theme.Champagne, Color.Goldenrod)), Math.Max(2f, 3f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        graphics.DrawLine(trackPen, left, lineY, right, lineY);
        if (progress > 0f)
        {
            graphics.DrawLine(progressPen, left, lineY, left + (right - left) * progress, lineY);
        }
    }

    private static void DrawActiveCourseCell(
        Graphics graphics,
        RectangleF cell,
        float scale,
        Brush fillBrush,
        Pen borderPen,
        Pen cornerPen)
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

    private static StringFormat CenterFormat()
    {
        return new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
    }

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

    private static Color ParseColor(string? value, Color fallback)
    {
        try
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : ColorTranslator.FromHtml(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static void SaveJpeg(Bitmap bitmap, string outputPath, long quality)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var codec = ImageCodecInfo.GetImageEncoders().First(encoder => encoder.MimeType == "image/jpeg");
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        bitmap.Save(outputPath, codec, parameters);
    }
}
