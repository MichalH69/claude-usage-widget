using System.Windows;
using System.Windows.Media;

namespace ClaudeUsageWidget.Skins;

/// <summary>Something in the tank that moves with the tank's clock.</summary>
internal interface ITankLife
{
    /// <summary>One step of the clock: <paramref name="now"/> in seconds since the tank started, <paramref name="step"/> since the last step.</summary>
    void Animate(double now, double step);
}

/// <summary>
/// A layer of the tank whose pieces are separate visuals. Each piece is redrawn on its own, so a frame repaints
/// only the small spots where something moved, not the whole tank.
/// </summary>
public abstract class TankLayer : FrameworkElement
{
    private readonly List<DrawingVisual> pieces = new();

    protected TankLayer() => IsHitTestVisible = false;

    protected DrawingVisual AddPiece()
    {
        var piece = new DrawingVisual();
        pieces.Add(piece);
        AddVisualChild(piece);
        return piece;
    }

    protected void RemovePiece(DrawingVisual piece)
    {
        RemoveVisualChild(piece);
        pieces.Remove(piece);
    }

    protected override int VisualChildrenCount => pieces.Count;

    protected override Visual GetVisualChild(int index) => pieces[index];

    internal static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

/// <summary>Bubbles rising from the air stone to the surface, growing as they go and wobbling from side to side.</summary>
public sealed class RisingBubbles : TankLayer, ITankLife
{
    private const int Count = 8;
    private const double Period = 6.5;

    /// <summary>The air stone and the surface, in the tank's coordinates.</summary>
    private static readonly Point Source = new(209, 204);
    private const double Surface = 15;

    private static readonly Brush Fill = Frozen(new RadialGradientBrush
    {
        GradientOrigin = new Point(0.32, 0.28),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0xD8, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(0x14, 255, 255, 255), 0.55),
            new GradientStop(Color.FromArgb(0x78, 255, 255, 255), 1),
        },
    });

    private readonly DrawingVisual[] bubbles;

    public RisingBubbles()
    {
        bubbles = Enumerable.Range(0, Count).Select(_ => AddPiece()).ToArray();
        Animate(0, 0);
    }

    public void Animate(double now, double step)
    {
        double time = now + 2.4;
        for (int i = 0; i < Count; i++)
        {
            // Each bubble is somewhere on its way up, and the way up takes the same time for all of them.
            double cycle = time / Period + (double)i / Count + 0.07 * Math.Sin(i * 2.7);
            double up = cycle - Math.Floor(cycle);

            // A bubble speeds up a little as it rises.
            double y = Source.Y - Math.Pow(up, 0.85) * (Source.Y - Surface);
            double x = Source.X + 2.4 * Math.Sqrt(up) * Math.Sin(time * 2.3 + i * 2.1) + 3 * up * Math.Sin(i * 1.3);
            double r = 1.6 + 3.4 * up;
            double fade = Math.Min(1, Math.Min(up / 0.04, (1 - up) / 0.05));

            using DrawingContext dc = bubbles[i].RenderOpen();
            dc.PushOpacity(fade);
            var rim = new Pen(new SolidColorBrush(Color.FromArgb((byte)(0x90 + 0x20 * up), 255, 255, 255)), 0.6 + 0.1 * up);
            dc.DrawEllipse(Fill, rim, new Point(x, y), r, r);
            dc.Pop();
        }
    }
}

/// <summary>A small fish far off in the tank: where it is, where it is going, and which way it faces.</summary>
internal sealed class SmallFish
{
    public Point At;
    public Vector Velocity;
    public Point Target;
    public double Cruise;
    public double Length;

    /// <summary>1 facing right, −1 facing left. In between the fish is turning and is seen edge-on.</summary>
    public double Facing = 1;

    public double Phase;

    /// <summary>Swims towards the target: picks up speed gently, turns gently, and faces the way it goes.</summary>
    public void Steer(double step, double cruise)
    {
        Vector toTarget = Target - At;
        double distance = toTarget.Length;
        Vector wanted = distance < 0.01 ? default : toTarget / distance * Math.Min(cruise, distance * 0.9 + 0.5);
        Velocity += (wanted - Velocity) * (1 - Math.Exp(-1.1 * step));
        At += Velocity * step;

        if (Math.Abs(Velocity.X) > 0.6) Facing += (Math.Sign(Velocity.X) - Facing) * (1 - Math.Exp(-5 * step));
        Phase += step * (4 + Velocity.Length * 0.6);
    }

