using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeUsageWidget.Skins;

/// <summary>
/// The electronic sign over one lane of the road: the limit's name, the time left until its reset and how much of it
/// is used. The time glows green while much of the window is still to run, turns orange and then red as the reset
/// nears. Under the pointer the time left gives way to the time of day of the reset, as on the gauges.
/// </summary>
public sealed class RoadSign : FrameworkElement
{
    // The texts decide how long the sign is.
    public static readonly DependencyProperty LabelProperty = Register("Label", "", Sizing);
    public static readonly DependencyProperty PercentTextProperty = Register("PercentText", "—", Sizing);
    public static readonly DependencyProperty LevelProperty = Register("Level", "ok");
    public static readonly DependencyProperty CountdownProperty = Register("Countdown", "", Sizing);
    public static readonly DependencyProperty ResetTextProperty = Register("ResetText", "", Sizing);
    public static readonly DependencyProperty ResetClockProperty = Register("ResetClock", "", Sizing);
    public static readonly DependencyProperty TimeLeftProperty = Register("TimeLeft", double.NaN);
    public static readonly DependencyProperty HoveredProperty = DependencyProperty.Register("Hovered", typeof(bool), typeof(RoadSign),
        new FrameworkPropertyMetadata(false, (d, _) => ((RoadSign)d).OnHoveredChanged()));

    // Which of the two times shows: 0 is the time left, 1 the time of day. In between, one fades into the other.
    private static readonly DependencyProperty FlipProperty = Register("Flip", 0.0);

    private const FrameworkPropertyMetadataOptions Sizing =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

    private static DependencyProperty Register<T>(string name, T defaultValue,
        FrameworkPropertyMetadataOptions options = FrameworkPropertyMetadataOptions.AffectsRender) =>
        DependencyProperty.Register(name, typeof(T), typeof(RoadSign), new FrameworkPropertyMetadata(defaultValue, options));

