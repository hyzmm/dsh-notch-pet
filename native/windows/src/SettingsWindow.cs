using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DshNotchPet
{
    internal sealed class SettingsWindow : Window
    {
        public const double ContentWidth = 300;
        public const double MarginSize = 24;
        public const double ContentTop = 31;
        public const double ArrowHeight = 7;

        private readonly SettingsSurface surface;
        private readonly PetTheme theme;
        private readonly PetPreferences preferences;
        private double contentHeight;
        internal double ArrowX { get; private set; }
        private double screenWidth;
        private double screenHeight;
        private DispatcherTimer heightTimer;
        private double heightFrom;
        private double heightTo;
        private DateTime heightStarted;
        private IntPtr mouseHook;
        private NativeMethods.LowLevelMouseProc mouseProc;
        private bool closing;

        public event Action ClosedByUser;

        public SettingsWindow(PetTheme theme, PetPreferences preferences)
        {
            this.theme = theme;
            this.preferences = preferences;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Width = ContentWidth + MarginSize * 2;
            surface = new SettingsSurface(theme, preferences);
            contentHeight = surface.CustomExpanded
                ? SettingsSurface.ExpandedHeight : SettingsSurface.CollapsedHeight;
            Height = ContentTop + contentHeight + MarginSize;
            surface.ContentHeight = contentHeight;
            surface.CloseRequested += delegate { CloseFromUser(); };
            surface.CustomToggled += delegate { AnimateToCustomHeight(); };
            Content = surface;
            theme.Changed += OnThemeChanged;
            preferences.Changed += OnPreferencesChanged;

            SourceInitialized += delegate { InstallMouseHook(); };
            Closed += delegate
            {
                closing = true;
                if (heightTimer != null) heightTimer.Stop();
                UninstallMouseHook();
                theme.Changed -= OnThemeChanged;
                preferences.Changed -= OnPreferencesChanged;
            };
        }

        public SettingsSurface Surface { get { return surface; } }

        public void PlaceBelow(double panelLeft, double panelWidth, double panelBottom,
            double screenWidthDip, double screenHeightDip)
        {
            screenWidth = screenWidthDip;
            screenHeight = screenHeightDip;
            double center = panelLeft + panelWidth / 2.0;
            Left = Math.Max(8, Math.Min(screenWidth - Width - 8, center - Width / 2.0));
            ArrowX = Math.Max(40, Math.Min(ContentWidth + MarginSize - 40, center - Left));
            Top = panelBottom - (ContentTop - ArrowHeight);
            EnsureOnScreen();
        }

        private void EnsureOnScreen()
        {
            if (screenHeight <= 0) return;
            double bottom = Top + Height;
            if (bottom > screenHeight - 8)
                Top = Math.Max(8, screenHeight - 8 - Height);
        }

        private void AnimateToCustomHeight()
        {
            double target = surface.CustomExpanded
                ? SettingsSurface.ExpandedHeight : SettingsSurface.CollapsedHeight;
            if (Math.Abs(target - contentHeight) < 0.5)
            {
                contentHeight = target;
                Height = ContentTop + contentHeight + MarginSize;
                surface.ContentHeight = contentHeight;
                EnsureOnScreen();
                return;
            }
            heightFrom = contentHeight;
            heightTo = target;
            heightStarted = DateTime.UtcNow;
            if (heightTimer == null)
            {
                heightTimer = new DispatcherTimer();
                heightTimer.Interval = TimeSpan.FromMilliseconds(15);
                heightTimer.Tick += delegate { StepHeight(); };
            }
            heightTimer.Start();
        }

        private void StepHeight()
        {
            double elapsed = (DateTime.UtcNow - heightStarted).TotalSeconds;
            double progress = Math.Min(1, elapsed / 0.20);
            double eased = 1 - Math.Pow(1 - progress, 3);
            contentHeight = heightFrom + (heightTo - heightFrom) * eased;
            Height = ContentTop + contentHeight + MarginSize;
            surface.ContentHeight = contentHeight;
            EnsureOnScreen();
            if (progress >= 1)
            {
                heightTimer.Stop();
                contentHeight = heightTo;
                Height = ContentTop + contentHeight + MarginSize;
                surface.ContentHeight = contentHeight;
                EnsureOnScreen();
            }
        }

        private void OnThemeChanged()
        {
            surface.InvalidateVisual();
        }

        private void OnPreferencesChanged()
        {
            surface.InvalidateVisual();
        }

        public void CloseFromUser()
        {
            if (closing) return;
            closing = true;
            Close();
            Action handler = ClosedByUser;
            if (handler != null) handler();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyNoActivate();
        }

        private void ApplyNoActivate()
        {
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            int ex = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE,
                ex | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
        }

        private void InstallMouseHook()
        {
            try
            {
                mouseProc = MouseHook;
                mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, mouseProc,
                    NativeMethods.GetModuleHandle(null), 0);
            }
            catch (Exception ex)
            {
                AppLog.Write("settings mouse hook failed: " + ex.Message);
            }
        }

        private void UninstallMouseHook()
        {
            if (mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
        }

        private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && !closing)
                {
                    int message = wParam.ToInt32();
                    if (message == NativeMethods.WM_LBUTTONDOWN || message == NativeMethods.WM_RBUTTONDOWN
                        || message == NativeMethods.WM_MBUTTONDOWN)
                    {
                        NativeMethods.MSLLHOOKSTRUCT info = (NativeMethods.MSLLHOOKSTRUCT)
                            Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                        if (!ContainsScreenPoint(info.pt))
                        {
                            Dispatcher.BeginInvoke(new Action(delegate
                            {
                                if (!closing) CloseFromUser();
                            }), DispatcherPriority.Background);
                        }
                    }
                }
            }
            catch { }
            return NativeMethods.CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        private bool ContainsScreenPoint(NativeMethods.POINT point)
        {
            if (!IsVisible) return true;
            try
            {
                Point topLeft = PointToScreen(new Point(MarginSize, ContentTop));
                Point bottomRight = PointToScreen(new Point(MarginSize + ContentWidth, ContentTop + contentHeight));
                return point.X >= topLeft.X && point.X <= bottomRight.X
                    && point.Y >= topLeft.Y && point.Y <= bottomRight.Y;
            }
            catch
            {
                return true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }

    internal sealed class SettingsSurface : FrameworkElement
    {
        public const double CollapsedHeight = 411;
        public const double ExpandedHeight = 522;
        private const double Pad = 16;
        private const double GridTop = 71;
        private const double CellHeight = 52;
        private const double CellGap = 7;
        private const double CustomTop = 195;
        private const double BehaviorTopCollapsed = 230;
        private const double BehaviorTopExpanded = 341;
        private const double RowHeight = 46;
        private const double RowGap = 7;
        private const double SVHeight = 86;

        private readonly PetTheme theme;
        private readonly PetPreferences preferences;
        private bool customExpanded;
        private bool draggingSv;
        private bool draggingHue;
        private double hue;
        private double saturation;
        private double brightness;
        private Rect closeRect;
        private Rect[] presetRects = new Rect[6];
        private Rect customHeaderRect;
        private Rect resetRect;
        private Rect svRect;
        private Rect hueRect;
        private Rect[] behaviorRects = new Rect[3];
        private string hoverKey = string.Empty;

        public event Action CloseRequested;
        public event Action CustomToggled;

        public double ContentHeight { get; set; }
        public bool CustomExpanded { get { return customExpanded; } }

        public SettingsSurface(PetTheme theme, PetPreferences preferences)
        {
            this.theme = theme;
            this.preferences = preferences;
            Focusable = false;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);

            HsbComponents c = ColorUtil.ToHsb(theme.ChosenColor);
            hue = c.Hue;
            saturation = Math.Max(0, Math.Min(1, c.Saturation));
            brightness = Math.Max(0.30, Math.Min(1, c.Brightness));
            customExpanded = theme.SelectedPreset == null;
        }

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            Point p = hitTestParameters.HitPoint;
            if (p.X < SettingsWindow.MarginSize || p.Y < SettingsWindow.ContentTop
                || p.X > SettingsWindow.MarginSize + SettingsWindow.ContentWidth
                || p.Y > SettingsWindow.ContentTop + ContentHeight)
                return null;
            return new PointHitTestResult(this, p);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 2 || height < 2) return;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            Draw.PixelsPerDip = dpi;
            double contentX = SettingsWindow.MarginSize;
            double contentY = SettingsWindow.ContentTop;

            // A soft shadow keeps the popover visually lifted over DSH.
            for (int i = 8; i >= 0; i--)
            {
                byte alpha = (byte)(7 + (8 - i) * 2);
                double inflate = i * 1.8;
                dc.DrawRoundedRectangle(
                    new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)),
                    null,
                    new Rect(contentX - inflate, contentY - inflate + 6,
                        SettingsWindow.ContentWidth + inflate * 2, ContentHeight + inflate * 2),
                    18 + inflate, 18 + inflate);
            }

            Rect contentRect = new Rect(contentX, contentY, SettingsWindow.ContentWidth, ContentHeight);
            Geometry clip = new RectangleGeometry(contentRect, 16, 16);
            dc.PushClip(clip);
            DrawBackground(dc, contentRect);
            dc.PushTransform(new TranslateTransform(contentX, contentY));
            DrawContent(dc, dpi);
            dc.Pop();
            dc.Pop();

            // Arrow pointing to the panel (drawn over the panel's bottom edge).
            Point[] arrow = new Point[]
            {
                new Point(ArrowCenter(), SettingsWindow.ContentTop - SettingsWindow.ArrowHeight),
                new Point(ArrowCenter() - 7, SettingsWindow.ContentTop + 1),
                new Point(ArrowCenter() + 7, SettingsWindow.ContentTop + 1)
            };
            StreamGeometry arrowGeometry = new StreamGeometry();
            using (StreamGeometryContext ctx = arrowGeometry.Open())
            {
                ctx.BeginFigure(arrow[0], true, true);
                ctx.LineTo(arrow[1], true, false);
                ctx.LineTo(arrow[2], true, false);
            }
            arrowGeometry.Freeze();
            dc.DrawGeometry(Draw.Solid(Color.FromRgb(28, 28, 30)), null, arrowGeometry);
        }

        private double ArrowCenter()
        {
            try
            {
                Window window = Window.GetWindow(this);
                SettingsWindow settings = window as SettingsWindow;
                if (settings != null)
                {
                    // Arrow is centered under the pet panel, not the popover.
                    return settings.ArrowX;
                }
            }
            catch { }
            return SettingsWindow.MarginSize + SettingsWindow.ContentWidth / 2;
        }

        private void DrawBackground(DrawingContext dc, Rect contentRect)
        {
            LinearGradientBrush fill = Draw.Linear(
                new Color[] { Color.FromRgb(34, 34, 38), Color.FromRgb(24, 24, 27) },
                new double[] { 0, 1 }, new Point(0, 0), new Point(0, 1));
            dc.DrawRoundedRectangle(fill, new Pen(Draw.White(0.10), 0.8), contentRect, 16, 16);
        }

        private void DrawContent(DrawingContext dc, double dpi)
        {
            Color accent = theme.Palette.Primary;
            double headerY = 16;
            double closeX = SettingsWindow.ContentWidth - 16 - 18;
            drawHeader(dc, dpi, headerY, closeX, accent);

            double accentY = 54;
            Draw.DrawTracked(dc, L10n.AccentColor, 8.5, Color.FromArgb(87, 255, 255, 255),
                FontWeights.SemiBold, false, 0.45, 16, accentY, dpi);

            double gridY = GridTop;
            double cellWidth = (SettingsWindow.ContentWidth - 32 - CellGap * 2) / 3.0;
            for (int i = 0; i < AccentPreset.All.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                Rect rect = new Rect(16 + col * (cellWidth + CellGap),
                    gridY + row * (CellHeight + CellGap), cellWidth, CellHeight);
                presetRects[i] = rect;
                DrawPreset(dc, rect, AccentPreset.All[i], dpi, accent);
            }

            DrawCustomSection(dc, dpi, accent);
            DrawBehavior(dc, dpi, accent);
        }

        private void drawHeader(DrawingContext dc, double dpi, double y, double closeX, Color accent)
        {
            double cx = 16 + 13;
            double cy = y + 13;
            RadialGradientBrush aura = Draw.Radial(
                new Color[] { ColorUtil.WithAlpha(accent, 0.30), ColorUtil.WithAlpha(accent, 0.0) },
                new double[] { 0, 1 }, new Point(0.5, 0.5), new Point(0.5, 0.5), 0.5, 0.5);
            dc.DrawEllipse(aura, null, new Point(cx, cy), 17, 17);
            LinearGradientBrush orb = Draw.Linear(
                new Color[] { theme.Palette.Primary, theme.Palette.Companion },
                new double[] { 0, 1 }, new Point(0, 0), new Point(1, 1));
            dc.DrawEllipse(orb, new Pen(Draw.White(0.35), 0.6), new Point(cx, cy), 6.5, 6.5);

            FormattedText title = Draw.Text(L10n.SettingsTitle, 12.5, Color.FromArgb(240, 255, 255, 255),
                FontWeights.SemiBold, false, 0, 0, dpi, TextTrimming.None);
            Draw.DrawText(dc, title, 16 + 26 + 9, y + 1);
            FormattedText hint = Draw.Text(L10n.SettingsHint, 9, Color.FromArgb(107, 255, 255, 255),
                FontWeights.Regular, false, 0, 0, dpi, TextTrimming.None);
            Draw.DrawText(dc, hint, 16 + 26 + 9, y + 16);

            FormattedText hex = Draw.Text(theme.Hex.ToUpperInvariant(), 9,
                Color.FromArgb(87, 255, 255, 255), FontWeights.Medium, true, 0, 0, dpi, TextTrimming.None);
            Draw.DrawText(dc, hex, closeX - 9 - hex.WidthIncludingTrailingWhitespace, y + 6);

            closeRect = new Rect(closeX, y + 2, 18, 18);
            Brush closeFill = Draw.White(hoverKey == "close" ? 0.12 : 0.07);
            dc.DrawEllipse(closeFill, null, new Point(closeX + 9, y + 11), 9, 9);
            Draw.DrawGlyph(dc, "close", closeX + 9, y + 11, 9, Color.FromArgb(143, 255, 255, 255),
                accent, false);
        }

        private void DrawPreset(DrawingContext dc, Rect rect, AccentPreset preset, double dpi, Color accent)
        {
            bool selected = string.Equals(theme.Hex, preset.Hex, StringComparison.OrdinalIgnoreCase);
            bool hover = hoverKey == "preset:" + preset.Id;
            double radius = 12;
            Color fillColor = selected ? ColorUtil.WithAlpha(preset.color, 0.14)
                : Draw.White(hover ? 0.075 : 0.045).Color;
            dc.DrawRoundedRectangle(Draw.Solid(fillColor), null, rect, radius, radius);
            Color borderColor = selected ? ColorUtil.WithAlpha(preset.color, 0.62)
                : Draw.White(hover ? 0.13 : 0.06).Color;
            dc.DrawRoundedRectangle(null, new Pen(Draw.Solid(borderColor), selected ? 1 : 0.6),
                rect, radius, radius);

            Rect swatch = new Rect(rect.X + 6, rect.Y + 6, rect.Width - 12, 24);
            LinearGradientBrush gradient = Draw.Linear(
                new Color[] { ColorUtil.WithAlpha(preset.color, 0.98), ColorUtil.WithAlpha(preset.companion, 0.92) },
                new double[] { 0, 1 }, new Point(0, 0), new Point(1, 1));
            dc.DrawRoundedRectangle(gradient, null, swatch, 9, 9);
            if (selected)
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), null,
                    new Point(swatch.X + swatch.Width / 2 + 0.5, swatch.Y + swatch.Height / 2 + 1), 6, 6);
                Draw.DrawGlyph(dc, "check", swatch.X + swatch.Width / 2, swatch.Y + swatch.Height / 2,
                    11, Colors.White, accent, false);
            }

            FormattedText label = Draw.Text(preset.Name, 9,
                Color.FromArgb((byte)(selected ? 230 : 133), 255, 255, 255),
                selected ? FontWeights.SemiBold : FontWeights.Medium, false,
                rect.Width - 12, 0, dpi, TextTrimming.CharacterEllipsis);
            Draw.DrawText(dc, label, rect.X + 6, rect.Y + 34);
        }

        private void DrawCustomSection(DrawingContext dc, double dpi, Color accent)
        {
            double y = CustomTop;
            FormattedText resetText = Draw.Text(L10n.Reset, 9, Color.FromArgb((byte)(theme.IsDefault ? 56 : 153), 255, 255, 255),
                FontWeights.Medium, false, 0, 0, dpi, TextTrimming.None);
            double resetWidth = 8 + 8 + 3 + resetText.WidthIncludingTrailingWhitespace + 8;
            resetRect = new Rect(SettingsWindow.ContentWidth - 16 - resetWidth, y, resetWidth, 22);
            double mainWidth = resetRect.X - 8 - 16;
            customHeaderRect = new Rect(16, y, mainWidth, 22);

            Rect swatch = new Rect(16, y, 30, 22);
            LinearGradientBrush gradient = Draw.Linear(
                new Color[] { theme.Palette.Primary, ColorUtil.WithAlpha(theme.Palette.Companion, 0.92) },
                new double[] { 0, 1 }, new Point(0, 0), new Point(1, 1));
            dc.DrawRoundedRectangle(gradient, new Pen(Draw.White(0.18), 0.6), swatch, 8, 8);

            FormattedText title = Draw.Text(L10n.CustomColor, 10, Color.FromArgb(209, 255, 255, 255),
                FontWeights.Medium, false, 0, 0, dpi, TextTrimming.None);
            Draw.DrawText(dc, title, 55, y + 1);
            FormattedText hint = Draw.Text(L10n.CustomHint, 8.5, Color.FromArgb(92, 255, 255, 255),
                FontWeights.Regular, false, 0, 0, dpi, TextTrimming.None);
            Draw.DrawText(dc, hint, 55, y + 13);

            Draw.DrawGlyph(dc, "chevron", customHeaderRect.Right - 10, y + 11, 8,
                Color.FromArgb(97, 255, 255, 255), accent, customExpanded);

            Brush resetFill = Draw.White(hoverKey == "reset" ? 0.085 : 0.055);
            dc.DrawRoundedRectangle(resetFill, null, resetRect, 11, 11);
            Draw.DrawGlyph(dc, "reset", resetRect.X + 8, resetRect.Y + 11, 8,
                Color.FromArgb((byte)(theme.IsDefault ? 56 : 153), 255, 255, 255), accent, false);
            Draw.DrawText(dc, resetText, resetRect.X + 19, resetRect.Y + 5);

            if (!customExpanded) return;

            double svY = y + 32;
            svRect = new Rect(16, svY, SettingsWindow.ContentWidth - 32, SVHeight);
            Color hueColor = ColorUtil.FromHsb(hue, 1, 1);
            LinearGradientBrush baseGradient = Draw.Linear(
                new Color[] { Colors.White, hueColor }, new double[] { 0, 1 },
                new Point(0, 0), new Point(1, 0));
            RectangleGeometry svClip = new RectangleGeometry(svRect, 9, 9);
            dc.PushClip(svClip);
            dc.DrawRectangle(baseGradient, null, svRect);
            LinearGradientBrush shade = Draw.Linear(
                new Color[] { Color.FromArgb(0, 0, 0, 0), Color.FromArgb(178, 0, 0, 0) },
                new double[] { 0, 1 }, new Point(0, 0), new Point(0, 1));
            dc.DrawRectangle(shade, null, svRect);
            dc.Pop();
            dc.DrawRoundedRectangle(null, new Pen(Draw.White(0.10), 0.6), svRect, 9, 9);

            double norm = NormalizedBrightness();
            double cursorX = Math.Max(7, Math.Min(svRect.Width - 7, svRect.Width * saturation));
            double cursorY = Math.Max(7, Math.Min(svRect.Height - 7, svRect.Height * (1 - norm)));
            dc.DrawEllipse(Draw.Solid(PickerColor()),
                new Pen(Draw.Solid(Colors.White), 1.4), new Point(svRect.X + cursorX, svRect.Y + cursorY), 6, 6);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), 1),
                new Point(svRect.X + cursorX, svRect.Y + cursorY + 1), 6, 6);

            double hueY = svY + SVHeight + 9;
            hueRect = new Rect(16, hueY + 2.5, SettingsWindow.ContentWidth - 32 - 56 - 10, 9);
            LinearGradientBrush hueGradient = new LinearGradientBrush();
            hueGradient.StartPoint = new Point(0, 0);
            hueGradient.EndPoint = new Point(1, 0);
            for (int i = 0; i <= 12; i++)
                hueGradient.GradientStops.Add(new GradientStop(ColorUtil.FromHsb(i / 12.0, 0.95, 1), i / 12.0));
            hueGradient.Freeze();
            dc.DrawRoundedRectangle(hueGradient, null, hueRect, 4.5, 4.5);
            double hueCursorX = Math.Max(7, Math.Min(hueRect.Width - 7, hueRect.Width * hue));
            Color cursorColor = ColorUtil.FromHsb(hue, Math.Max(0.62, saturation), Math.Max(0.78, brightness));
            dc.DrawEllipse(Draw.Solid(cursorColor), new Pen(Draw.Solid(Colors.White), 1.4),
                new Point(hueRect.X + hueCursorX, hueRect.Y + hueRect.Height / 2), 7, 7);

            FormattedText hex = Draw.Text(theme.Hex.ToUpperInvariant(), 8.5,
                Color.FromArgb(97, 255, 255, 255), FontWeights.Medium, true, 56, 0, dpi,
                TextTrimming.None);
            FormattedText hexMeasure = Draw.Text(theme.Hex.ToUpperInvariant(), 8.5,
                Colors.White, FontWeights.Medium, true, 0, 0, dpi, TextTrimming.None);
            Draw.DrawText(dc, hex, SettingsWindow.ContentWidth - 16 - hexMeasure.WidthIncludingTrailingWhitespace,
                hueY - 1);
        }

        private void DrawBehavior(DrawingContext dc, double dpi, Color accent)
        {
            double y = customExpanded ? BehaviorTopExpanded : BehaviorTopCollapsed;
            dc.DrawRectangle(Draw.White(0.07), null, new Rect(16, y, SettingsWindow.ContentWidth - 32, 0.6));

            DrawBehaviorRow(dc, dpi, accent, 0, "eye", L10n.OnlyWhileWorking,
                L10n.OnlyWhileWorkingHint, preferences.hideWhenIdle, y + 7);
            DrawBehaviorRow(dc, dpi, accent, 1, "smile", L10n.HidePet, L10n.HidePetHint,
                preferences.hidePet, y + 7 + RowHeight + RowGap);
            DrawBehaviorRow(dc, dpi, accent, 2, "menubar", L10n.HideStatusItem,
                L10n.HideStatusItemHint, preferences.hideStatusItem,
                y + 7 + (RowHeight + RowGap) * 2);
        }

        private void DrawBehaviorRow(DrawingContext dc, double dpi, Color accent, int index,
            string glyph, string title, string hint, bool isOn, double y)
        {
            Rect rect = new Rect(16, y, SettingsWindow.ContentWidth - 32, RowHeight);
            behaviorRects[index] = rect;
            bool hover = hoverKey == "row:" + index;
            dc.DrawRoundedRectangle(Draw.White(hover ? 0.06 : 0.045), null, rect, 12, 12);
            dc.DrawRoundedRectangle(null, new Pen(Draw.White(0.06), 0.6), rect, 12, 12);

            Rect iconRect = new Rect(rect.X + 9, rect.Y + (RowHeight - 22) / 2, 22, 22);
            dc.DrawRoundedRectangle(
                Draw.Solid(isOn ? ColorUtil.WithAlpha(accent, 0.14) : Color.FromArgb(13, 255, 255, 255)),
                null, iconRect, 7, 7);
            Draw.DrawGlyph(dc, glyph, iconRect.X + 11, iconRect.Y + 11, 11,
                isOn ? ColorUtil.WithAlpha(accent, 0.92) : Color.FromArgb(112, 255, 255, 255),
                accent, false);

            double textX = rect.X + 40;
            double textWidth = rect.Width - 40 - 9 - 32;
            FormattedText titleText = Draw.Text(title, 10.5, Color.FromArgb(219, 255, 255, 255),
                FontWeights.Medium, false, textWidth, 0, dpi, TextTrimming.CharacterEllipsis);
            Draw.DrawText(dc, titleText, textX, rect.Y + 8);
            FormattedText hintText = Draw.Text(hint, 8.5, Color.FromArgb(97, 255, 255, 255),
                FontWeights.Regular, false, textWidth, 30, dpi, TextTrimming.CharacterEllipsis);
            Draw.DrawText(dc, hintText, textX, rect.Y + 21);

            DrawSwitch(dc, accent, isOn, rect.Right - 9 - 27, rect.Y + (RowHeight - 15) / 2);
        }

        private void DrawSwitch(DrawingContext dc, Color accent, bool on, double x, double y)
        {
            Rect track = new Rect(x, y, 27, 15);
            Brush fill = on ? Draw.Solid(ColorUtil.WithAlpha(accent, 0.92)) : Draw.White(0.12);
            dc.DrawRoundedRectangle(fill, null, track, 7.5, 7.5);
            double thumbX = on ? x + 27 - 13 : x + 2;
            dc.DrawEllipse(Draw.Solid(Colors.White), on ? new Pen(Draw.Solid(Color.FromArgb(40, 0, 0, 0)), 0.5) : null,
                new Point(thumbX + 5.5, y + 7.5), 5.5, 5.5);
        }

        private Color PickerColor()
        {
            return ColorUtil.FromHsb(hue, saturation, brightness);
        }

        private double NormalizedBrightness()
        {
            return (brightness - 0.30) / 0.70;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);
            Point local = new Point(p.X - SettingsWindow.MarginSize, p.Y - SettingsWindow.ContentTop);

            if (draggingSv)
            {
                double x = Math.Max(0, Math.Min(svRect.Width, local.X - svRect.X));
                double y = Math.Max(0, Math.Min(svRect.Height, local.Y - svRect.Y));
                saturation = x / Math.Max(1, svRect.Width);
                brightness = 0.30 + (1 - y / Math.Max(1, svRect.Height)) * 0.70;
                theme.SetCustom(PickerColor());
                InvalidateVisual();
                return;
            }
            if (draggingHue)
            {
                double x = Math.Max(0, Math.Min(hueRect.Width, local.X - hueRect.X));
                hue = Math.Min(0.999, x / Math.Max(1, hueRect.Width));
                theme.SetCustom(PickerColor());
                InvalidateVisual();
                return;
            }

            string next = HitKey(local);
            if (next != hoverKey)
            {
                hoverKey = next;
                Cursor = next.Length > 0 ? Cursors.Hand : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (!draggingSv && !draggingHue && hoverKey.Length > 0)
            {
                hoverKey = string.Empty;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p = e.GetPosition(this);
            Point local = new Point(p.X - SettingsWindow.MarginSize, p.Y - SettingsWindow.ContentTop);

            if (closeRect.Contains(local))
            {
                Action handler = CloseRequested;
                if (handler != null) handler();
                e.Handled = true;
                return;
            }

            for (int i = 0; i < presetRects.Length; i++)
            {
                if (presetRects[i].Contains(local))
                {
                    SelectPreset(AccentPreset.All[i]);
                    e.Handled = true;
                    return;
                }
            }

            if (resetRect.Contains(local))
            {
                if (!theme.IsDefault)
                {
                    theme.Reset();
                    SyncPicker(theme.ChosenColor);
                    InvalidateVisual();
                }
                e.Handled = true;
                return;
            }

            if (customHeaderRect.Contains(local))
            {
                customExpanded = !customExpanded;
                Action handler = CustomToggled;
                if (handler != null) handler();
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (customExpanded && svRect.Contains(local))
            {
                draggingSv = true;
                CaptureMouse();
                UpdateSv(local);
                e.Handled = true;
                return;
            }
            if (customExpanded && hueRect.Contains(local))
            {
                draggingHue = true;
                CaptureMouse();
                UpdateHue(local);
                e.Handled = true;
                return;
            }

            for (int i = 0; i < behaviorRects.Length; i++)
            {
                if (behaviorRects[i].Contains(local))
                {
                    ToggleBehavior(i);
                    e.Handled = true;
                    return;
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (draggingSv || draggingHue)
            {
                draggingSv = false;
                draggingHue = false;
                ReleaseMouseCapture();
                InvalidateVisual();
                e.Handled = true;
            }
        }

        private void SelectPreset(AccentPreset preset)
        {
            theme.Select(preset);
            SyncPicker(preset.color);
            InvalidateVisual();
        }

        private void ToggleBehavior(int index)
        {
            if (index == 0) preferences.SetHideWhenIdle(!preferences.hideWhenIdle);
            else if (index == 1) preferences.SetHidePet(!preferences.hidePet);
            else preferences.SetHideStatusItem(!preferences.hideStatusItem);
            InvalidateVisual();
        }

        private void UpdateSv(Point local)
        {
            double x = Math.Max(0, Math.Min(svRect.Width, local.X - svRect.X));
            double y = Math.Max(0, Math.Min(svRect.Height, local.Y - svRect.Y));
            saturation = x / Math.Max(1, svRect.Width);
            brightness = 0.30 + (1 - y / Math.Max(1, svRect.Height)) * 0.70;
            theme.SetCustom(PickerColor());
            InvalidateVisual();
        }

        private void UpdateHue(Point local)
        {
            double x = Math.Max(0, Math.Min(hueRect.Width, local.X - hueRect.X));
            hue = Math.Min(0.999, x / Math.Max(1, hueRect.Width));
            theme.SetCustom(PickerColor());
            InvalidateVisual();
        }

        private void SyncPicker(Color color)
        {
            HsbComponents c = ColorUtil.ToHsb(color);
            hue = c.Hue;
            saturation = Math.Max(0, Math.Min(1, c.Saturation));
            brightness = Math.Max(0.30, Math.Min(1, c.Brightness));
        }

        private string HitKey(Point local)
        {
            if (closeRect.Contains(local)) return "close";
            if (resetRect.Contains(local) && !theme.IsDefault) return "reset";
            for (int i = 0; i < presetRects.Length; i++)
                if (presetRects[i].Contains(local)) return "preset:" + AccentPreset.All[i].Id;
            if (customHeaderRect.Contains(local)) return "custom";
            for (int i = 0; i < behaviorRects.Length; i++)
                if (behaviorRects[i].Contains(local)) return "row:" + i;
            if (customExpanded && (svRect.Contains(local) || hueRect.Contains(local))) return "picker";
            return string.Empty;
        }
    }
}