    public void Draw(DrawingContext dc, SmallFishLook look)
    {
        double tilt = Math.Clamp(Math.Atan2(Velocity.Y, Math.Abs(Velocity.X) + 2) * 180 / Math.PI, -24, 24);
        double scale = Length / 100;
        dc.PushTransform(new TranslateTransform(At.X, At.Y));
        dc.PushTransform(new RotateTransform(tilt * Math.Sign(Facing)));
        // The fish is drawn with its middle at its place, so that it turns about its middle.
        dc.PushTransform(new MatrixTransform(scale * Facing, 0, 0, scale, 0, 0));
        dc.PushTransform(new TranslateTransform(-55, 0));

        dc.PushTransform(new ScaleTransform(0.78 + 0.22 * Math.Sin(Phase), 1, look.TailRoot, 0));
        dc.DrawGeometry(look.TailFill, look.Edge, look.Tail);
        dc.Pop();
        if (look.Fins is not null) dc.DrawGeometry(look.FinFill, look.Edge, look.Fins);
        dc.DrawGeometry(look.BodyFill, look.Edge, look.Body);
        dc.PushClip(look.Patterned);
        foreach (var (shape, fill) in look.Marks) dc.DrawGeometry(fill, null, shape);
        dc.Pop();
        dc.DrawEllipse(SmallFishLook.EyeFill, null, look.Eye, look.EyeSize, look.EyeSize);
        dc.DrawEllipse(SmallFishLook.Glint, null, look.Eye + new Vector(look.EyeSize * 0.3, -look.EyeSize * 0.35), look.EyeSize * 0.32, look.EyeSize * 0.32);

        dc.Pop();
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }
}