    /// <summary>The board, and under it the two posts it stands on, down to the edge of the road.</summary>
    private const double BoardHeight = 20, WholeHeight = 30;
    private const double TextSize = BoardHeight * 0.56;
    private const double Pad = 5.5, AfterLabel = 12, BeforePercent = 8;

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string PercentText { get => (string)GetValue(PercentTextProperty); set => SetValue(PercentTextProperty, value); }
    public string Level { get => (string)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public string Countdown { get => (string)GetValue(CountdownProperty); set => SetValue(CountdownProperty, value); }
    public string ResetText { get => (string)GetValue(ResetTextProperty); set => SetValue(ResetTextProperty, value); }
    public string ResetClock { get => (string)GetValue(ResetClockProperty); set => SetValue(ResetClockProperty, value); }
    public double TimeLeft { get => (double)GetValue(TimeLeftProperty); set => SetValue(TimeLeftProperty, value); }
    public bool Hovered { get => (bool)GetValue(HoveredProperty); set => SetValue(HoveredProperty, value); }

    /// <summary>Road signs use a narrow, plain face. Bahnschrift is Windows' own road-sign type; Segoe UI stands in without it.</summary>
    private static readonly FontFamily Face = new("Bahnschrift, Segoe UI");
    private static readonly Typeface Caption = new(Face, FontStyles.Normal, FontWeights.SemiBold, FontStretches.SemiCondensed);
    private static readonly Typeface Digits = new(Face, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface Strong = new(Face, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static readonly Color Green = CarArt.Rgb(0x4DF26A), Orange = CarArt.Rgb(0xFFA21E), Red = CarArt.Rgb(0xFF3B2E);
    private static readonly Color Neutral = CarArt.Rgb(0xBFD3E0);

    private void OnHoveredChanged()
    {
        double flip = Hovered ? 1 : 0;
        if (!IsLoaded)
        {
            BeginAnimation(FlipProperty, null);
            SetValue(FlipProperty, flip);
            return;
        }
        BeginAnimation(FlipProperty, new DoubleAnimation(flip, TimeSpan.FromMilliseconds(240)));
    }

    /// <summary>Green while half the window or more is still to run, orange at a quarter, red from a tenth.</summary>
    internal static Color TimeColor(double left)
    {
        if (double.IsNaN(left)) return Neutral;
        if (left >= 0.5) return Green;
        if (left >= 0.25) return CarArt.Mix(Orange, Green, (left - 0.25) / 0.25);
        if (left >= 0.1) return CarArt.Mix(Red, Orange, (left - 0.1) / 0.15);
        return Red;
    }

    /// <summary>The time left, and the time of day of the reset that replaces it under the pointer.</summary>
    private (string Left, string Clock) Times()
    {
        string left = string.IsNullOrEmpty(Countdown) ? ResetText : Countdown;
        return (left, string.IsNullOrEmpty(ResetClock) ? left : ResetClock);
    }

    /// <summary>
    /// How wide each part of the sign is. The time takes the room of the longer of its two texts, so the sign does
    /// not change length when the pointer comes or goes.
    /// </summary>
    private (double Percent, double Board) Layout()
    {
        var (left, clock) = Times();
        double label = TextWidth(Label, Caption, TextSize * 0.86);
        double time = Math.Max(TextWidth(left, Digits, TextSize), TextWidth(clock, Digits, TextSize));
        double percent = TextWidth(PercentText, Strong, TextSize * 1.08);
        double board = Pad + label + (time > 0 ? AfterLabel + time : 0) + BeforePercent + percent + Pad;
        return (percent, Math.Ceiling(board));
    }

    protected override Size MeasureOverride(Size available) => new(Layout().Board, WholeHeight);

    protected override void OnRender(DrawingContext dc)
    {
        var (percentWidth, w) = Layout();
        double h = BoardHeight;

        // Two posts down to the road, just in from the ends of the board.
        var post = new LinearGradientBrush(CarArt.Rgb(0xC9D1D7), CarArt.Rgb(0x7C868D), 0);
        dc.DrawRectangle(post, null, new Rect(7, h - 1, 2.4, WholeHeight - h + 1));
        dc.DrawRectangle(post, null, new Rect(w - 9.4, h - 1, 2.4, WholeHeight - h + 1));

        // A dark board in a light metal frame, with a sheen across its top half.
        var board = new Rect(0, 0, w, h);
        var frame = new LinearGradientBrush(new GradientStopCollection
        {
            new(CarArt.Rgb(0xE6EBEF), 0), new(CarArt.Rgb(0x8E989F), 0.5), new(CarArt.Rgb(0x5A6268), 1),
        }, 90);
        dc.DrawRoundedRectangle(frame, null, board, 2.5, 2.5);
        var face = new Rect(1.4, 1.4, w - 2.8, h - 2.8);
        var black = new LinearGradientBrush(CarArt.Rgb(0x1C2228), CarArt.Rgb(0x07090B), 90);
        dc.DrawRoundedRectangle(black, null, face, 1.6, 1.6);

        double mid = h / 2;

        // The name, quiet, on the left.
        Glow(dc, Text(Label, Caption, TextSize * 0.86, new Point(Pad, mid), 0), CarArt.Rgb(0xE4ECF2), 0.85);

        // The percentage on the right, in the colour the other skins use for it.
        Color level = Level switch { "critical" => CarArt.Rgb(0xFF4A3A), "warn" => CarArt.Rgb(0xFFC83A), _ => CarArt.Rgb(0xF4F7FA) };
        Glow(dc, Text(PercentText, Strong, TextSize * 1.08, new Point(w - Pad, mid), 1), level, 1);

        // The time, right-aligned before the percentage, in the colour of how much of the window is left.
        var (left, clock) = Times();
        double flip = (double)GetValue(FlipProperty);
        double timeRight = w - Pad - percentWidth - BeforePercent;
        dc.PushOpacity(clock == left ? 1 : Math.Abs(flip - 0.5) * 2);
        Glow(dc, Text(flip < 0.5 ? left : clock, Digits, TextSize, new Point(timeRight, mid), 1), TimeColor(TimeLeft), 1);
        dc.Pop();

        // Glass over the board.
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromArgb(0x30, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90),
            null, new Rect(face.Left, face.Top, face.Width, face.Height * 0.48), 1.6, 1.6);
    }

    private static double TextWidth(string text, Typeface face, double size) => string.IsNullOrEmpty(text) ? 0
        : new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Brushes.White, 1.0).WidthIncludingTrailingWhitespace;

    /// <summary>Lit letters: a soft halo round them, as on a real LED board.</summary>
    private static void Glow(DrawingContext dc, Geometry text, Color color, double strength)
    {
        if (text.Bounds.IsEmpty) return;
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(40 * strength), color.R, color.G, color.B)), 2.4)
        {
            LineJoin = PenLineJoin.Round,
        }, text);
        dc.DrawGeometry(new SolidColorBrush(color), null, text);
    }

    /// <summary>Text as a shape, standing at <paramref name="at"/> with its left (0) or right (1) end, centred in height.</summary>
    private static Geometry Text(string text, Typeface face, double size, Point at, double align)
    {
        if (string.IsNullOrEmpty(text)) return Geometry.Empty;
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Brushes.White, 1.0);
        double width = ft.WidthIncludingTrailingWhitespace;
        return ft.BuildGeometry(new Point(at.X - width * align, at.Y - ft.Height / 2));
    }
}
