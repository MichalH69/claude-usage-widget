using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeUsageWidget.Skins;

/// <summary>
/// One analog dial in the style of the Windows Vista CPU Meter gadget:
/// chrome bezel, black glass face, orange needle, curved reflection on top.
/// Everything is drawn as vectors, so it scales to any size.
/// </summary>
public sealed class GaugeDial : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(GaugeDial),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GaugeDial)d).SwingNeedle()));
    public static readonly DependencyProperty EstimateProperty = Register(nameof(Estimate), false);

    // Where the needle is right now. It trails Value, so a change shows as a swing rather than a jump.
    private static readonly DependencyProperty ShownProperty = Register("Shown", 0.0);
    public static readonly DependencyProperty LabelProperty = Register(nameof(Label), "");
    public static readonly DependencyProperty ResetTextProperty = Register(nameof(ResetText), "");
    public static readonly DependencyProperty ResetClockProperty = Register(nameof(ResetClock), "");
    public static readonly DependencyProperty AccentProperty = Register(nameof(Accent), Color.FromRgb(0x8F, 0xDF, 0xFF));
    public static readonly DependencyProperty DetailedProperty = Register(nameof(Detailed), false);
    public static readonly DependencyProperty CompactProperty = Register(nameof(Compact), false);
    public static readonly DependencyProperty HoveredProperty = DependencyProperty.Register(nameof(Hovered), typeof(bool), typeof(GaugeDial),
        new FrameworkPropertyMetadata(false, (d, _) => ((GaugeDial)d).OnHoveredChanged()));

    // Which of the two reset texts shows: 0 is the time left, 1 the time of day. In between, one fades into the other.
    private static readonly DependencyProperty FlipProperty = Register("Flip", 0.0);

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(GaugeDial),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Used percent, 0–100. NaN means no data yet.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>How long is left until the next reset.</summary>
    public string ResetText { get => (string)GetValue(ResetTextProperty); set => SetValue(ResetTextProperty, value); }

    /// <summary>The time of day of the next reset. It takes the place of ResetText while the pointer is over the widget.</summary>
    public string ResetClock { get => (string)GetValue(ResetClockProperty); set => SetValue(ResetClockProperty, value); }

    /// <summary>The value is a guess, shown with a tilde.</summary>
    public bool Estimate { get => (bool)GetValue(EstimateProperty); set => SetValue(EstimateProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    /// <summary>Large dials get numerals and fine ticks.</summary>
    public bool Detailed { get => (bool)GetValue(DetailedProperty); set => SetValue(DetailedProperty, value); }

    /// <summary>The dial belongs to the small widget.</summary>
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    /// <summary>The pointer is over the widget: the needle trembles once and the reset text turns to the time of day.</summary>
    public bool Hovered { get => (bool)GetValue(HoveredProperty); set => SetValue(HoveredProperty, value); }

    /// <summary>
    /// Sizes and places of the lettering, as parts of the face radius. A dial of the small widget gets larger
    /// letters for its size, or nobody could read them.
    /// </summary>
    private readonly record struct Lettering(
        double Label, double LabelAbove, double Reset, double ResetBelow, double Number, double NumberBaseline, double Numerals);

    private static readonly Lettering MainLarge = new(0.118, 0.335, 0.150, 0.280, 0.30, 0.76, 0.145);
    private static readonly Lettering MainSmall = new(0.130, 0.335, 0.146, 0.262, 0.31, 0.76, 0.152);
    private static readonly Lettering SideLarge = new(0.175, 0.40, 0.205, 0.315, 0.37, 0.80, 0);
    private static readonly Lettering SideSmall = new(0.215, 0.40, 0.228, 0.315, 0.40, 0.80, 0);

    private Lettering Sizes => Detailed ? (Compact ? MainSmall : MainLarge) : (Compact ? SideSmall : SideLarge);

    private const double StartAngle = -225;
    private const double SweepAngle = 270;
    public const double WarnAt = Model.LimitView.WarnAt;
    public const double CriticalAt = Model.LimitView.CriticalAt;

    private static readonly Typeface Semibold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static readonly Color Red = Color.FromRgb(0xF0, 0x3A, 0x26);
    private static readonly Color Amber = Color.FromRgb(0xFF, 0xB4, 0x3A);

    public GaugeDial()
    {
        // The first swing waits for the window, or it would be over before anyone sees the dial.
        Loaded += (_, _) => SwingNeedle(fromRest: true);
    }

    // Off screen nothing moves: a still image must show how things end up.
    private bool OnScreen => IsLoaded && PresentationSource.FromVisual(this) is not null;

    private void SwingNeedle(bool fromRest = false)
    {
        double target = double.IsNaN(Value) ? 0 : Math.Clamp(Value, 0, 100);
        if (!OnScreen)
        {
            BeginAnimation(ShownProperty, null);
            SetValue(ShownProperty, target);
            return;
        }
        var swing = new DoubleAnimation(target, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut },
        };
        if (fromRest) swing.From = 0;
        BeginAnimation(ShownProperty, swing);
    }

    private void OnHoveredChanged()
    {
        double flip = Hovered ? 1 : 0;
        if (!OnScreen)
        {
            BeginAnimation(FlipProperty, null);
            SetValue(FlipProperty, flip);
            return;
        }
        BeginAnimation(FlipProperty, new DoubleAnimation(flip, TimeSpan.FromMilliseconds(240)));
        if (Hovered) ShakeNeedle();
    }

    /// <summary>A short tremble around the value, like a real gauge whose glass somebody tapped.</summary>
    private void ShakeNeedle()
    {
        if (double.IsNaN(Value)) return;
        double target = Math.Clamp(Value, 0, 100);

        // A needle still on its way to a new value is left alone.
        if (Math.Abs((double)GetValue(ShownProperty) - target) > 1) return;

        // Every dial gets a kick of its own, so the three do not move as one.
        double kick = (Detailed ? 2.0 : 2.8) * (0.8 + 0.4 * Random.Shared.NextDouble()) * (Random.Shared.Next(2) == 0 ? -1 : 1);
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(850 + Random.Shared.Next(250)) };
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(target + kick, KeyTime.FromPercent(0.11),
            new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(target, KeyTime.FromPercent(1),
            new ElasticEase { Oscillations = 3, Springiness = 5, EasingMode = EasingMode.EaseOut }));
        BeginAnimation(ShownProperty, shake);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        double ro = size * 0.47;   // outer edge of the bezel
        double rf = ro * 0.872;    // glass face

        bool hasValue = !double.IsNaN(Value);
        double v = hasValue ? Math.Clamp(Value, 0, 100) : 0;

        DrawShadow(dc, c, ro);
        DrawBezel(dc, c, ro, rf);

        dc.PushClip(new EllipseGeometry(c, rf, rf));
        DrawFace(dc, c, rf, hasValue ? v : 0);
        DrawScale(dc, c, rf);
        DrawTexts(dc, c, rf, v, hasValue);
        DrawNeedle(dc, c, rf, (double)GetValue(ShownProperty));
        DrawGlass(dc, c, rf);
        dc.Pop();

        dc.DrawEllipse(null, new Pen(Solid(Color.FromArgb(235, 4, 6, 8)), Math.Max(1, rf * 0.022)), c, rf, rf);
    }

    private static void DrawShadow(DrawingContext dc, Point c, double ro)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(170, 0, 0, 0), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(170, 0, 0, 0), 0.82));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(70, 0, 0, 0), 0.91));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
        dc.DrawEllipse(brush, null, new Point(c.X, c.Y + ro * 0.055), ro * 1.10, ro * 1.10);
    }

    private static void DrawBezel(DrawingContext dc, Point c, double ro, double rf)
    {
        // Outer ring: polished chrome, lit from the upper left.
        var chrome = new LinearGradientBrush { StartPoint = new Point(0.18, 0), EndPoint = new Point(0.82, 1) };
        chrome.GradientStops.Add(new GradientStop(Rgb(0xFFFFFF), 0));
        chrome.GradientStops.Add(new GradientStop(Rgb(0xBFC8CF), 0.18));
        chrome.GradientStops.Add(new GradientStop(Rgb(0xF7F9FA), 0.34));
        chrome.GradientStops.Add(new GradientStop(Rgb(0x7A838B), 0.52));
        chrome.GradientStops.Add(new GradientStop(Rgb(0xD5DBE0), 0.70));
        chrome.GradientStops.Add(new GradientStop(Rgb(0x565E66), 0.87));
        chrome.GradientStops.Add(new GradientStop(Rgb(0x2B3136), 1));
        dc.DrawEllipse(chrome, new Pen(Solid(Color.FromArgb(230, 22, 27, 31)), 1), c, ro, ro);

        // Inner chamfer: the same metal, but facing the other way.
        double rm = rf + (ro - rf) * 0.45;
        var chamfer = new LinearGradientBrush { StartPoint = new Point(0.18, 0), EndPoint = new Point(0.82, 1) };
        chamfer.GradientStops.Add(new GradientStop(Rgb(0x30363C), 0));
        chamfer.GradientStops.Add(new GradientStop(Rgb(0x7F8890), 0.45));
        chamfer.GradientStops.Add(new GradientStop(Rgb(0xF2F5F7), 1));
        dc.DrawEllipse(chamfer, new Pen(Solid(Color.FromArgb(110, 0, 0, 0)), 0.8), c, rm, rm);
    }

    private static void DrawFace(DrawingContext dc, Point c, double rf, double v)
    {
        var face = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.32), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
        face.GradientStops.Add(new GradientStop(Rgb(0x343D46), 0));
        face.GradientStops.Add(new GradientStop(Rgb(0x171C21), 0.55));
        face.GradientStops.Add(new GradientStop(Rgb(0x06080A), 1));
        dc.DrawEllipse(face, null, c, rf, rf);

        // Backlight from below. Turns warm when the limit is nearly used up.
        Color light = v >= CriticalAt ? Color.FromRgb(0xFF, 0x3B, 0x24)
                    : v >= WarnAt ? Color.FromRgb(0xFF, 0xA6, 0x2B)
                    : Color.FromRgb(0x3C, 0xC2, 0xFF);
        var glow = new RadialGradientBrush { Center = new Point(0.5, 1.04), GradientOrigin = new Point(0.5, 1.04), RadiusX = 0.66, RadiusY = 0.52 };
        glow.GradientStops.Add(new GradientStop(WithAlpha(light, 105), 0));
        glow.GradientStops.Add(new GradientStop(WithAlpha(light, 0), 1));
        dc.DrawEllipse(glow, null, c, rf, rf);

        // Shadow the bezel casts on the face.
        var inner = new RadialGradientBrush();
        inner.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.80));
        inner.GradientStops.Add(new GradientStop(Color.FromArgb(150, 0, 0, 0), 1));
        dc.DrawEllipse(inner, null, c, rf, rf);
    }

    private void DrawScale(DrawingContext dc, Point c, double rf)
    {
        double rBand = rf * 0.955;
        double bandWidth = rf * 0.04;
        dc.DrawGeometry(null, new Pen(Solid(Color.FromArgb(120, 0xB8, 0xC4, 0xCD)), rf * 0.012), Arc(c, rBand, AngleFor(0), AngleFor(WarnAt)));
        dc.DrawGeometry(null, new Pen(Solid(Amber), bandWidth), Arc(c, rBand, AngleFor(WarnAt), AngleFor(CriticalAt)));
        dc.DrawGeometry(null, new Pen(Solid(Red), bandWidth), Arc(c, rBand, AngleFor(CriticalAt), AngleFor(100)));

        double rOuter = rf * 0.915;
        double majorLength = rf * 0.135;
        double minorLength = rf * 0.065;
        var major = new Pen(Solid(Rgb(0xF3F7FA)), Math.Max(1.1, rf * 0.027));
        var majorRed = new Pen(Solid(Red), Math.Max(1.1, rf * 0.027));
        var minor = new Pen(Solid(Rgb(0x9CA8B2)), Math.Max(0.7, rf * 0.013));
        var minorRed = new Pen(Solid(WithAlpha(Red, 220)), Math.Max(0.7, rf * 0.013));

        int step = Detailed ? 2 : 5;
        for (int t = 0; t <= 100; t += step)
        {
            bool isMajor = t % 10 == 0;
            bool isHalf = !isMajor && t % 5 == 0;
            double length = isMajor ? majorLength : isHalf && Detailed ? minorLength * 1.35 : minorLength;
            bool red = t >= CriticalAt;
            Pen pen = isMajor ? (red ? majorRed : major) : (red ? minorRed : minor);
            double a = AngleFor(t);
            dc.DrawLine(pen, Polar(c, rOuter, a), Polar(c, rOuter - length, a));
        }

        if (!Detailed) return;

        var numeral = Solid(Rgb(0xDCE5EC));
        for (int t = 0; t <= 100; t += 20)
        {
            Point p = Polar(c, rf * 0.615, AngleFor(t));
            dc.DrawGeometry(t >= CriticalAt ? Solid(Rgb(0xFF7A66)) : numeral, null,
                Text(t.ToString(CultureInfo.InvariantCulture), Semibold, rf * Sizes.Numerals, p));
        }
    }

    private void DrawTexts(DrawingContext dc, Point c, double rf, double v, bool hasValue)
    {
        Lettering sizes = Sizes;

        // Name of the limit, in tracked capitals above the hub.
        double labelSize = rf * sizes.Label;
        var label = Tracked(Label, Semibold, labelSize, labelSize * 0.14, new Point(c.X, c.Y - rf * sizes.LabelAbove));
        dc.DrawGeometry(Solid(Mix(Accent, Colors.White, 0.25)), null, label);

        if (!string.IsNullOrEmpty(ResetText)) DrawReset(dc, new Point(c.X, c.Y + rf * sizes.ResetBelow), rf, rf * sizes.Reset);

        // Digital readout in the gap at the bottom of the scale.
        Color readoutColor = !hasValue ? Rgb(0x8A97A2)
                           : v >= CriticalAt ? Rgb(0xFF6A55)
                           : v >= WarnAt ? Rgb(0xFFC65C)
                           : Rgb(0xF4FAFF);
        double numberSize = rf * sizes.Number;
        double baseline = c.Y + rf * sizes.NumberBaseline;
        string number = hasValue ? (Estimate ? "~" : "") + Math.Round(v).ToString(CultureInfo.InvariantCulture) : "—";

        var numberText = Formatted(number, Bold, numberSize);
        var percentText = Formatted("%", Semibold, numberSize * 0.56);
        double gap = numberSize * 0.07;
        double total = numberText.WidthIncludingTrailingWhitespace + (hasValue ? gap + percentText.WidthIncludingTrailingWhitespace : 0);
        double x = c.X - total / 2;

        var readout = new GeometryGroup { FillRule = FillRule.Nonzero };
        readout.Children.Add(numberText.BuildGeometry(new Point(x, baseline - numberText.Baseline)));
        if (hasValue)
        {
            readout.Children.Add(percentText.BuildGeometry(
                new Point(x + numberText.WidthIncludingTrailingWhitespace + gap, baseline - percentText.Baseline)));
        }

        // A faint halo so the digits look lit rather than printed.
        dc.DrawGeometry(null, new Pen(Solid(WithAlpha(readoutColor, 22)), numberSize * 0.22) { LineJoin = PenLineJoin.Round }, readout);
        dc.DrawGeometry(null, new Pen(Solid(WithAlpha(readoutColor, 34)), numberSize * 0.10) { LineJoin = PenLineJoin.Round }, readout);
        dc.DrawGeometry(Solid(readoutColor), null, readout);
    }

    /// <summary>The next reset: how long is left, or under the pointer the time of day it comes at.</summary>
    private void DrawReset(DrawingContext dc, Point center, double rf, double size)
    {
        // A limit with no time of day to show, such as one that has just reset, keeps its text under the pointer.
        string clock = string.IsNullOrEmpty(ResetClock) ? ResetText : ResetClock;
        double flip = (double)GetValue(FlipProperty);

        // Every dial shows its reset in a small display of its own.
        {
            // The frame keeps its height whatever the letters, and its width slides from one text to the other.
            double from = Formatted(ResetText, Semibold, size).WidthIncludingTrailingWhitespace;
            double to = Formatted(clock, Semibold, size).WidthIncludingTrailingWhitespace;
            double width = from + (to - from) * flip + rf * 0.15;
            double height = size * 0.74 + rf * 0.076;
            var box = new Rect(center.X - width / 2, center.Y + size * 0.045 - height / 2, width, height);
            var lcd = new LinearGradientBrush(Color.FromArgb(235, 3, 5, 7), Color.FromArgb(235, 18, 24, 30), 90);
            var edge = new LinearGradientBrush(Color.FromArgb(200, 0, 0, 0), Color.FromArgb(120, 0x7C, 0x8A, 0x96), 90);
            dc.DrawRoundedRectangle(lcd, new Pen(edge, 0.9), box, rf * 0.035, rf * 0.035);
        }

        dc.PushOpacity(clock == ResetText ? 1 : Math.Abs(flip - 0.5) * 2);
        dc.DrawGeometry(Solid(Rgb(0x9BE3FF)), null,
            Text(flip < 0.5 ? ResetText : clock, Semibold, size, center));
        dc.Pop();
    }

    private static void DrawNeedle(DrawingContext dc, Point c, double rf, double v)
    {
        double tip = rf * 0.81;
        double tail = rf * 0.21;
        double half = rf * 0.036;

        var body = new StreamGeometry();
        using (var g = body.Open())
        {
            g.BeginFigure(new Point(c.X - tail, c.Y - half * 1.3), true, true);
            g.LineTo(new Point(c.X + tip * 0.93, c.Y - half * 0.30), true, true);
            g.LineTo(new Point(c.X + tip, c.Y), true, true);
            g.LineTo(new Point(c.X + tip * 0.93, c.Y + half * 0.30), true, true);
            g.LineTo(new Point(c.X - tail, c.Y + half * 1.3), true, true);
        }
        body.Freeze();

        var ridge = new StreamGeometry();
        using (var g = ridge.Open())
        {
            g.BeginFigure(new Point(c.X - tail, c.Y - half * 1.3), true, true);
            g.LineTo(new Point(c.X + tip * 0.93, c.Y - half * 0.30), true, true);
            g.LineTo(new Point(c.X + tip, c.Y), true, true);
            g.LineTo(new Point(c.X - tail, c.Y), true, true);
        }
        ridge.Freeze();

        // Not clamped to the scale: the needle may overshoot a little while it settles.
        double angle = StartAngle + SweepAngle * Math.Clamp(v, -4, 104) / 100;

        // Shadow on the face, a little below the needle.
        foreach (var (dx, dy, alpha) in new[] { (0.018, 0.040, 70), (0.026, 0.056, 40) })
        {
            dc.PushTransform(new TranslateTransform(rf * dx, rf * dy));
            dc.PushTransform(new RotateTransform(angle, c.X, c.Y));
            dc.DrawGeometry(Solid(Color.FromArgb((byte)alpha, 0, 0, 0)), null, body);
            dc.Pop();
            dc.Pop();
        }

        dc.PushTransform(new RotateTransform(angle, c.X, c.Y));
        dc.DrawGeometry(Solid(Rgb(0xE2390B)), new Pen(Solid(Color.FromArgb(150, 0x6B, 0x14, 0x00)), 0.6), body);
        dc.DrawGeometry(Solid(Rgb(0xFF8046)), null, ridge);
        dc.Pop();

        // Hub.
        double hub = rf * 0.125;
        dc.DrawEllipse(Solid(Color.FromArgb(90, 0, 0, 0)), null, new Point(c.X + rf * 0.012, c.Y + rf * 0.03), hub * 1.08, hub * 1.08);
        var metal = new RadialGradientBrush { GradientOrigin = new Point(0.34, 0.28), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
        metal.GradientStops.Add(new GradientStop(Rgb(0xFFFFFF), 0));
        metal.GradientStops.Add(new GradientStop(Rgb(0xB4BCC3), 0.45));
        metal.GradientStops.Add(new GradientStop(Rgb(0x4A5158), 1));
        dc.DrawEllipse(metal, new Pen(Solid(Rgb(0x14181B)), Math.Max(0.6, rf * 0.012)), c, hub, hub);

        var cap = new RadialGradientBrush { GradientOrigin = new Point(0.36, 0.30), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
        cap.GradientStops.Add(new GradientStop(Rgb(0x4B535B), 0));
        cap.GradientStops.Add(new GradientStop(Rgb(0x0B0D0F), 1));
        dc.DrawEllipse(cap, null, c, hub * 0.52, hub * 0.52);
    }

    private static void DrawGlass(DrawingContext dc, Point c, double rf)
    {
        // The curved reflection across the upper half.
        var gloss = new LinearGradientBrush(Color.FromArgb(132, 255, 255, 255), Color.FromArgb(8, 255, 255, 255), 90);
        dc.DrawGeometry(gloss, null, new EllipseGeometry(new Point(c.X, c.Y - rf * 0.62), rf * 1.10, rf * 0.66));

        // Light caught by the lower rim.
        dc.DrawGeometry(null, new Pen(Solid(Color.FromArgb(46, 255, 255, 255)), rf * 0.018), Arc(c, rf * 0.975, 28, 152));
    }

    // ---- helpers ----

    private static double AngleFor(double value) => StartAngle + SweepAngle * Math.Clamp(value, 0, 100) / 100;

    private static Point Polar(Point c, double r, double degrees)
    {
        double rad = degrees * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
    }

    private static Geometry Arc(Point c, double r, double fromDegrees, double toDegrees)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(Polar(c, r, fromDegrees), false, false);
            g.ArcTo(Polar(c, r, toDegrees), new Size(r, r), 0, Math.Abs(toDegrees - fromDegrees) > 180, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static FormattedText Formatted(string text, Typeface face, double size) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Brushes.White, 1.0);

    private static Geometry Text(string text, Typeface face, double size, Point center)
    {
        var ft = Formatted(text, face, size);
        return ft.BuildGeometry(new Point(center.X - ft.WidthIncludingTrailingWhitespace / 2, center.Y - ft.Height / 2));
    }

    private static Geometry Tracked(string text, Typeface face, double size, double tracking, Point center)
    {
        var glyphs = new List<FormattedText>();
        double total = 0;
        foreach (char ch in text)
        {
            var ft = Formatted(ch.ToString(), face, size);
            glyphs.Add(ft);
            total += ft.WidthIncludingTrailingWhitespace + tracking;
        }
        total -= tracking;

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        double x = center.X - total / 2;
        foreach (var ft in glyphs)
        {
            group.Children.Add(ft.BuildGeometry(new Point(x, center.Y - ft.Height / 2)));
            x += ft.WidthIncludingTrailingWhitespace + tracking;
        }
        return group;
    }

    private static SolidColorBrush Solid(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color Rgb(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));
}
