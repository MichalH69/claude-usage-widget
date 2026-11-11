using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeUsageWidget.Skins;

/// <summary>
/// A fish swimming along a ruler etched into the tank glass. Its nose points at the used share of the limit,
/// and its eye follows the pointer while that is over the widget. The tank drives it through <see cref="Animate"/>.
/// </summary>
public sealed class FishLane : FrameworkElement
{
    /// <summary>Nose to tail tip.</summary>
    private const double FishLength = 46;

    /// <summary>From this far away the eye turns all the way. Closer, it eases back to the middle.</summary>
    private const double FullTurnDistance = 34;

    private static readonly Vector RestGaze = new(0.35, 0);

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(FishLane),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((FishLane)d).Swim()));
    public static readonly DependencyProperty KindProperty = Register(nameof(Kind), FishKind.BlueTang);

    // Where the nose is right now, in percent. It trails Value, so the fish swims to a new reading.
    private static readonly DependencyProperty ShownProperty = Register("Shown", 0.0);

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(FishLane),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Used percent, 0–100. NaN means no data yet, and the lane stays empty.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public FishKind Kind { get => (FishKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    // Offsets and sizes of the bubbles a fish leaves behind. Fixed, so the wake does not flicker on redraw.
    private static readonly (double Dy, double R)[] Wake =
    {
        (1.5, 1.4), (-2.5, 2.2), (2.0, 1.1), (-0.5, 1.8), (3.0, 1.3), (-3.0, 2.5), (0.5, 1.2), (-1.5, 1.9),
    };

    private Window? window;

    /// <summary>Where the eye looks, as a vector no longer than 1, and where it is turning to.</summary>
    private Vector gaze = RestGaze, gazeTarget = RestGaze;

    /// <summary>The pointer in the lane's coordinates while it is over the widget.</summary>
    private Point? pointer;

    /// <summary>Seconds of the tank's clock. Every movement of the fish is a function of it.</summary>
    private double time;

    /// <summary>Each fish keeps its own rhythm, so the three do not move in step.</summary>
    private double Phase => Kind switch { FishKind.Clownfish => 2.1, FishKind.PurpleTang => 4.3, _ => 0 };

    public FishLane()
    {
        Loaded += (_, _) =>
        {
            Swim(fromStart: true);
            Watch(Window.GetWindow(this));
        };
        Unloaded += (_, _) => Watch(null);
    }

    private double Shown => (double)GetValue(ShownProperty);
    private double MidY => Math.Round((ActualHeight - 3) / 2) + 0.5;

    private double NoseAt(double percent)
    {
        double start = FishLength + 4;
        return start + (ActualWidth - start) * percent / 100;
    }

    private void Swim(bool fromStart = false)
    {
        double target = double.IsNaN(Value) ? 0 : Math.Clamp(Value, 0, 100);
        if (!IsLoaded || PresentationSource.FromVisual(this) is null)
        {
            // Not on screen: a still image must show the fish where it belongs.
            BeginAnimation(ShownProperty, null);
            SetValue(ShownProperty, target);
            return;
        }
        var swim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(1400))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        if (fromStart) swim.From = 0;
        BeginAnimation(ShownProperty, swim);
    }

    private void Watch(Window? next)
    {
        if (window is not null)
        {
            window.PreviewMouseMove -= OnPointerMoved;
            window.MouseLeave -= OnPointerLeft;
        }
        window = next;
        if (window is not null)
        {
            window.PreviewMouseMove += OnPointerMoved;
            window.MouseLeave += OnPointerLeft;
        }
    }

    private void OnPointerMoved(object sender, MouseEventArgs e) => pointer = e.GetPosition(this);

    private void OnPointerLeft(object sender, MouseEventArgs e) => pointer = null;

    /// <summary>The pointer has left the widget, as the window sees it.</summary>
    internal void PointerGone() => pointer = null;

    /// <summary>How far the fish drifts from its place right now: a slow hover up and down and a little back and forth.</summary>
    private Vector Drift => new(
        0.7 * Math.Sin(time * 0.53 + Phase * 1.7),
        1.3 * Math.Sin(time * 1.15 + Phase) + 0.4 * Math.Sin(time * 2.3 + Phase * 0.6));

    /// <summary>One step of the tank's clock: the fish hovers, its fins beat and its eye turns towards the pointer.</summary>
    internal void Animate(double now, double step)
    {
        time = now;
        if (double.IsNaN(Value) || ActualWidth <= 0) return;

        if (pointer is { } at)
        {
            var (eye, _) = FishArt.EyeOf(Kind);
            double scale = FishLength / 100;
            Point eyeAt = new Point(NoseAt(Shown) - FishLength + eye.X * scale, MidY + eye.Y * scale) + Drift;
            Vector toPointer = at - eyeAt;
            double distance = toPointer.Length;
            gazeTarget = distance < 0.5 ? default : toPointer / distance * Math.Min(1, distance / FullTurnDistance);
        }
        else
        {
            gazeTarget = RestGaze;
        }
        // The eye closes the gap a little every frame, quickly towards the pointer and lazily back to rest.
        double rate = pointer is null ? 5 : 16;
        gaze += (gazeTarget - gaze) * (1 - Math.Exp(-rate * step));

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double mid = MidY;
        double start = NoseAt(0);
        double baseY = Math.Round(h) - 1.5;

        // The ruler: 0, 25, 50, 75 and 100, with the last stretch marked as shallow water.
        var etch = new Pen(new SolidColorBrush(Color.FromArgb(92, 255, 255, 255)), 1);
        dc.DrawLine(etch, new Point(start, baseY), new Point(w, baseY));
        for (int q = 0; q <= 4; q++)
        {
            double x = Math.Min(w - 0.5, Math.Round(NoseAt(q * 25)) + 0.5);
            dc.DrawLine(etch, new Point(x, baseY - (q % 2 == 0 ? 5 : 3)), new Point(x, baseY));
        }
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 0xFF, 0xC2, 0x4D)), 2),
            new Point(NoseAt(GaugeDial.WarnAt), baseY), new Point(NoseAt(GaugeDial.CriticalAt), baseY));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(215, 0xFF, 0x62, 0x4E)), 2),
            new Point(NoseAt(GaugeDial.CriticalAt), baseY), new Point(w, baseY));

        if (double.IsNaN(Value)) return;
        double nose = NoseAt(Shown);
        DrawWake(dc, nose - FishLength - 5, mid, time);

        Vector drift = Drift;
        double scale = FishLength / 100;
        // A slight nod: the nose dips as the fish sinks and lifts as it rises.
        double pitch = 2.2 * Math.Cos(time * 1.15 + Phase);
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, nose - FishLength + drift.X, mid + drift.Y));
        dc.PushTransform(new RotateTransform(pitch, 60, 0));
        DrawFish(dc, FishArt.Get(Kind), time + Phase);
        FishArt.DrawPupil(dc, Kind, gaze);
        dc.Pop();
        dc.Pop();
    }

    /// <summary>
    /// The fish beats its tail and fins while it holds its place. Seen from the side, a tail that swings towards
    /// and away from the viewer looks narrower and wider, so it is squeezed about its root and bends a little.
    /// </summary>
    private static void DrawFish(DrawingContext dc, FishParts fish, double t)
    {
        double beat = Math.Sin(t * 5.2);

        var tail = new TransformGroup();
        tail.Children.Add(new ScaleTransform(0.86 + 0.14 * beat, 1, fish.TailRoot.X, fish.TailRoot.Y));
        tail.Children.Add(new RotateTransform(3.5 * Math.Sin(t * 5.2 - 0.9), fish.TailRoot.X, fish.TailRoot.Y));
        dc.PushTransform(tail);
        dc.DrawDrawing(fish.Tail);
        dc.Pop();

        // The long fins along the back and belly ripple: top and bottom lean opposite ways and breathe a little.
        var fins = new TransformGroup();
        fins.Children.Add(new ScaleTransform(1, 1 + 0.05 * Math.Sin(t * 2.6), 60, 0));
        fins.Children.Add(new SkewTransform(5 * Math.Sin(t * 2.6 + 1.2), 0, 60, 0));
        dc.PushTransform(fins);
        dc.DrawDrawing(fish.Fins);
        dc.Pop();

        dc.DrawDrawing(fish.Body);

        // The pectoral fin paddles, which is what keeps a fish in one place.
        double paddle = Math.Sin(t * 7.4);
        var pectoral = new TransformGroup();
        pectoral.Children.Add(new ScaleTransform(0.8 + 0.2 * Math.Abs(paddle), 1, fish.PectoralRoot.X, fish.PectoralRoot.Y));
        pectoral.Children.Add(new RotateTransform(16 * paddle, fish.PectoralRoot.X, fish.PectoralRoot.Y));
        dc.PushTransform(pectoral);
        dc.DrawDrawing(fish.Pectoral);
        dc.Pop();
    }

    /// <summary>The bubbles the fish has left stay where they are and only sway a little about that place.</summary>
    private static void DrawWake(DrawingContext dc, double fromX, double mid, double time)
    {
        for (int i = 0; ; i++)
        {
            double rest = fromX - i * 9;
            if (rest < 3) break;
            var (dy, r) = Wake[i % Wake.Length];
            double x = rest + 0.8 * Math.Sin(time * 1.3 + i * 1.9);
            double y = mid + dy - Math.Min(8, i * 0.9) + 1.3 * Math.Sin(time * 1.7 + i * 0.85);
            byte alpha = (byte)Math.Max(70, 215 - i * 12);
            var white = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(alpha / 4), 255, 255, 255)), new Pen(white, 0.7), new Point(x, y), r, r);
            dc.DrawEllipse(white, null, new Point(x - r * 0.35, y - r * 0.35), r * 0.28, r * 0.28);
        }
    }
}
