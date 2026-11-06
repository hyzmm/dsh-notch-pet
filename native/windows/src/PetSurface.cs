using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DshNotchPet
{
    internal sealed class PetSurface : FrameworkElement
    {
        private readonly PetViewModel model;
        private readonly PetTheme theme;
        private readonly PetPreferences preferences;
        private Rect robotHit = Rect.Empty;
        private Rect gearHit = Rect.Empty;
        private Rect pinHit = Rect.Empty;
        private Rect openHit = Rect.Empty;
        private Rect usageHit = Rect.Empty;
        private readonly DispatcherTimer tipTimer;
        private ToolTip activeTip;
        private string tipKey = string.Empty;
        private string tipText = string.Empty;

        public event Action<bool> HoverChanged;
        public event Action TogglePinRequested;
        public event Action ToggleSettingsRequested;
        public event Action OpenDshRequested;
        public event Action<Point> ContextMenuRequested;

        public bool ReduceMotion { get; set; }
        public double Time { get; set; }
        public double ContentOpacity { get; set; }

        public PetSurface(PetViewModel model, PetTheme theme, PetPreferences preferences)
        {
            this.model = model;
            this.theme = theme;
            this.preferences = preferences;
            Focusable = false;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
            Cursor = Cursors.Arrow;
            ContentOpacity = 1;
            tipTimer = new DispatcherTimer();
            tipTimer.Interval = TimeSpan.FromMilliseconds(650);
            tipTimer.Tick += delegate { ShowTipIfStillHovered(); };
        }

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            Point point = hitTestParameters.HitPoint;
            if (point.X < 0 || point.Y < 0 || point.X > ActualWidth || point.Y > ActualHeight)
                return null;
            return new PointHitTestResult(this, point);
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            Action<bool> handler = HoverChanged;
            if (handler != null) handler(true);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            HideToolTip();
            Action<bool> handler = HoverChanged;
            if (handler != null) handler(false);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);
            bool over = (!robotHit.IsEmpty && robotHit.Contains(p))
                || (!gearHit.IsEmpty && gearHit.Contains(p))
                || (!pinHit.IsEmpty && pinHit.Contains(p))
                || (!openHit.IsEmpty && openHit.Contains(p));
            Cursor = over ? Cursors.Hand : Cursors.Arrow;

            string key = string.Empty;
            string text = string.Empty;
            if (!gearHit.IsEmpty && gearHit.Contains(p))
            {
                key = "gear";
                text = L10n.Settings;
            }
            else if (!pinHit.IsEmpty && pinHit.Contains(p))
            {
                key = "pin";
                text = model.Pinned ? L10n.Unpin : L10n.Pin;
            }
            else if (!openHit.IsEmpty && openHit.Contains(p))
            {
                key = "open";
                text = L10n.OpenDSH;
            }
            else if (!usageHit.IsEmpty && usageHit.Contains(p) && model.Focus != null
                && model.Focus.usage != null && model.Focus.usage.HasUsage)
            {
                key = "usage";
                text = L10n.UsageDetail(model.Focus.usage);
            }
            if (key != tipKey)
            {
                HideToolTip();
                tipKey = key;
                tipText = text;
                if (key.Length > 0) tipTimer.Start();
            }
        }

        public void HideToolTip()
        {
            if (tipTimer != null) tipTimer.Stop();
            if (activeTip != null)
            {
                activeTip.IsOpen = false;
                activeTip = null;
            }
            tipKey = string.Empty;
            tipText = string.Empty;
        }

        private void ShowTipIfStillHovered()
        {
            tipTimer.Stop();
            if (tipKey.Length == 0 || !IsMouseOver) return;
            Point p = Mouse.GetPosition(this);
            Rect target = Rect.Empty;
            if (tipKey == "gear") target = gearHit;
            else if (tipKey == "pin") target = pinHit;
            else if (tipKey == "open") target = openHit;
            else if (tipKey == "usage") target = usageHit;
            if (target.IsEmpty || !target.Contains(p)) return;
            if (activeTip != null) return;

            ToolTip tip = new ToolTip();
            tip.Content = tipText;
            tip.PlacementTarget = this;
            tip.Placement = PlacementMode.Mouse;
            tip.StaysOpen = true;
            tip.HasDropShadow = true;
            tip.Background = Draw.Solid(Color.FromRgb(28, 28, 30));
            tip.Foreground = Draw.Solid(Color.FromRgb(235, 235, 240));
            tip.BorderBrush = Draw.Solid(Color.FromRgb(58, 58, 60));
            tip.BorderThickness = new Thickness(1);
            tip.Padding = new Thickness(9, 6, 9, 6);
            tip.FontFamily = Draw.UIFont;
            tip.FontSize = 10;
            tip.IsOpen = true;
            activeTip = tip;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            Point p = e.GetPosition(this);
            if (!robotHit.IsEmpty && robotHit.Contains(p))
            {
                Action handler = TogglePinRequested;
                if (handler != null) handler();
                e.Handled = true;
                return;
            }
            if (!gearHit.IsEmpty && gearHit.Contains(p))
            {
                Action handler = ToggleSettingsRequested;
                if (handler != null) handler();
                e.Handled = true;
                return;
            }
            if (!pinHit.IsEmpty && pinHit.Contains(p))
            {
                Action handler = TogglePinRequested;
                if (handler != null) handler();
                e.Handled = true;
                return;
            }
            if (!openHit.IsEmpty && openHit.Contains(p))
            {
                Action handler = OpenDshRequested;
                if (handler != null) handler();
                e.Handled = true;
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            Action<Point> handler = ContextMenuRequested;
            if (handler != null)
            {
                Point screen = PointToScreen(e.GetPosition(this));
                handler(screen);
            }
            e.Handled = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            robotHit = Rect.Empty;
            gearHit = Rect.Empty;
            pinHit = Rect.Empty;
            openHit = Rect.Empty;
            usageHit = Rect.Empty;

            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 2 || height < 2) return;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            Draw.PixelsPerDip = dpi;
            PetPhase phase = model.Phase;
            PetPalette palette = theme.Palette;
            Color accent = palette.ColorFor(phase);
            bool expanded = model.Expanded;
            bool showsPet = !preferences.hidePet;

            Draw.DrawShell(dc, width, height, expanded);
            if (showsPet) DrawGlow(dc, width, expanded, accent);

            dc.PushOpacity(Math.Max(0, Math.Min(1, ContentOpacity)));
            if (expanded) DrawExpanded(dc, width, height, dpi, phase, palette, accent, showsPet);
            else DrawCollapsed(dc, width, height, dpi, phase, palette, accent, showsPet);
            dc.Pop();
        }

        private void DrawGlow(DrawingContext dc, double width, bool expanded, Color accent)
        {
            double cx = expanded ? 66 : width - 37;
            double cy = expanded ? 82 : 43;
            double w = expanded ? 220 : 120;
            double h = expanded ? 140 : 70;
            RadialGradientBrush brush = Draw.Radial(
                new Color[]
                {
                    ColorUtil.WithAlpha(accent, 0.24),
                    ColorUtil.WithAlpha(accent, 0.04),
                    Color.FromArgb(0, accent.R, accent.G, accent.B)
                },
                new double[] { 0, 0.42, 1 }, new Point(0.5, 0.5), new Point(0.5, 0.5), 0.5, 0.5);
            dc.DrawEllipse(brush, null, new Point(cx, cy), w / 2, h / 2);
        }

        private void DrawCollapsed(DrawingContext dc, double width, double height, double dpi,
            PetPhase phase, PetPalette palette, Color accent, bool showsPet)
        {
            double bottom = height - 5;
            double textRight = width - 14 - (showsPet ? 38 : 0);
            double available = Math.Max(40, textRight - 14);

            string title = L10n.TaskTitle(model.Focus);
            double titleLine = LineHeight(10, FontWeights.SemiBold, false, dpi);
            FormattedText titleText = Draw.Text(title, 10, Color.FromArgb(237, 255, 255, 255),
                FontWeights.SemiBold, false, available - 10, titleLine + 1, dpi,
                TextTrimming.CharacterEllipsis);
            double titleHeight = Math.Min(titleText.Height, titleLine + 1);

            double elapsedWidth = 0;
            string elapsedText = ElapsedString();
            if (elapsedText.Length > 0)
            {
                FormattedText elapsed = Draw.Text(elapsedText, 8.5, Color.FromArgb(87, 255, 255, 255),
                    FontWeights.Medium, true, 0, 0, dpi, TextTrimming.None);
                elapsedWidth = elapsed.WidthIncludingTrailingWhitespace;
            }

            string live = L10n.LiveProgress(model.Focus, true);
            double liveTextLeft = 14 + 8.5 + 5;
            double liveMax = Math.Max(20, textRight - liveTextLeft - (elapsedWidth > 0 ? elapsedWidth + 4 : 0));
            Color liveColor = LiveLabelColor(phase, true, palette);
            bool middle = IsPathActivity();
            FormattedText liveText = null;
            string fittedLive = live;
            if (middle)
            {
                fittedLive = FitMiddle(live, 8.5, FontWeights.Medium, true, liveMax, dpi);
            }
            double liveLine = LineHeight(8.5, FontWeights.Medium, true, dpi);
            liveText = Draw.Text(fittedLive, 8.5, liveColor, FontWeights.Medium, true, liveMax,
                liveLine + 1, dpi, TextTrimming.CharacterEllipsis);
            double liveHeight = Math.Max(10, Math.Min(liveText.Height, liveLine + 1));

            double stackTop = bottom - titleHeight - 2.5 - liveHeight;

            dc.DrawEllipse(
                Draw.Solid(model.Connected ? accent : Color.FromArgb(64, 255, 255, 255)),
                null, new Point(14 + 2.5, stackTop + titleHeight / 2), 2.5, 2.5);

            Draw.DrawText(dc, titleText, 24, stackTop);

            Draw.DrawLoading(dc, 14 + 4.25, stackTop + titleHeight + 2.5 + liveHeight / 2,
                8.5, phase, accent, ReduceMotion, Time);
            Draw.DrawText(dc, liveText, liveTextLeft, stackTop + titleHeight + 2.5);

            if (elapsedWidth > 0)
            {
                FormattedText elapsed = Draw.Text(elapsedText, 8.5, Color.FromArgb(87, 255, 255, 255),
                    FontWeights.Medium, true, 0, 0, dpi, TextTrimming.None);
                double ey = stackTop + titleHeight + 2.5 + (liveHeight - elapsed.Height) / 2;
                Draw.DrawText(dc, elapsed, textRight - elapsed.WidthIncludingTrailingWhitespace, ey);
            }

            if (showsPet)
            {
                double size = 28;
                double px = width - 14 - size;
                double py = height - 2 - size + 1;
                robotHit = new Rect(px, py, size, size);
                PetAvatar.Render(dc, px, py, size, phase, model.Celebrating,
                    CelebrationAge(), Time, palette, ReduceMotion);
            }

            if (model.ShowsProgress)
            {
                Draw.DrawProgress(dc, 44, height - 2.8, Math.Max(24, width - 88), 2,
                    model.Focus != null && model.Focus.progress != null
                        ? model.Focus.progress.Fraction : null,
                    accent, phase.IsBusy(), ReduceMotion, Time);
            }
        }

        private void DrawExpanded(DrawingContext dc, double width, double height, double dpi,
            PetPhase phase, PetPalette palette, Color accent, bool showsPet)
        {
            double margin = 18;
            double headerTop = 34;
            double avatarSize = 58;
            if (showsPet)
            {
                robotHit = new Rect(margin, headerTop, avatarSize, avatarSize);
                PetAvatar.Render(dc, margin, headerTop, avatarSize, phase, model.Celebrating,
                    CelebrationAge(), Time, palette, ReduceMotion);
            }

            double textX = margin + (showsPet ? avatarSize + 13 : 0);
            double textWidth = Math.Max(80, width - margin - textX);
            double textTop = headerTop + 2;
            SessionFocus focus = model.Focus;

            string title = L10n.TaskTitle(focus);
            double sampleLine = LineHeight(13.5, FontWeights.SemiBold, false, dpi);
            FormattedText titleText = Draw.Text(title, 13.5, Color.FromArgb(242, 255, 255, 255),
                FontWeights.SemiBold, false, textWidth, sampleLine * 2 + 1, dpi,
                TextTrimming.CharacterEllipsis);
            double titleHeight = Math.Min(titleText.Height, sampleLine * 2 + 1);
            Draw.DrawText(dc, titleText, textX, textTop);

            double liveY = textTop + titleHeight + 6;
            double elapsedWidth = 0;
            string elapsedText = ElapsedString();
            if (elapsedText.Length > 0)
            {
                FormattedText elapsed = Draw.Text(elapsedText, 9.5, Color.FromArgb(87, 255, 255, 255),
                    FontWeights.Medium, true, 0, 0, dpi, TextTrimming.None);
                elapsedWidth = elapsed.WidthIncludingTrailingWhitespace;
            }
            string live = L10n.LiveProgress(focus, false);
            double liveTextLeft = textX + 10 + 6;
            double liveMax = Math.Max(30, textWidth - (10 + 6) - (elapsedWidth > 0 ? elapsedWidth + 4 : 0));
            Color liveColor = LiveLabelColor(phase, false, palette);
            string fittedLive = IsPathActivity()
                ? FitMiddle(live, 10.5, FontWeights.Medium, false, liveMax, dpi)
                : live;
            double liveLine = LineHeight(10.5, FontWeights.Medium, false, dpi);
            FormattedText liveText = Draw.Text(fittedLive, 10.5, liveColor, FontWeights.Medium, false,
                liveMax, liveLine + 1, dpi, TextTrimming.CharacterEllipsis);
            double liveHeight = Math.Max(12, Math.Min(liveText.Height, liveLine + 1));
            Draw.DrawLoading(dc, textX + 5, liveY + liveHeight / 2, 10, phase, accent,
                ReduceMotion, Time);
            Draw.DrawText(dc, liveText, liveTextLeft, liveY);
            if (elapsedWidth > 0)
            {
                FormattedText elapsed = Draw.Text(elapsedText, 9.5, Color.FromArgb(87, 255, 255, 255),
                    FontWeights.Medium, true, 0, 0, dpi, TextTrimming.None);
                Draw.DrawText(dc, elapsed,
                    width - margin - elapsed.WidthIncludingTrailingWhitespace,
                    liveY + (liveHeight - elapsed.Height) / 2);
            }

            double nextY = liveY + liveHeight;
            if (model.ShowsProgress)
            {
                double progressTop = nextY + 6;
                DrawProgressArea(dc, margin, textX, width, textWidth, progressTop, dpi, phase, palette, accent);
            }

            DrawFooter(dc, width, height, margin, dpi, accent);
        }

        private void DrawProgressArea(DrawingContext dc, double margin, double textX, double width,
            double textWidth, double top, double dpi, PetPhase phase, PetPalette palette, Color accent)
        {
            string label = L10n.ProgressLine(model.Focus);
            double labelHeight = LineHeight(9, FontWeights.Bold, false, dpi);
            Draw.DrawTracked(dc, label, 9, ColorUtil.WithAlpha(accent, 0.92), FontWeights.Bold,
                false, 0.25, textX, top, dpi);

            string chip = L10n.Phase(phase);
            if (model.ActiveCount > 1) chip += " ×" + model.ActiveCount;
            Color chipColor = model.Connected ? ColorUtil.WithAlpha(accent, 0.95)
                : Color.FromArgb(89, 255, 255, 255);
            FormattedText chipText = Draw.Text(chip, 8, chipColor,
                FontWeights.Bold, false, 0, 0, dpi, TextTrimming.None);
            double chipWidth = 6 + 4 + 4 + chipText.WidthIncludingTrailingWhitespace + 6;
            double chipHeight = 15;
            double chipX = width - margin - chipWidth;
            Rect chipRect = new Rect(chipX, top, chipWidth, chipHeight);
            Brush fill = Draw.Solid(ColorUtil.WithAlpha(accent, model.Connected ? 0.10 : 0.04));
            Pen border = new Pen(Draw.Solid(ColorUtil.WithAlpha(accent, model.Connected ? 0.22 : 0.08)), 0.5);
            Draw.DrawCapsule(dc, fill, border, chipRect);
            dc.DrawEllipse(Draw.Solid(model.Connected ? accent : Color.FromArgb(64, 255, 255, 255)),
                null, new Point(chipX + 6 + 2, top + chipHeight / 2), 2, 2);
            Draw.DrawText(dc, chipText, chipX + 6 + 4 + 4, top + (chipHeight - chipText.Height) / 2);

            double trackY = top + Math.Max(labelHeight, chipHeight) + 5;
            Draw.DrawProgress(dc, textX, trackY, textWidth, 3,
                model.Focus != null && model.Focus.progress != null
                    ? model.Focus.progress.Fraction : null,
                accent, phase.IsBusy(), ReduceMotion, Time);
        }

        private void DrawFooter(DrawingContext dc, double width, double height, double margin, double dpi,
            Color accent)
        {
            SessionFocus focus = model.Focus;
            double dividerY = height - 40.6;
            dc.DrawRectangle(Draw.White(0.07), null,
                new Rect(margin, dividerY, Math.Max(0, width - margin * 2), 0.6));

            double rowTop = dividerY + 0.6 + 9;
            double rowHeight = 20;
            double rowCenter = rowTop + rowHeight / 2;

            double cursor = width - margin;
            openHit = new Rect(cursor - 20, rowTop, 20, 20);
            Draw.DrawIconButton(dc, openHit, "open", accent, false);
            cursor -= 27;
            pinHit = new Rect(cursor - 20, rowTop, 20, 20);
            Draw.DrawIconButton(dc, pinHit, "pin", accent, model.Pinned);
            cursor -= 27;
            gearHit = new Rect(cursor - 20, rowTop, 20, 20);
            Draw.DrawIconButton(dc, gearHit, "gear", accent, false);
            cursor -= 27;

            if (focus != null && !string.IsNullOrEmpty(focus.model))
            {
                FormattedText modelText = Draw.Text(focus.model, 8.5, Color.FromArgb(71, 255, 255, 255),
                    FontWeights.Medium, true, 90, LineHeight(8.5, FontWeights.Medium, true, dpi) + 1,
                    dpi, TextTrimming.CharacterEllipsis);
                double modelWidth = Math.Min(90, modelText.WidthIncludingTrailingWhitespace);
                Draw.DrawText(dc, modelText, cursor - modelWidth, rowCenter - modelText.Height / 2);
                cursor -= modelWidth + 7;
            }

            double usageStart = cursor;
            if (focus != null && focus.usage != null && focus.usage.HasUsage)
            {
                TokenUsage usage = focus.usage;
                FormattedText usageText = Draw.Text(L10n.TokenUsage(usage), 8.5,
                    Color.FromArgb(112, 255, 255, 255), FontWeights.Medium, true, 0,
                    LineHeight(8.5, FontWeights.Medium, true, dpi) + 1,
                    dpi, TextTrimming.None);
                string hit = usage.CacheHitText();
                FormattedText hitText = hit == null ? null : Draw.Text("· " + L10n.CacheHit(hit), 8,
                    Color.FromArgb(66, 255, 255, 255), FontWeights.Regular, true, 0,
                    LineHeight(8, FontWeights.Regular, true, dpi) + 1,
                    dpi, TextTrimming.None);
                double tokenWidth = usageText.WidthIncludingTrailingWhitespace;
                double hitWidth = hitText == null ? 0 : hitText.WidthIncludingTrailingWhitespace;
                double total = 7 + 3 + tokenWidth + (hitText == null ? 0 : 3 + hitWidth);
                double startX = cursor - total;
                Draw.DrawGlyph(dc, "chart", startX + 3.5, rowCenter, 7,
                    ColorUtil.WithAlpha(accent, 0.55), accent, false);
                Draw.DrawText(dc, usageText, startX + 10, rowCenter - usageText.Height / 2);
                if (hitText != null)
                {
                    Draw.DrawText(dc, hitText, startX + 10 + tokenWidth + 3,
                        rowCenter - hitText.Height / 2);
                }
                usageStart = startX;
                usageHit = new Rect(startX, rowTop, total, rowHeight);
                cursor = startX - 7;
            }

            double labelMax = Math.Max(20, cursor - margin);
            FormattedText footer = Draw.Text(L10n.Footer(focus, model.ActiveCount), 10,
                Color.FromArgb(117, 255, 255, 255), FontWeights.Regular, false,
                labelMax, LineHeight(10, FontWeights.Regular, false, dpi) + 1, dpi,
                TextTrimming.CharacterEllipsis);
            Draw.DrawText(dc, footer, margin, rowCenter - footer.Height / 2);
        }

        private double CelebrationAge()
        {
            return model.Celebrating
                ? Math.Max(0, (DateTime.UtcNow - model.CelebrationStarted).TotalSeconds) : 0;
        }

        private bool IsPathActivity()
        {
            SessionFocus focus = model.Focus;
            if (focus == null || focus.activity == null) return false;
            string kind = focus.activity.kind;
            return kind == "edit" || kind == "write" || kind == "read" || kind == "readImage"
                || kind == "webFetch";
        }

        private Color LiveLabelColor(PetPhase phase, bool compact, PetPalette palette)
        {
            if (phase == PetPhase.Error) return ColorUtil.WithAlpha(palette.Error, 0.92);
            if (phase == PetPhase.Waiting) return ColorUtil.WithAlpha(palette.Waiting, 0.92);
            bool active = phase.IsBusy();
            return Color.FromArgb((byte)((active ? 0.72 : 0.48) * 255), 255, 255, 255);
        }

        private string ElapsedString()
        {
            SessionFocus focus = model.Focus;
            if (focus == null || focus.running != true || !focus.startedAt.HasValue
                || focus.startedAt.Value <= 0) return string.Empty;
            double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                .TotalMilliseconds;
            double seconds = Math.Max(0, (now - focus.startedAt.Value) / 1000.0);
            return L10n.Elapsed(seconds);
        }

        private double LineHeight(double size, FontWeight weight, bool mono, double dpi)
        {
            return Draw.Text("Ag", size, Colors.White, weight, mono, 0, 0, dpi, TextTrimming.None).Height;
        }

        private string FitMiddle(string value, double size, FontWeight weight, bool mono,
            double maxWidth, double dpi)
        {
            if (string.IsNullOrEmpty(value)) return value;
            FormattedText full = Draw.Text(value, size, Colors.White, weight, mono, 0, 0, dpi,
                TextTrimming.None);
            if (full.WidthIncludingTrailingWhitespace <= maxWidth) return value;

            int prefixLength = Math.Min(32, value.Length);
            int suffixLength = Math.Min(36, Math.Max(0, value.Length - prefixLength));
            if (value.Length <= 16)
            {
                return value.Substring(0, Math.Max(0, value.Length - 1)) + "…";
            }
            while (prefixLength + suffixLength > 0)
            {
                string candidate = value.Substring(0, prefixLength) + "…"
                    + value.Substring(value.Length - suffixLength);
                FormattedText measure = Draw.Text(candidate, size, Colors.White, weight, mono, 0, 0,
                    dpi, TextTrimming.None);
                if (measure.WidthIncludingTrailingWhitespace <= maxWidth) return candidate;
                if (prefixLength >= suffixLength && prefixLength > 4) prefixLength -= 2;
                else if (suffixLength > 4) suffixLength -= 2;
                else if (prefixLength > 2) prefixLength -= 1;
                else suffixLength -= 1;
            }
            return "…";
        }
    }
}