/// <summary>
/// One kind of small fish, drawn side on with its tail at 0 and its nose at 100. The marks are painted inside the
/// body and fins only, the way a real fish's colours stay within its outline.
/// </summary>
internal sealed record SmallFishLook(
    Geometry Body, Brush BodyFill,
    Geometry Tail, double TailRoot, Brush TailFill,
    Geometry? Fins, Brush? FinFill,
    (Geometry Shape, Brush Fill)[] Marks,
    Point Eye, double EyeSize, Pen? Edge, double Speed)
{
    public static readonly Brush EyeFill = Solid(0x1C, 0x22, 0x2A);
    public static readonly Brush Glint = TankLayer.Frozen(new SolidColorBrush(Color.FromArgb(0xC0, 255, 255, 255)));

    /// <summary>Where the marks may go: the body and, for fish whose fins carry the pattern, the fins.</summary>
    public Geometry Patterned { get; } = Fins is null
        ? Body
        : TankLayer.Frozen(new CombinedGeometry(GeometryCombineMode.Union, Body, Fins));

    private static readonly Geometry SlimBody = Shape(
        "M 100,0 C 97,-11 76,-17 50,-13 C 39,-11 31,-6 25,0 C 31,6 39,11 50,13 C 76,17 97,11 100,0 Z");
    private static readonly Geometry ForkedTail = Shape("M 29,0 L 3,-15 C 8,-5 8,5 3,15 Z");

    /// <summary>Neon tetra: a glowing blue line from eye to tail and red on the lower back half.</summary>
    public static readonly SmallFishLook NeonTetra = new(
        SlimBody, Gradient(0xD6, 0xDD, 0xD2, 0xA9, 0xB5, 0xAE),
        ForkedTail, 27, Solid(0xB8, 0xC2, 0xBC),
        Shape("M 56,-13 L 48,-22 L 41,-11 Z M 50,12.5 L 44,19 L 38,10 Z"), Solid(0xC4, 0xCC, 0xC6),
        new[]
        {
            (Shape("M 96,-5 C 76,-9 50,-8 28,-3 L 28,1 C 50,-2 76,-2 96,-1 Z"), Solid(0x24, 0xC8, 0xFF)),
            (Shape("M 72,1.5 C 58,1 42,1.5 26,1 L 26,14 L 72,14 Z"), Solid(0xE5, 0x2E, 0x3C)),
        },
        new Point(89, -2.5), 4.6, Outline(0x50, 0x2A, 0x3A, 0x40), 1);

    /// <summary>Zebra danio: slim and pale gold with long dark blue stripes running into the tail.</summary>
    public static readonly SmallFishLook ZebraDanio = new(
        Shape("M 100,0 C 97,-9 78,-13 52,-11 C 40,-9.5 31,-5 25,0 C 31,5 40,9.5 52,11 C 78,13 97,9 100,0 Z"),
        Gradient(0xF2, 0xEA, 0xC2, 0xD8, 0xCC, 0x9A),
        ForkedTail, 27, Solid(0xC9, 0xC6, 0xB0),
        null, null,
        new[]
        {
            (Shape("M 97,-4 C 76,-6 50,-5.5 25,-2 L 25,0 C 50,-3.2 76,-3.6 97,-2.2 Z"), Solid(0x2E, 0x4A, 0x9A)),
            (Shape("M 97,0.4 C 76,-0.6 50,0.4 25,1.2 L 25,3 C 50,2.6 76,1.8 97,2.2 Z"), Solid(0x2E, 0x4A, 0x9A)),
            (Shape("M 93,4.6 C 76,4.4 54,5.4 30,4.8 L 31,6.8 C 54,7.8 76,6.8 92,6.8 Z"), Solid(0x2E, 0x4A, 0x9A)),
        },
        new Point(90, -2), 4.2, Outline(0x50, 0x5A, 0x50, 0x30), 1.25);

    /// <summary>Guppy: a small grey body behind a big fan of a tail, orange running into blue.</summary>
    public static readonly SmallFishLook Guppy = new(
        Shape("M 100,0 C 98,-8 84,-11 66,-9 C 56,-7.5 48,-4 42,0 C 48,4 56,7.5 66,9 C 84,11 98,8 100,0 Z"),
        Gradient(0xD8, 0xD2, 0xCA, 0xB2, 0xAA, 0xA2),
        Shape("M 46,0 C 34,-5 18,-21 3,-19 C -3,-7 -3,7 3,19 C 18,21 34,5 46,0 Z"), 44,
        TankLayer.Frozen(new LinearGradientBrush(Color.FromRgb(0x3E, 0x7C, 0xF0), Color.FromRgb(0xFF, 0x8A, 0x3A), 0)),
        Shape("M 72,-9 C 66,-15 57,-15 53,-7 Z"), Solid(0xF0, 0x9A, 0x50),
        new[]
        {
            ((Geometry)TankLayer.Frozen(new EllipseGeometry(new Point(62, 1.5), 6, 3.8)), Solid(0x3A, 0x78, 0xE6)),
            (Shape("M 56,-9 L 44,0 L 56,9 L 52,0 Z"), Solid(0xF2, 0x7A, 0x34)),
        },
        new Point(91, -1.5), 3.8, Outline(0x50, 0x4A, 0x40, 0x3A), 1.1);

    /// <summary>Sunset platy: short and deep, orange with a darker, rounded tail.</summary>
    public static readonly SmallFishLook Platy = new(
        Shape("M 100,0 C 99,-12 82,-21 58,-19 C 42,-17 32,-9 26,0 C 32,9 42,17 58,19 C 82,21 99,12 100,0 Z"),
        Gradient(0xFF, 0xB8, 0x5A, 0xEE, 0x6A, 0x22),
        Shape("M 30,0 C 22,-6 10,-15 3,-14 C 0,-5 0,5 3,14 C 10,15 22,6 30,0 Z"), 28, Solid(0xD0, 0x50, 0x24),
        Shape("M 66,-19 C 60,-29 47,-29 42,-16 Z M 56,18 C 52,25 46,25 43,16 Z"), Solid(0xF0, 0x80, 0x34),
        new[] { (Shape("M 98,-7 C 88,-4 80,-3 70,-6 L 70,-22 L 100,-22 Z"), Solid(0xFF, 0xD2, 0x7A)) },
        new Point(88, -4), 5, Outline(0x58, 0x8A, 0x34, 0x10), 0.8);

    /// <summary>Angelfish: a tall silver disc with sail fins and dark bars across it. It glides slowly.</summary>
    public static readonly SmallFishLook Angelfish = new(
        Shape("M 100,-2 C 98,-14 84,-24 66,-24 C 52,-22 40,-14 30,0 C 40,14 52,22 66,24 C 84,24 98,12 100,-2 Z"),
        Gradient(0xF2, 0xF4, 0xF0, 0xBE, 0xC8, 0xC8),
        Shape("M 34,0 L 9,-15 C 13,-5 13,5 9,15 Z"), 32, Solid(0xC8, 0xD0, 0xD0),
        Shape("M 76,-22 C 62,-40 44,-58 30,-66 C 36,-46 40,-26 46,-16 Z M 76,22 C 62,40 44,58 30,66 C 36,46 40,26 46,16 Z"),
        TankLayer.Frozen(new SolidColorBrush(Color.FromArgb(0xD0, 0xD8, 0xE0, 0xE0))),
        new[]
        {
            (Shape("M 82,-40 L 88,-40 L 88,40 L 82,40 Z"), Solid(0x2C, 0x30, 0x36)),
            (Shape("M 60,-70 L 67,-70 L 67,70 L 60,70 Z"), Solid(0x2C, 0x30, 0x36)),
            (Shape("M 40,-70 L 45,-70 L 45,70 L 40,70 Z"), Solid(0x3A, 0x3E, 0x44)),
        },
        new Point(91, -5), 4.6, Outline(0x50, 0x40, 0x48, 0x50), 0.6);

    /// <summary>Harlequin rasbora: coppery pink with a black wedge on its back half.</summary>
    public static readonly SmallFishLook Harlequin = new(
        Shape("M 100,0 C 98,-11 80,-18 56,-16 C 42,-14 32,-7 25,0 C 32,7 42,14 56,16 C 80,18 98,11 100,0 Z"),
        Gradient(0xF4, 0xBE, 0xA0, 0xD8, 0x8C, 0x6C),
        ForkedTail, 27, Solid(0xE8, 0x8A, 0x6A),
        Shape("M 60,-16 L 52,-25 L 45,-14 Z"), Solid(0xE0, 0x90, 0x70),
        new[] { (Shape("M 74,-12 L 44,-2 L 30,0 L 44,3 L 74,9 C 66,3 66,-6 74,-12 Z"), Solid(0x26, 0x20, 0x26)) },
        new Point(89, -3), 4.6, Outline(0x50, 0x6A, 0x30, 0x20), 1);

    /// <summary>The little one that lives in the jar: a pale gold tetra, closer and so a touch clearer.</summary>
    public static readonly SmallFishLook Homebody = new(
        SlimBody, Solid(0xF2, 0xD7, 0x8E),
        ForkedTail, 27, Solid(0xE3, 0xB4, 0x62),
        null, null,
        new[] { (Shape("M 92,-4 C 72,-7 50,-6 30,-2 L 30,1 C 50,-1 72,-1 92,-1 Z"), Solid(0xFF, 0xF3, 0xCC)) },
        new Point(88, -2.5), 4.5, TankLayer.Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x8A, 0x5A, 0x1C)), 3)), 1);

    private static Geometry Shape(string data) => TankLayer.Frozen(Geometry.Parse(data));

    private static Brush Solid(byte r, byte g, byte b) => TankLayer.Frozen(new SolidColorBrush(Color.FromRgb(r, g, b)));

    /// <summary>Lighter on the back, darker on the belly, as light from above falls on a fish.</summary>
    private static Brush Gradient(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2) => TankLayer.Frozen(
        new LinearGradientBrush(Color.FromRgb(r1, g1, b1), Color.FromRgb(r2, g2, b2), 90));

    private static Pen Outline(byte a, byte r, byte g, byte b) =>
        TankLayer.Frozen(new Pen(new SolidColorBrush(Color.FromArgb(a, r, g, b)), 2.4));
}

