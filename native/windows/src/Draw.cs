using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace DshNotchPet
{
    internal static class Draw
    {
        public static readonly FontFamily UIFont = new FontFamily("Segoe UI, Microsoft YaHei UI");
        public static readonly FontFamily MonoFont = new FontFamily("Cascadia Mono, Consolas, Microsoft YaHei UI");
        public static readonly FontFamily SymbolFont = new FontFamily("Segoe MDL2 Assets");
        public static double PixelsPerDip = 1;
        private static readonly Dictionary<uint, SolidColorBrush> BrushCache = new Dictionary<uint, SolidColorBrush>();
        private static readonly CultureInfo Culture = CultureInfo.CurrentUICulture;

        public static SolidColorBrush Solid(Color color)
        {
            uint key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
            SolidColorBrush brush;
            if (!BrushCache.TryGetValue(key, out brush))
            {
                brush = new SolidColorBrush(color);
                brush.Freeze();
                BrushCache[key] = brush;
            }
            return brush;
        }

        public static SolidColorBrush White(double opacity)
        {
            return Solid(Color.FromArgb((byte)Math.Round(Math.Max(0, Math.Min(1, opacity)) * 255), 255, 255, 255));
        }

        public static Pen Stroke(Color color, double width)
        {
            Pen pen = new Pen(Solid(color), width);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            return pen;
        }

        public static Pen Stroke(Color color, double width, PenLineCap cap)
        {
            Pen pen = Stroke(color, width);
            pen.StartLineCap = cap;
            pen.EndLineCap = cap;
            return pen;
        }

        public static LinearGradientBrush Linear(Color from, Color to, Point start, Point end)
        {
            LinearGradientBrush brush = new LinearGradientBrush(from, to, start, end);
            brush.Freeze();
            return brush;
        }

        public static LinearGradientBrush Linear(Color[] colors, double[] stops, Point start, Point end)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = start;
            brush.EndPoint = end;
            for (int i = 0; i < colors.Length; i++)
            {
                GradientStop stop = new GradientStop(colors[i],
                    stops != null && i < stops.Length ? stops[i] : (double)i / (colors.Length - 1));
                brush.GradientStops.Add(stop);
            }
            brush.Freeze();
            return brush;
        }

        public static RadialGradientBrush Radial(Color[] colors, double[] stops, Point center, Point origin,
            double radiusX, double radiusY)
        {
            RadialGradientBrush brush = new RadialGradientBrush();
            brush.MappingMode = BrushMappingMode.RelativeToBoundingBox;
            brush.Center = center;
            brush.GradientOrigin = origin;
            brush.RadiusX = radiusX;
            brush.RadiusY = radiusY;
            for (int i = 0; i < colors.Length; i++)
            {
                brush.GradientStops.Add(new GradientStop(colors[i],
                    stops != null && i < stops.Length ? stops[i] : (double)i / (colors.Length - 1)));
            }
            brush.Freeze();
            return brush;
        }

        public static FormattedText Text(string value, double size, Color color, FontWeight weight,
            bool mono, double maxWidth, double maxHeight, double dpi, TextTrimming trimming)
        {
            Typeface typeface = new Typeface(mono ? MonoFont : UIFont, FontStyles.Normal, weight,
                FontStretches.Normal);
            FormattedText text = new FormattedText(value ?? string.Empty, Culture,
                FlowDirection.LeftToRight, typeface, size, Solid(color), dpi);
            text.TextAlignment = TextAlignment.Left;
            if (maxWidth > 0) text.MaxTextWidth = maxWidth;
            if (maxHeight > 0) text.MaxTextHeight = maxHeight;
            text.Trimming = trimming;
            return text;
        }

        public static FormattedText Text(string value, double size, Brush brush, FontWeight weight,
            bool mono, double maxWidth, double maxHeight, double dpi, TextTrimming trimming)
        {
            Typeface typeface = new Typeface(mono ? MonoFont : UIFont, FontStyles.Normal, weight,
                FontStretches.Normal);
            FormattedText text = new FormattedText(value ?? string.Empty, Culture,
                FlowDirection.LeftToRight, typeface, size, brush, dpi);
            text.TextAlignment = TextAlignment.Left;
            if (maxWidth > 0) text.MaxTextWidth = maxWidth;
            if (maxHeight > 0) text.MaxTextHeight = maxHeight;
            text.Trimming = trimming;
            return text;
        }

        public static void DrawText(DrawingContext dc, FormattedText text, double x, double y)
        {
            dc.DrawText(text, new Point(x, y));
        }

        public static void DrawTracked(DrawingContext dc, string value, double size, Color color,
            FontWeight weight, bool mono, double tracking, double x, double y, double dpi)
        {
            if (string.IsNullOrEmpty(value)) return;
            double cursor = x;
            for (int i = 0; i < value.Length; i++)
            {
                string ch = value.Substring(i, 1);
                FormattedText text = Text(ch, size, color, weight, mono, 0, 0, dpi, TextTrimming.None);
                dc.DrawText(text, new Point(cursor, y));
                cursor += text.WidthIncludingTrailingWhitespace + tracking;
            }
        }

        public static double MeasureTracked(string value, double size, FontWeight weight, bool mono,
            double tracking, double dpi)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            FormattedText text = Text(value, size, Colors.White, weight, mono, 0, 0, dpi, TextTrimming.None);
            return text.WidthIncludingTrailingWhitespace + tracking * (value.Length - 1);
        }

        public static StreamGeometry RoundedGeometry(double width, double height, double radius)
        {
            return ShellGeometry(width, height, radius, radius);
        }

        public static StreamGeometry ShellGeometry(double width, double height, double topRadius, double bottomRadius)
        {
            double top = Math.Max(0, Math.Min(topRadius, Math.Min(width / 2, height / 2)));
            double bottom = Math.Max(0, Math.Min(bottomRadius, Math.Min(width / 2, height / 2)));
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(top, 0), true, true);
                ctx.LineTo(new Point(width - top, 0), true, false);
                if (top > 0)
                {
                    ctx.ArcTo(new Point(width, top), new Size(top, top), 0, false,
                        SweepDirection.Clockwise, true, false);
                }
                ctx.LineTo(new Point(width, height - bottom), true, false);
                ctx.ArcTo(new Point(width - bottom, height), new Size(bottom, bottom), 0, false,
                    SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(bottom, height), true, false);
                ctx.ArcTo(new Point(0, height - bottom), new Size(bottom, bottom), 0, false,
                    SweepDirection.Clockwise, true, false);
                if (top > 0)
                {
                    ctx.LineTo(new Point(0, top), true, false);
                    ctx.ArcTo(new Point(top, 0), new Size(top, top), 0, false,
                        SweepDirection.Clockwise, true, false);
                }
                else
                {
                    ctx.LineTo(new Point(0, 0), true, false);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        public static void DrawShell(DrawingContext dc, double width, double height, bool expanded)
        {
            double topRadius = 22;
            double bottomRadius = expanded ? 25 : 19;
            StreamGeometry geometry = ShellGeometry(width, height, topRadius, bottomRadius);
            LinearGradientBrush fill = Linear(
                new Color[] { Color.FromRgb(9, 11, 18), Color.FromRgb(2, 3, 5) },
                new double[] { 0, 1 }, new Point(0, 0), new Point(0, 1));
            dc.DrawGeometry(fill, null, geometry);

            LinearGradientBrush border = Linear(
                new Color[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(28, 255, 255, 255) },
                new double[] { 0, 1 }, new Point(0, 0), new Point(0, 1));
            dc.DrawGeometry(null, new Pen(border, 0.8), geometry);
        }

        public static void DrawEllipse(DrawingContext dc, Brush fill, Pen pen, double cx, double cy,
            double rx, double ry)
        {
            dc.DrawEllipse(fill, pen, new Point(cx, cy), rx, ry);
        }

        public static void DrawCapsule(DrawingContext dc, Brush fill, Pen pen, Rect rect)
        {
            double radius = Math.Min(rect.Width, rect.Height) / 2;
            dc.DrawRoundedRectangle(fill, pen, rect, radius, radius);
        }

        public static void DrawRounded(DrawingContext dc, Brush fill, Pen pen, Rect rect, double radius)
        {
            dc.DrawRoundedRectangle(fill, pen, rect, radius, radius);
        }

        public static void DrawProgress(DrawingContext dc, double x, double y, double width, double height,
            double? fraction, Color accent, bool animated, bool reduceMotion, double time)
        {
            double radius = height / 2;
            Rect trackRect = new Rect(x, y, width, height);
            dc.DrawRoundedRectangle(White(height > 2.5 ? 0.09 : 0.07), null, trackRect, radius, radius);
            dc.PushClip(new RectangleGeometry(trackRect, radius, radius));
            if (fraction.HasValue)
            {
                double barWidth = Math.Max(height, width * Math.Max(0, Math.Min(1, fraction.Value)));
                Rect barRect = new Rect(x, y, barWidth, height);
                LinearGradientBrush fill = Linear(
                    new Color[] { ColorUtil.WithAlpha(accent, 0.62), accent, ColorUtil.WithAlpha(accent, 0.78) },
                    new double[] { 0, 0.62, 1 }, new Point(0, 0), new Point(1, 0));
                dc.DrawRoundedRectangle(fill, null, barRect, radius, radius);
                if (animated && !reduceMotion)
                {
                    double shimmerWidth = Math.Min(28, Math.Max(10, barWidth * 0.45));
                    double unit = (time * 0.555) % 1.0;
                    double offset = -shimmerWidth + unit * (barWidth + shimmerWidth);
                    LinearGradientBrush shimmer = Linear(
                        new Color[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(140, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) },
                        new double[] { 0, 0.5, 1 }, new Point(0, 0), new Point(1, 0));
                    Rect shine = new Rect(x + offset, y, shimmerWidth, height);
                    dc.PushClip(new RectangleGeometry(barRect, radius, radius));
                    dc.DrawRectangle(shimmer, null, shine);
                    dc.Pop();
                }
            }
            else
            {
                double bandWidth = Math.Max(24, width * 0.34);
                double unit = reduceMotion ? 0.4 : (time * 0.588) % 1.0;
                double offset = -bandWidth + unit * (width + bandWidth);
                LinearGradientBrush band = Linear(
                    new Color[] { Color.FromArgb(0, accent.R, accent.G, accent.B),
                        ColorUtil.WithAlpha(accent, 0.62), accent,
                        Color.FromArgb(0, accent.R, accent.G, accent.B) },
                    new double[] { 0, 0.35, 0.65, 1 }, new Point(0, 0), new Point(1, 0));
                dc.DrawRoundedRectangle(band, null, new Rect(x + offset, y, bandWidth, height), radius, radius);
            }
            dc.Pop();
        }

        public static void DrawLoading(DrawingContext dc, double cx, double cy, double size, PetPhase phase,
            Color accent, bool reduceMotion, double time)
        {
            if (phase.IsBusy())
            {
                double angle = reduceMotion ? 0 : (time * 360.0) % 360.0;
                Pen ring = new Pen(Solid(ColorUtil.WithAlpha(accent, 0.95)), Math.Max(1, size * 0.16));
                ring.StartLineCap = PenLineCap.Round;
                ring.EndLineCap = PenLineCap.Round;
                // Draw a 72% arc using a path so the cap stays round.
                StreamGeometry geometry = ArcGeometry(cx, cy, size / 2, angle, 0.72);
                dc.DrawGeometry(null, ring, geometry);
            }
            else
            {
                dc.DrawEllipse(Draw.Solid(ColorUtil.WithAlpha(accent, 0.72)), null,
                    new Point(cx, cy), size * 0.21, size * 0.21);
            }
        }

        private static StreamGeometry ArcGeometry(double cx, double cy, double radius, double startDegrees,
            double fraction)
        {
            double start = (startDegrees - 90) * Math.PI / 180.0;
            double end = start + fraction * Math.PI * 2;
            Point p0 = new Point(cx + radius * Math.Cos(start), cy + radius * Math.Sin(start));
            Point p1 = new Point(cx + radius * Math.Cos(end), cy + radius * Math.Sin(end));
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(p0, false, false);
                ctx.ArcTo(p1, new Size(radius, radius), 0, fraction > 0.5,
                    SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        public static void DrawIconButton(DrawingContext dc, Rect rect, string kind, Color accent, bool pinned)
        {
            double radius = 6;
            dc.DrawRoundedRectangle(White(0.055), new Pen(White(0.06), 0.5), rect, radius, radius);
            double cx = rect.X + rect.Width / 2;
            double cy = rect.Y + rect.Height / 2;
            DrawGlyph(dc, kind, cx, cy, 10.0, Color.FromArgb(143, 255, 255, 255), accent, pinned);
        }

        public static void DrawGlyph(DrawingContext dc, string kind, double cx, double cy, double size,
            Color color, Color accent, bool filled)
        {
            Pen pen = Stroke(color, 1.15);
            switch (kind)
            {
                case "gear": DrawMdl2(dc, "\uE713", cx, cy, size * 1.18, color); break;
                case "pin": DrawMdl2(dc, filled ? "\uE840" : "\uE718", cx, cy, size * 1.12, color); break;
                case "open": DrawOpen(dc, cx, cy, size, color); break;
                case "chart": DrawChart(dc, cx, cy, size, accent); break;
                case "check": DrawCheck(dc, cx, cy, size, color); break;
                case "chevron": DrawChevron(dc, cx, cy, size, color, filled); break;
                case "close": DrawClose(dc, cx, cy, size, color); break;
                case "reset": DrawReset(dc, cx, cy, size, color); break;
                case "eye": DrawEyeSlash(dc, cx, cy, size, color); break;
                case "smile": DrawSmile(dc, cx, cy, size, color); break;
                case "menubar": DrawMenuBar(dc, cx, cy, size, color); break;
                case "paw": DrawPaw(dc, cx, cy, size, color); break;
                default: dc.DrawEllipse(Draw.Solid(color), pen, new Point(cx, cy), size * 0.35, size * 0.35); break;
            }
        }

        public static void DrawMdl2(DrawingContext dc, string code, double cx, double cy, double size,
            Color color)
        {
            Typeface typeface = new Typeface(SymbolFont, FontStyles.Normal, FontWeights.Regular,
                FontStretches.Normal);
            FormattedText text = new FormattedText(code, Culture, FlowDirection.LeftToRight,
                typeface, size, Solid(color), PixelsPerDip);
            text.TextAlignment = TextAlignment.Left;
            dc.DrawText(text, new Point(cx - text.WidthIncludingTrailingWhitespace / 2,
                cy - text.Height / 2));
        }

        private static void DrawGear(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Brush brush = Solid(color);
            double tooth = size * 0.17;
            for (int i = 0; i < 8; i++)
            {
                double angle = i * 45;
                dc.PushTransform(new RotateTransform(angle, cx, cy));
                dc.DrawRoundedRectangle(brush, null,
                    new Rect(cx - tooth / 2, cy - size * 0.46, tooth, size * 0.23),
                    tooth * 0.3, tooth * 0.3);
                dc.Pop();
            }
            Pen ring = Stroke(color, Math.Max(1, size * 0.14));
            dc.DrawEllipse(null, ring, new Point(cx, cy), size * 0.27, size * 0.27);
        }

        private static void DrawPin(DrawingContext dc, double cx, double cy, double size, Color color, bool filled)
        {
            dc.PushTransform(new RotateTransform(42, cx, cy));
            Brush brush = filled ? Solid(color) : null;
            Pen pen = Stroke(color, 1.1);
            dc.DrawRoundedRectangle(brush, pen,
                new Rect(cx - size * 0.25, cy - size * 0.40, size * 0.50, size * 0.20),
                size * 0.08, size * 0.08);
            dc.DrawRoundedRectangle(brush, pen,
                new Rect(cx - size * 0.16, cy - size * 0.22, size * 0.32, size * 0.26),
                size * 0.05, size * 0.05);
            dc.DrawLine(pen, new Point(cx, cy + size * 0.04), new Point(cx, cy + size * 0.42));
            dc.Pop();
        }

        private static void DrawOpen(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Pen pen = Stroke(color, 1.15);
            double s = size * 0.5;
            StreamGeometry box = new StreamGeometry();
            using (StreamGeometryContext ctx = box.Open())
            {
                ctx.BeginFigure(new Point(cx - s + 1, cy - s + 2), false, false);
                ctx.LineTo(new Point(cx - s + 1, cy + s - 1), true, false);
                ctx.LineTo(new Point(cx + s - 2, cy + s - 1), true, false);
                ctx.LineTo(new Point(cx + s - 2, cy - 1), true, false);
            }
            box.Freeze();
            dc.DrawGeometry(null, pen, box);
            dc.DrawLine(pen, new Point(cx - 0.5, cy + 0.5), new Point(cx + s - 0.5, cy - s + 0.5));
            dc.DrawLine(pen, new Point(cx + s - 4.5, cy - s + 0.5), new Point(cx + s - 0.5, cy - s + 0.5));
            dc.DrawLine(pen, new Point(cx + s - 0.5, cy - s + 0.5), new Point(cx + s - 0.5, cy - s + 4.5));
        }

        private static void DrawChart(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Pen pen = Stroke(color, 1.35);
            double baseY = cy + size * 0.38;
            double[] heights = new double[] { 0.24, 0.44, 0.62 };
            double[] xs = new double[] { -0.32, 0, 0.32 };
            for (int i = 0; i < 3; i++)
            {
                double x = cx + size * xs[i];
                dc.DrawLine(pen, new Point(x, baseY), new Point(x, baseY - size * heights[i]));
            }
        }

        private static void DrawCheck(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Pen pen = Stroke(color, 1.55);
            dc.DrawLine(pen, new Point(cx - size * 0.30, cy), new Point(cx - size * 0.06, cy + size * 0.25));
            dc.DrawLine(pen, new Point(cx - size * 0.06, cy + size * 0.25), new Point(cx + size * 0.33, cy - size * 0.25));
        }

        private static void DrawChevron(DrawingContext dc, double cx, double cy, double size, Color color, bool up)
        {
            Pen pen = Stroke(color, 1.1);
            double direction = up ? -1 : 1;
            dc.DrawLine(pen, new Point(cx - size * 0.28, cy - direction * size * 0.10),
                new Point(cx, cy + direction * size * 0.18));
            dc.DrawLine(pen, new Point(cx, cy + direction * size * 0.18),
                new Point(cx + size * 0.28, cy - direction * size * 0.10));
        }

        private static void DrawClose(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Pen pen = Stroke(color, 1.25);
            double s = size * 0.25;
            dc.DrawLine(pen, new Point(cx - s, cy - s), new Point(cx + s, cy + s));
            dc.DrawLine(pen, new Point(cx + s, cy - s), new Point(cx - s, cy + s));
        }

        private static void DrawReset(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Pen pen = Stroke(color, 1.1);
            double r = size * 0.28;
            Point start = new Point(cx - r * 0.95, cy - r * 0.25);
            StreamGeometry arc = new StreamGeometry();
            using (StreamGeometryContext ctx = arc.Open())
            {
                ctx.BeginFigure(new Point(cx, cy - r), false, false);
                ctx.ArcTo(new Point(cx - r, cy), new Size(r, r), 0, false,
                    SweepDirection.Counterclockwise, true, false);
                ctx.ArcTo(new Point(cx, cy + r), new Size(r, r), 0, false,
                    SweepDirection.Counterclockwise, true, false);
            }
            arc.Freeze();
            dc.DrawGeometry(null, pen, arc);
            dc.DrawLine(pen, new Point(cx, cy - r), new Point(start.X, start.Y));
            dc.DrawLine(pen, new Point(cx, cy - r), new Point(cx + r * 0.55, cy - r * 0.55));
        }

        private static void DrawEyeSlash(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Pen pen = Stroke(color, 1.0);
            double w = size * 0.38;
            double h = size * 0.20;
            StreamGeometry eye = new StreamGeometry();
            using (StreamGeometryContext ctx = eye.Open())
            {
                ctx.BeginFigure(new Point(cx - w, cy), false, false);
                ctx.QuadraticBezierTo(new Point(cx, cy - h * 1.8), new Point(cx + w, cy), true, false);
                ctx.QuadraticBezierTo(new Point(cx, cy + h * 1.8), new Point(cx - w, cy), true, false);
            }
            eye.Freeze();
            dc.DrawGeometry(null, pen, eye);
            dc.DrawEllipse(null, Stroke(color, 0.9), new Point(cx, cy), size * 0.075, size * 0.075);
            dc.DrawLine(pen, new Point(cx - w, cy + h), new Point(cx + w, cy - h));
        }

        private static void DrawSmile(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Pen pen = Stroke(color, 1.0);
            dc.DrawEllipse(null, pen, new Point(cx, cy), size * 0.36, size * 0.36);
            dc.DrawEllipse(Draw.Solid(color), null, new Point(cx - size * 0.12, cy - size * 0.10), 0.75, 0.75);
            dc.DrawEllipse(Draw.Solid(color), null, new Point(cx + size * 0.12, cy - size * 0.10), 0.75, 0.75);
            StreamGeometry smile = new StreamGeometry();
            using (StreamGeometryContext ctx = smile.Open())
            {
                ctx.BeginFigure(new Point(cx - size * 0.18, cy + size * 0.08), false, false);
                ctx.QuadraticBezierTo(new Point(cx, cy + size * 0.25),
                    new Point(cx + size * 0.18, cy + size * 0.08), true, false);
            }
            smile.Freeze();
            dc.DrawGeometry(null, pen, smile);
        }

        private static void DrawMenuBar(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Pen pen = Stroke(color, 1.0);
            double w = size * 0.42;
            double h = size * 0.32;
            dc.DrawRoundedRectangle(null, pen, new Rect(cx - w, cy - h, w * 2, h * 2), 1.5, 1.5);
            dc.DrawLine(pen, new Point(cx - w, cy - h + h * 0.62), new Point(cx + w, cy - h + h * 0.62));
            dc.DrawEllipse(Draw.Solid(color), null, new Point(cx - w + 2, cy - h + h * 0.31), 0.6, 0.6);
        }

        private static void DrawPaw(DrawingContext dc, double cx, double cy, double size, Color color)
        {
            Brush brush = Solid(color);
            double s = size;
            dc.DrawEllipse(brush, null, new Point(cx, cy + s * 0.18), s * 0.27, s * 0.22);
            dc.DrawEllipse(brush, null, new Point(cx - s * 0.31, cy - s * 0.08), s * 0.11, s * 0.15);
            dc.DrawEllipse(brush, null, new Point(cx - s * 0.10, cy - s * 0.25), s * 0.11, s * 0.16);
            dc.DrawEllipse(brush, null, new Point(cx + s * 0.12, cy - s * 0.25), s * 0.11, s * 0.16);
            dc.DrawEllipse(brush, null, new Point(cx + s * 0.31, cy - s * 0.06), s * 0.10, s * 0.14);
        }
    }

    internal static class PetAvatar
    {
        public static void Render(DrawingContext dc, double left, double top, double size, PetPhase phase,
            bool celebrating, double celebrationAge, double time, PetPalette palette, bool reduceMotion)
        {
            Color accent = palette.ColorFor(phase);
            double cx = left + size / 2;
            double cy = top + size / 2;

            double bob = BobOffset(phase, time, size, reduceMotion);
            bool blink = Blink(time, phase, reduceMotion);
            double eyeTravel = phase.IsBusy() && !reduceMotion ? Math.Sin(time * 1.7) * size * 0.018 : 0;
            double tilt = phase == PetPhase.Waiting ? -6 : (phase == PetPhase.Error ? 7 : 0);

            double celebrateGlow = 1;
            double celebrateBody = 1;
            if (celebrating)
            {
                double t = Math.Max(0, celebrationAge);
                double envelope = Math.Exp(-4.6 * t);
                celebrateGlow = 1 + 0.16 * envelope * Math.Cos(9.5 * t);
                celebrateBody = 1 + 0.10 * envelope * Math.Cos(9.5 * t);
            }

            // The ambient glow.
            double glowW = size * 0.95 * celebrateGlow;
            double glowH = size * 0.88 * celebrateGlow;
            RadialGradientBrush glow = Draw.Radial(
                new Color[]
                {
                    ColorUtil.WithAlpha(accent, phase.IsBusy() ? 0.34 : 0.20),
                    ColorUtil.WithAlpha(accent, 0.08),
                    Color.FromArgb(0, accent.R, accent.G, accent.B)
                },
                new double[] { 0, 0.50, 1 }, new Point(0.5, 0.5), new Point(0.5, 0.5), 0.5, 0.5);
            dc.DrawEllipse(glow, null, new Point(cx, cy), glowW / 2, glowH / 2);

            dc.PushTransform(new TranslateTransform(0, bob));
            double pivotY = size * 0.34;
            dc.PushTransform(new RotateTransform(tilt, cx, cy + pivotY));
            dc.PushTransform(new ScaleTransform(celebrateBody, celebrateBody, cx, cy + pivotY));

            Color companion = palette.SecondToneFor(phase);
            double bodyW = size * 0.76;
            double bodyH = size * 0.68;
            Rect bodyRect = new Rect(cx - bodyW / 2, cy - bodyH / 2, bodyW, bodyH);

            // Antenna and feet are details that only exist at the expanded size.
            if (size > 34)
            {
                dc.DrawRoundedRectangle(Draw.Solid(ColorUtil.WithAlpha(accent, 0.75)), null,
                    new Rect(cx - 0.6, cy - size * 0.43 - size * 0.07, 1.2, size * 0.14),
                    0.6, 0.6);
                dc.DrawEllipse(Draw.Solid(accent), null,
                    new Point(cx, cy - size * 0.51), size * 0.0375, size * 0.0375);
            }

            if (size > 34)
            {
                double footW = size * 0.15;
                double footH = size * 0.065;
                double offset = size * 0.17;
                Draw.DrawCapsule(dc, Draw.Solid(ColorUtil.WithAlpha(accent, 0.72)), null,
                    new Rect(cx - offset - footW / 2, cy + size * 0.29 - footH / 2, footW, footH));
                Draw.DrawCapsule(dc, Draw.Solid(ColorUtil.WithAlpha(accent, 0.72)), null,
                    new Rect(cx + offset - footW / 2, cy + size * 0.29 - footH / 2, footW, footH));
            }

            LinearGradientBrush body = Draw.Linear(
                new Color[] { ColorUtil.WithAlpha(accent, 0.98), ColorUtil.WithAlpha(accent, 0.82),
                    ColorUtil.WithAlpha(companion, 0.88) },
                new double[] { 0, 0.55, 1 }, new Point(0, 0), new Point(1, 1));
            dc.DrawRoundedRectangle(body, new Pen(Draw.White(0.22), 0.7), bodyRect,
                size * 0.32, size * 0.32);

            // Highlight on the upper-left of the shell.
            dc.DrawEllipse(Draw.White(0.20), null,
                new Point(cx - size * 0.13, cy - size * 0.20), size * 0.13, size * 0.05);

            double visorW = size * 0.56;
            double visorH = size * 0.27;
            Rect visor = new Rect(cx - visorW / 2 + eyeTravel * 0.4,
                cy - visorH / 2 + size * 0.008, visorW, visorH);
            LinearGradientBrush visorFill = Draw.Linear(
                new Color[] { Color.FromRgb(6, 11, 22), Color.FromRgb(2, 4, 9) },
                new double[] { 0, 1 }, new Point(0, 0), new Point(0, 1));
            dc.DrawRoundedRectangle(visorFill, new Pen(Draw.White(0.10), 0.5), visor,
                size * 0.13, size * 0.13);

            double eyeW = size * 0.060;
            double eyeH = size * 0.078;
            double gap = size * 0.105;
            double eyeY = cy + size * 0.004;
            double scaleY = blink ? 0.14 : 1;
            DrawEye(dc, cx - gap / 2 - eyeW / 2 + eyeTravel, eyeY, eyeW, eyeH, scaleY);
            DrawEye(dc, cx + gap / 2 + eyeW / 2 + eyeTravel, eyeY, eyeW, eyeH, scaleY);

            if (phase == PetPhase.Done || celebrating)
            {
                Pen smile = new Pen(Draw.White(0.55), Math.Max(0.7, size * 0.018));
                StreamGeometry mouth = new StreamGeometry();
                double mw = size * 0.13;
                double mh = size * 0.055;
                using (StreamGeometryContext ctx = mouth.Open())
                {
                    ctx.BeginFigure(new Point(cx - mw / 2, cy + size * 0.145 - mh / 2), false, false);
                    ctx.QuadraticBezierTo(new Point(cx, cy + size * 0.145 + mh / 2),
                        new Point(cx + mw / 2, cy + size * 0.145 - mh / 2), true, false);
                }
                mouth.Freeze();
                dc.DrawGeometry(null, smile, mouth);
            }
            else
            {
                Draw.DrawCapsule(dc, Draw.White(0.35), null,
                    new Rect(cx - size * 0.035, cy + size * 0.155 - Math.Max(0.7, size * 0.012) / 2,
                        size * 0.07, Math.Max(0.7, size * 0.012)));
            }

            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        private static void DrawEye(DrawingContext dc, double cx, double cy, double w, double h, double scaleY)
        {
            dc.PushTransform(new ScaleTransform(1, scaleY, cx, cy));
            Draw.DrawCapsule(dc, Draw.White(0.93), null, new Rect(cx - w / 2, cy - h / 2, w, h));
            dc.Pop();
        }

        private static double BobOffset(PetPhase phase, double time, double size, bool reduceMotion)
        {
            if (reduceMotion) return 0;
            switch (phase)
            {
                case PetPhase.Waiting: return Math.Sin(time * 1.8) * size * 0.012;
                case PetPhase.Error: return 0;
                case PetPhase.Done: return Math.Abs(Math.Sin(time * 5.5)) * size * 0.025;
                case PetPhase.Idle: return Math.Sin(time * 1.35) * size * 0.016;
                default: return Math.Sin(time * 3.8) * size * 0.026;
            }
        }

        private static bool Blink(double time, PetPhase phase, bool reduceMotion)
        {
            if (reduceMotion) return false;
            double period = phase.IsBusy() ? 2.7 : 4.1;
            double position = time - Math.Floor(time / period) * period;
            return position > period - 0.2;
        }
    }
}