/// <summary>
/// A few small fish of different kinds wandering all over the tank behind everything that matters. They stay
/// well under half the size of the fish that show the limits and are seen faded through the water, so nobody
/// takes them for those.
/// </summary>
public sealed class FishSchool : TankLayer, ITankLife
{
    private static readonly Rect Water = new(14, 34, 274, 150);

    private readonly Random random = new(11);
    private readonly SmallFish[] fish;
    private readonly SmallFishLook[] looks;
    private readonly DrawingVisual[] shapes;

    public FishSchool()
    {
        (double X, double Y, double Length, SmallFishLook Look)[] start =
        {
            (60, 52, 24, SmallFishLook.NeonTetra), (90, 62, 23, SmallFishLook.NeonTetra),
            (220, 96, 27, SmallFishLook.ZebraDanio), (150, 130, 25, SmallFishLook.Guppy),
            (248, 150, 25, SmallFishLook.Platy), (110, 120, 31, SmallFishLook.Angelfish),
            (180, 168, 24, SmallFishLook.Harlequin),
        };
        looks = start.Select(s => s.Look).ToArray();
        fish = start.Select((s, i) => new SmallFish
        {
            At = new Point(s.X, s.Y),
            Length = s.Length,
            Cruise = (8 + i % 3 * 2.2) * s.Look.Speed,
            Facing = i % 2 == 0 ? 1 : -1,
            Phase = i * 1.7,
        }).ToArray();
        foreach (SmallFish f in fish)
        {
            f.Target = NextTarget(f);
            f.Velocity = new Vector(f.Facing * 3, 0);
        }
        shapes = fish.Select(_ => AddPiece()).ToArray();
        Redraw();
    }

    /// <summary>Somewhere else in the water, mostly along the tank rather than up and down it.</summary>
    private Point NextTarget(SmallFish f)
    {
        double x = Water.Left + random.NextDouble() * Water.Width;
        // Bigger fish keep a little further from the surface and the sand.
        double room = f.Length * 0.4;
        double y = Math.Clamp(f.At.Y + (random.NextDouble() - 0.5) * 70, Water.Top + room, Water.Bottom - room);
        return new Point(x, y);
    }

    public void Animate(double now, double step)
    {
        foreach (SmallFish f in fish)
        {
            f.Steer(step, f.Cruise);
            if ((f.Target - f.At).Length < 8) f.Target = NextTarget(f);
        }
        Redraw();
    }

    private void Redraw()
    {
        for (int i = 0; i < fish.Length; i++)
        {
            using DrawingContext dc = shapes[i].RenderOpen();
            fish[i].Draw(dc, looks[i]);
        }
    }
}

/// <summary>
/// An old clay jar lying half sunk in the sand, and the little fish that lives in it. Now and then the fish swims
/// up to the mouth, slips inside, waits a while and comes out again.
/// </summary>
public sealed class JarHideout : TankLayer, ITankLife
{
    /// <summary>The jar lies on its side, its foot to the left and its mouth to the right, tipped up a little.</summary>
    private static readonly Transform Place = Frozen(new MatrixTransform(
        new RotateTransform(-7).Value * new TranslateTransform(132, 203).Value));

    private static readonly Geometry Body = Frozen(Geometry.Parse(
        "M 4,0 C 4,-8 10,-10.5 18,-10.5 C 26,-10.5 30,-7.5 33,-5.2 L 38,-5.4 C 39,-6.6 40,-7.2 41,-7.2 L 41,7.2 C 40,7.2 39,6.6 38,5.4 L 33,5.2 C 30,7.5 26,10.5 18,10.5 C 10,10.5 4,8 4,0 Z"));
    private static readonly Point MouthCenter = new(41, 0);
    private const double MouthRx = 2.5, MouthRy = 5.9;

    /// <summary>Where the fish waits before going in, and where it ends up inside, in the tank's coordinates.</summary>
    private static readonly Point Door = Place.Transform(new Point(58, 0));
    private static readonly Point Inside = Place.Transform(new Point(28, 0));

    /// <summary>Everything except the jar's wall, so the fish disappears into the jar but shows through its mouth.</summary>
    private static readonly Geometry OutsideWall = Frozen(new CombinedGeometry(GeometryCombineMode.Exclude,
        new RectangleGeometry(new Rect(-50, -50, 400, 330)),
        new CombinedGeometry(GeometryCombineMode.Exclude, Body, new EllipseGeometry(MouthCenter, MouthRx, MouthRy))
        {
            Transform = Place,
        }));

    private static readonly Brush Clay = Frozen(new LinearGradientBrush
    {
        StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0xE8, 0x9C, 0x6A), 0),
            new GradientStop(Color.FromRgb(0xC2, 0x6A, 0x3C), 0.45),
            new GradientStop(Color.FromRgb(0x7E, 0x3C, 0x22), 1),
        },
    });
    private static readonly Brush Dark = Frozen(new RadialGradientBrush
    {
        GradientOrigin = new Point(0.7, 0.5),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0x0A, 0x16, 0x22), 0),
            new GradientStop(Color.FromRgb(0x2A, 0x24, 0x22), 1),
        },
    });

    private enum Mood { Wander, ToDoor, GoIn, Hide, GoOut }

    private readonly Random random = new(5);
    private readonly SmallFish fish = new()
    {
        At = new Point(196, 172), Length = 13, Facing = -1, Velocity = new Vector(-3, 0),
    };
    private Mood mood = Mood.Wander;
    private double moodLeft = 7;

    private readonly DrawingVisual jar, dweller, drift;

    public JarHideout()
    {
        fish.Target = new Point(150, 168);
        // The fish swims between the jar and the sand drifted against it, which sit still and are drawn once.
        jar = AddPiece();
        dweller = AddPiece();
        drift = AddPiece();
        DrawJar();
        DrawFish();
    }

    public void Animate(double now, double step)
    {
        moodLeft -= step;
        switch (mood)
        {
            case Mood.Wander:
                fish.Steer(step, 8);
                if ((fish.Target - fish.At).Length < 6) fish.Target = Roam();
                if (moodLeft <= 0) (mood, fish.Target) = (Mood.ToDoor, Door);
                break;
            case Mood.ToDoor:
                fish.Steer(step, 8);
                if ((Door - fish.At).Length < 2.5) (mood, fish.Target) = (Mood.GoIn, Inside);
                break;
            case Mood.GoIn:
                fish.Steer(step, 5);
                if ((Inside - fish.At).Length < 1.5) (mood, moodLeft) = (Mood.Hide, 4 + random.NextDouble() * 8);
                break;
            case Mood.Hide:
                // Turned round in the dark, ready to come out nose first. Nothing to redraw while it waits.
                (fish.Velocity, fish.Facing) = (default, 1);
                if (moodLeft <= 0) (mood, fish.Target) = (Mood.GoOut, Door);
                return;
            case Mood.GoOut:
                fish.Steer(step, 5);
                if ((Door - fish.At).Length < 2.5) (mood, moodLeft, fish.Target) = (Mood.Wander, 12 + random.NextDouble() * 20, Roam());
                break;
        }
        DrawFish();
    }

    /// <summary>The fish keeps to the lower half of the tank, never far from home.</summary>
    private Point Roam() => new(30 + random.NextDouble() * 240, 110 + random.NextDouble() * 76);

    private void DrawFish()
    {
        using DrawingContext dc = dweller.RenderOpen();
        if (mood == Mood.Hide) return;
        dc.PushClip(OutsideWall);
        fish.Draw(dc, SmallFishLook.Homebody);
        dc.Pop();
    }

    private void DrawJar()
    {
        using (DrawingContext dc = jar.RenderOpen())
        {
            dc.PushTransform(Place);
            // The foot of the jar, then the jar, a highlight along it, the rim and the dark inside of the mouth.
            dc.DrawEllipse(Clay, new Pen(Brush(0xB0, 0x4A, 0x22, 0x10), 0.7), new Point(3.2, 0), 2.6, 3.4);
            dc.DrawGeometry(Clay, new Pen(Brush(0xC0, 0x4A, 0x22, 0x10), 0.8), Body);
            dc.DrawGeometry(null, new Pen(Brush(0x70, 0xFF, 0xE2, 0xC4), 1.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
                Geometry.Parse("M 10,-7 C 15,-9.2 23,-9.2 29,-6.6"));
            dc.DrawGeometry(null, new Pen(Brush(0x40, 0x2E, 0x6B, 0x2A), 2.2), Geometry.Parse("M 8,6 C 14,9 22,9.2 28,7"));
            dc.DrawEllipse(Brush(0xFF, 0xD6, 0x8A, 0x5C), new Pen(Brush(0xC0, 0x4A, 0x22, 0x10), 0.6), MouthCenter, MouthRx + 0.9, MouthRy + 1.3);
            dc.DrawEllipse(Dark, null, MouthCenter, MouthRx, MouthRy);
            dc.Pop();
        }

        // Sand drifted against the jar, so it lies in the sand rather than on it.
        using (DrawingContext dc = drift.RenderOpen())
        {
            var sand = new LinearGradientBrush(Color.FromRgb(0xF3, 0xE0, 0xA8), Color.FromRgb(0xD9, 0xBC, 0x7E), 90);
            dc.DrawGeometry(sand, null, Geometry.Parse("M 124,214 C 134,207 150,207.5 160,208.6 C 166,209.3 170,211 173,214 Z"));
            dc.DrawGeometry(null, new Pen(Brush(0x90, 0xFF, 0xFB, 0xE6), 0.8),
                Geometry.Parse("M 128,211.5 C 138,207.5 150,207.8 160,208.7 C 165,209.3 168,210.5 170,212"));
        }
    }

    private static Brush Brush(byte a, byte r, byte g, byte b) => new SolidColorBrush(Color.FromArgb(a, r, g, b));
}
