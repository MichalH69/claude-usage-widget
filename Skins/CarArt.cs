using System.Windows;
using System.Windows.Media;

namespace ClaudeUsageWidget.Skins;

/// <summary>
/// Cars seen from the side as flat illustrations, facing left. Each is laid out in a box 100 units long:
/// nose at x = 0, back at x = 100, the road at y = 0 and the roof at about y = −40.
/// </summary>
internal sealed record CarModel(string Body, string Glass, double FrontWheel, double RearWheel, double Back, double LightY)
{
    public Geometry BodyShape { get; } = Shape(Body);
    public Geometry GlassShape { get; } = Shape(Glass);

    /// <summary>The wheels' radius. Racing cars run on bigger wheels than a family car.</summary>
    public double WheelSize { get; init; } = CarArt.WheelRadius;

    /// <summary>Whether the body has wheel arches. A formula car's wheels stand free.</summary>
    public bool Wells { get; init; } = true;

    /// <summary>A camper's body is white or cream; its paint goes on the stripes and trim the extra draws.</summary>
    public bool Cream { get; init; }

    /// <summary>What makes the car what it is beyond its outline: a wing, a roof box, stripes, a race number.</summary>
    public Action<DrawingContext, Color>? Extra { get; init; }

    internal static Geometry Shape(string data)
    {
        Geometry g = string.IsNullOrEmpty(data) ? Geometry.Empty : Geometry.Parse(data);
        g.Freeze();
        return g;
    }
}

/// <summary>Which cars make up a lane's queues.</summary>
public enum Fleet
{
    /// <summary>A bit of everything: family cars, campers and racing cars.</summary>
    Mix,

    /// <summary>Camper vans, motorhomes and cars packed for a holiday.</summary>
    Campers,

    /// <summary>Racing cars, supercars, hypercars and formula cars.</summary>
    Racers,
}

internal static class CarArt
{
    public const double WheelRadius = 8.5;

    public static readonly CarModel Sedan = new(
        "M 2,-9 C 0,-13 1,-18 6,-20 L 25,-23 C 30,-29 35,-33 41,-35 C 52,-37 64,-37 72,-34 C 77,-31 81,-27 84,-23 L 96,-21 C 99,-20 100,-16 99.5,-12 L 99,-9 Z",
        "M 29,-23.5 C 33,-28 37,-31.5 42,-33 C 50,-34.5 58,-34.5 64,-33.5 L 64,-23.5 Z M 66.5,-23.5 L 66.5,-33 C 72,-32 76,-28.5 80,-23.5 Z",
        20, 80, 100, -16);

    public static readonly CarModel Hatchback = new(
        "M 4,-9 C 2,-13 3,-18 8,-20 L 24,-23 C 30,-30 36,-35 44,-36.5 C 58,-38 74,-37.5 84,-35 C 88,-31 91,-24 92,-18 L 92,-9 Z",
        "M 29,-23.5 C 34,-29 39,-33 45,-34.5 L 60,-35 L 60,-23.5 Z M 62.5,-23.5 L 62.5,-35 C 72,-35 79,-34 83,-33 C 85,-30 87,-27 88,-23.5 Z",
        21, 74, 92, -17);

    public static readonly CarModel Wagon = new(
        "M 2,-9 C 0,-13 1,-18 6,-20 L 24,-23 C 29,-29 34,-33 40,-35 L 94,-35.5 C 97,-35 98.5,-32 99,-28 L 99.5,-12 L 99,-9 Z",
        "M 28,-23.5 C 32,-28 36,-31.5 41,-33 L 58,-33.3 L 58,-23.5 Z M 60.5,-23.5 L 60.5,-33.3 L 77,-33.4 L 77,-23.5 Z M 79.5,-23.5 L 79.5,-33.4 L 92,-33.5 C 94,-33 95,-30 95.3,-23.5 Z",
        20, 80, 100, -18);

    public static readonly CarModel Suv = new(
        "M 2,-10 C 0,-15 1,-21 6,-23 L 22,-26 C 27,-33 31,-39 36,-41 L 92,-41.5 C 96,-41 98.5,-37 99,-31 L 99.5,-12 L 99,-10 Z",
        "M 26,-26.5 C 30,-32 33,-37 37,-39 L 57,-39.3 L 57,-26.5 Z M 59.5,-26.5 L 59.5,-39.3 L 77,-39.4 L 77,-26.5 Z M 79.5,-26.5 L 79.5,-39.4 L 92,-39.5 C 94,-39 95,-35 95.3,-26.5 Z",
        20, 80, 100, -20);

    public static readonly CarModel Van = new(
        "M 2,-9 C 0,-14 1,-20 5,-23 L 13,-27 C 17,-35 21,-43 27,-46 L 96,-46.5 C 99,-46 100,-43 100,-40 L 99.5,-9 Z",
        "M 15,-28 C 18,-35 22,-41 27,-43.5 L 34,-43.6 L 34,-28 Z M 37,-28 L 37,-43.6 L 55,-43.8 L 55,-28 Z",
        19, 81, 100, -22);

    public static readonly CarModel Coupe = new(
        "M 2,-8.5 C 0,-12 1,-16 6,-18 L 28,-21.5 C 34,-27 40,-31 48,-32.5 C 58,-33.5 68,-32 76,-27 L 96,-20 C 99,-19 100,-15 99.5,-11 L 99,-8.5 Z",
        "M 33,-22 C 38,-26.5 43,-29.5 49,-30.8 C 54,-31.4 58,-31.4 61,-31 L 61,-22 Z M 63.5,-22 L 63.5,-30.6 C 68,-29.8 72,-28 74,-26 L 70,-22 Z",
        20, 80, 100, -14);

    public static readonly CarModel[] Models = { Sedan, Hatchback, Wagon, Suv, Van, Coupe };

    // ---- racing cars ----

    /// <summary>A GT racing car: the coupe with a rear wing, a stripe along its flank and a number on its door.</summary>
    public static readonly CarModel RaceGt = new(Coupe.Body, Coupe.Glass, 20, 80, 100, -14)
    {
        WheelSize = 8.8,
        Extra = (dc, paint) =>
        {
            Stripe(dc, Coupe.BodyShape, -17.5, 2.6, Colors.White);
            dc.DrawEllipse(Brushes.White, Outline(0.6), new Point(52, -13.5), 5.4, 5.4);
            Number(dc, "27", new Point(52, -13.5), 6.4);
            RearWing(dc, 82, -31, 17, 8);
        },
    };

    /// <summary>A supercar: low and wedge-shaped, with an air intake behind the door.</summary>
    public static readonly CarModel Supercar = new(
        "M 1,-8 C 0,-11 2,-14 8,-15.5 L 30,-19 C 38,-25 46,-28.5 56,-29 C 66,-29 74,-26 80,-22 L 97,-19 C 99.5,-18 100,-14 99.5,-8 Z",
        "M 36,-20 C 42,-25 48,-27.3 56,-27.6 C 60,-27.6 63,-27 64,-26 L 62,-20 Z",
        20, 80, 100, -12.5)
    {
        WheelSize = 8.6,
        Extra = (dc, paint) =>
        {
            dc.DrawGeometry(Dark, null, Shape("M 62,-17.5 L 76,-20 L 75,-12.5 L 66,-12.5 Z"));
            dc.DrawRectangle(Dark, null, new Rect(1, -9, 28, 1.6));
        },
    };

    /// <summary>A hypercar: lower still, with a shark fin and a tall wing at the back.</summary>
    public static readonly CarModel Hypercar = new(
        "M 0.5,-7.5 C 0,-10.5 3,-13 10,-14 L 32,-17.5 C 40,-23.5 48,-26.5 57,-26.7 C 66,-26.5 72,-23.5 78,-19.5 L 98,-16.5 C 100,-14.5 100,-10.5 99.5,-7.5 Z",
        "M 39,-18.5 C 45,-23 51,-25 57,-25.2 L 64,-24.4 L 66,-19 Z",
        21, 79, 100, -11.5)
    {
        WheelSize = 8.2,
        Extra = (dc, paint) =>
        {
            dc.DrawGeometry(new SolidColorBrush(Mix(paint, Colors.Black, 0.35)), null, Shape("M 64,-24.5 L 90,-18 L 66,-19.5 Z"));
            dc.DrawGeometry(Dark, null, Shape("M 64,-15.5 L 78,-17.5 L 78,-10 L 68,-10 Z"));
            Stripe(dc, Shape("M 0.5,-7.5 C 0,-10.5 3,-13 10,-14 L 32,-17.5 C 40,-23.5 48,-26.5 57,-26.7 C 66,-26.5 72,-23.5 78,-19.5 L 98,-16.5 C 100,-14.5 100,-10.5 99.5,-7.5 Z"),
                -10.5, 1.4, Mix(paint, Colors.White, 0.6));
            RearWing(dc, 80, -35, 21, 15);
        },
    };

    /// <summary>A formula car: a slim body between free-standing wheels, the driver's helmet, wings at both ends.</summary>
    public static readonly CarModel Formula = new(
        "M 3,-6.5 L 3,-9.5 C 14,-10.5 26,-11.5 34,-12.5 C 40,-15.5 46,-16.8 52,-17 C 55,-20.5 61,-22.8 66,-21.6 C 69,-20.8 71,-19 72,-17 L 90,-15.5 L 93,-7 Z",
        "", 19, 83, 100, -11)
    {
        WheelSize = 9.6,
        Wells = false,
        Extra = (dc, paint) =>
        {
            dc.DrawEllipse(new RadialGradientBrush(Rgb(0xFFF07A), Rgb(0xE8B400)) { GradientOrigin = new Point(0.35, 0.3) },
                Outline(0.6), new Point(56.5, -19.5), 4.2, 4.2);
            dc.DrawRectangle(Dark, null, new Rect(53.2, -21, 4, 1.8));
            dc.DrawGeometry(new SolidColorBrush(paint), Outline(0.8), Shape("M 61,-17 C 63,-24 69,-25.5 73,-22 L 75,-16.5 Z"));
            dc.DrawRectangle(Dark, null, new Rect(-1, -5.5, 13, 2.2));
            dc.DrawRectangle(Dark, null, new Rect(-1, -9.5, 2.2, 6.2));
            RearWing(dc, 86, -27, 15, 10);
        },
    };

    // ---- campers ----

    /// <summary>A camper van in two tones: paint below the waist, cream above, a round badge on its nose.</summary>
    public static readonly CarModel CamperVan = new(
        "M 2,-9 C 0,-14 0,-22 2,-30 C 3,-38 8,-42 16,-42.5 L 92,-43 C 97,-43 99,-40 99.5,-35 L 99.5,-9 Z",
        "M 3.5,-30 C 4.5,-36 8,-39.5 14,-40 L 14,-30 Z M 18,-30 L 18,-40 L 36,-40 L 36,-30 Z M 39,-30 L 39,-40 L 57,-40 L 57,-30 Z M 60,-30 L 60,-40 L 78,-40 L 78,-30 Z M 81,-30 L 81,-40 L 95,-40.2 L 95,-30 Z",
        18, 82, 100, -17)
    {
        Cream = true,
        Extra = (dc, paint) =>
        {
            Geometry body = Shape("M 2,-9 C 0,-14 0,-22 2,-30 C 3,-38 8,-42 16,-42.5 L 92,-43 C 97,-43 99,-40 99.5,-35 L 99.5,-9 Z");
            dc.PushClip(body);
            dc.DrawRectangle(Lacquer(paint), null, new Rect(-2, -27, 104, 20));
            dc.Pop();
            dc.DrawGeometry(null, Outline(1.8), body);
            dc.DrawEllipse(Brushes.White, Outline(0.5), new Point(4.5, -23.5), 2.8, 2.8);
        },
    };

    /// <summary>A motorhome with a bed over the cab, coloured sweeps along its white side and a box on the roof.</summary>
    public static readonly CarModel Motorhome = new(
        "M 2,-9 C 0,-13 1,-19 5,-21 L 12,-23 C 14,-29 17,-33 21,-35 L 9,-37 C 7,-41 8,-50 12,-53 C 15,-55 20,-55.5 26,-55.5 L 97,-55.5 C 99.5,-55.5 100,-53 100,-50 L 100,-9 Z",
        "M 13.5,-23.5 C 15,-28.5 17.5,-32 20.5,-33.8 L 21,-23.5 Z M 40,-29 L 40,-41 L 57,-41 L 57,-29 Z M 70,-31 L 70,-42 L 90,-42 L 90,-31 Z",
        19, 81, 100, -18)
    {
        Cream = true,
        Extra = (dc, paint) =>
        {
            var brush = new SolidColorBrush(paint);
            dc.DrawGeometry(brush, null, Shape("M 22,-24 C 48,-30 70,-17 100,-23 L 100,-19 C 70,-13 48,-26 22,-20 Z"));
            dc.DrawGeometry(new SolidColorBrush(Mix(paint, Colors.White, 0.35)), null,
                Shape("M 30,-17.5 C 52,-21 72,-12 100,-16 L 100,-14 C 72,-10 52,-19 30,-15.5 Z"));
            dc.DrawRectangle(null, Outline(0.8), new Rect(60, -40, 7.5, 27));
            dc.DrawRectangle(new SolidColorBrush(Rgb(0xD6DADD)), Outline(0.6), new Rect(68, -59, 13, 3.6));
        },
    };

    /// <summary>A pickup with a camper on its back: painted cab, cream living box over the bed.</summary>
    public static readonly CarModel PickupCamper = new(
        "M 2,-9 C 0,-14 1,-20 6,-22 L 22,-25 C 26,-32 30,-37 35,-38 L 50,-38 L 50,-46 C 50,-49 52,-50 55,-50 L 97,-50 C 99,-50 100,-48 100,-45 L 100,-9 Z",
        "M 26,-25.5 C 29,-31 32,-35 35.5,-36 L 47,-36 L 47,-25.5 Z M 62,-34 L 62,-44 L 84,-44 L 84,-34 Z",
        20, 80, 100, -18)
    {
        Extra = (dc, paint) =>
        {
            dc.DrawGeometry(Cream, Outline(1.2), Shape("M 50,-24 L 50,-46 C 50,-49 52,-50 55,-50 L 97,-50 C 99,-50 100,-48 100,-45 L 100,-24 Z"));
            dc.DrawGeometry(Glass, Outline(0.6), Shape("M 62,-34 L 62,-44 L 84,-44 L 84,-34 Z"));
            dc.DrawRectangle(new SolidColorBrush(paint), null, new Rect(50.6, -29, 49, 2.2));
        },
    };

    /// <summary>An estate packed for the holidays, with a box on its roof.</summary>
    public static readonly CarModel RoofBoxWagon = new(Wagon.Body, Wagon.Glass, 20, 80, 100, -18)
    {
        Extra = (dc, paint) =>
        {
            dc.DrawRectangle(Dark, null, new Rect(48, -38, 1.6, 3));
            dc.DrawRectangle(Dark, null, new Rect(80, -38, 1.6, 3));
            dc.DrawGeometry(new LinearGradientBrush(Rgb(0x5A6168), Rgb(0x23272B), 90), Outline(0.8),
                Shape("M 40,-37.5 C 42,-43 52,-44.5 64,-44.5 L 86,-44 C 90,-43.5 92,-41 91,-37.5 Z"));
        },
    };

    private static readonly CarModel[] RaceModels = { RaceGt, Supercar, Hypercar, Formula };
    private static readonly CarModel[] CamperModels = { CamperVan, Motorhome, PickupCamper, RoofBoxWagon };

    private static readonly Color[] RacePaints =
    {
        Rgb(0xD8232A), Rgb(0xF2C21B), Rgb(0xF06A12), Rgb(0x8BD12A), Rgb(0x1F5FC8), Rgb(0x26292E), Rgb(0xF2F2EE), Rgb(0x1E9AA6),
    };

    private static readonly Color[] CamperPaints =
    {
        Rgb(0xE8711E), Rgb(0x8A5A34), Rgb(0x1E9AA6), Rgb(0x2E8B47), Rgb(0xD8232A), Rgb(0x1F5FC8), Rgb(0xC9A227),
    };

    /// <summary>The cars and paints a lane's queues are made of.</summary>
    public static (CarModel[] Models, Color[] Paints) Of(Fleet fleet) => fleet switch
    {
        Fleet.Racers => (RaceModels, RacePaints),
        Fleet.Campers => (CamperModels, CamperPaints),
        _ => (Models.Concat(CamperModels).Concat(RaceModels).ToArray(), Paints),
    };

    private static readonly Brush Dark = Frozen(new SolidColorBrush(Rgb(0x15171A)));
    private static readonly Brush Cream = Frozen(new LinearGradientBrush(Rgb(0xFBFAF4), Rgb(0xD8D4C6), 90));

    private static Pen Outline(double thickness) => new(new SolidColorBrush(Color.FromArgb(0xE0, 0x0A, 0x0C, 0x0E)), thickness);

    private static Geometry Shape(string data) => CarModel.Shape(data);

    private static Brush Lacquer(Color paint) => new LinearGradientBrush(new GradientStopCollection
    {
        new(Mix(paint, Colors.White, 0.45), 0),
        new(paint, 0.42),
        new(Mix(paint, Colors.Black, 0.18), 0.62),
        new(Mix(paint, Colors.Black, 0.45), 1),
    }, 90);

    /// <summary>A band along the car at height <paramref name="y"/>, kept within the body.</summary>
    private static void Stripe(DrawingContext dc, Geometry body, double y, double height, Color color)
    {
        dc.PushClip(body);
        dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(-2, y, 104, height));
        dc.Pop();
    }

    /// <summary>A rear wing: a dark blade on two struts that reach down to the body.</summary>
    private static void RearWing(DrawingContext dc, double x, double top, double width, double strut)
    {
        dc.DrawRectangle(Dark, null, new Rect(x + width * 0.3, top + 2, 1.6, strut));
        dc.DrawRectangle(Dark, null, new Rect(x + width * 0.75, top + 2, 1.6, strut));
        dc.DrawRoundedRectangle(Dark, null, new Rect(x, top, width, 2.8), 1, 1);
        dc.DrawRectangle(Dark, null, new Rect(x + width - 1.6, top - 2, 2.2, 7));
    }

    private static void Number(DrawingContext dc, string text, Point center, double size)
    {
        var ft = new System.Windows.Media.FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(new FontFamily("Bahnschrift, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size, Brushes.Black, 1.0);
        dc.DrawText(ft, new Point(center.X - ft.WidthIncludingTrailingWhitespace / 2, center.Y - ft.Height / 2));
    }

    /// <summary>Paints that read well on grey asphalt and against each other.</summary>
    public static readonly Color[] Paints =
    {
        Rgb(0xD8232A), Rgb(0x1F5FC8), Rgb(0xF2F2EE), Rgb(0xA9B0B8), Rgb(0xF2B21B), Rgb(0x2E8B47),
        Rgb(0x26292E), Rgb(0xE8711E), Rgb(0x1E9AA6), Rgb(0x7A2A8C), Rgb(0x8A5A34),
    };

    private static readonly Dictionary<(CarModel, Color), Drawing> Bodies = new();

    private static readonly Brush Glass = Frozen(new LinearGradientBrush(
        new GradientStopCollection { new(Rgb(0xB8DDF2), 0), new(Rgb(0x4C6E88), 0.55), new(Rgb(0x23394B), 1) }, 90));
    private static readonly Brush Tyre = Frozen(new SolidColorBrush(Rgb(0x17181A)));
    private static readonly Brush Rim = Frozen(new RadialGradientBrush(Rgb(0xF2F4F6), Rgb(0x8A9298)) { GradientOrigin = new Point(0.35, 0.3) });
    private static readonly Pen Spoke = Frozen(new Pen(new SolidColorBrush(Rgb(0x4A5056)), 1.3));
    private static readonly Brush Well = Frozen(new SolidColorBrush(Color.FromArgb(0xE0, 0x12, 0x14, 0x16)));
    private static readonly Brush Shadow = Frozen(new RadialGradientBrush(Color.FromArgb(0xC8, 0, 0, 0), Color.FromArgb(0, 0, 0, 0)));
    private static readonly Brush HeadLamp = Frozen(new SolidColorBrush(Rgb(0xFFF6D8)));
    private static readonly Brush TailLamp = Frozen(new SolidColorBrush(Rgb(0xB0141A)));
    private static readonly Brush BrakeLamp = Frozen(new SolidColorBrush(Rgb(0xFF3A30)));
    private static readonly Brush BrakeGlow = Frozen(new RadialGradientBrush(Color.FromArgb(0xB0, 0xFF, 0x30, 0x20), Color.FromArgb(0, 0xFF, 0x30, 0x20)));

    /// <summary>The car without its wheels, which turn and are drawn on their own.</summary>
    public static Drawing Body(CarModel model, Color paint)
    {
        if (Bodies.TryGetValue((model, paint), out Drawing? cached)) return cached;

        var group = new DrawingGroup();
        using (DrawingContext dc = group.Open())
        {
            dc.DrawEllipse(Shadow, null, new Point(model.Back / 2, 0.5), model.Back * 0.6, 4.5);

            Brush lacquer = model.Cream ? Cream : Lacquer(paint);
            var edge = new Pen(new SolidColorBrush(Color.FromArgb(0xE0, 0x0A, 0x0C, 0x0E)), 1.8);
            dc.DrawGeometry(lacquer, edge, model.BodyShape);
            dc.DrawGeometry(Glass, new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0x10, 0x12, 0x14)), 0.8), model.GlassShape);

            // A light along the shoulder, the sill in shadow, and the dark wheel wells.
            dc.PushClip(model.BodyShape);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x70, 255, 255, 255)), 1.4),
                new Point(6, model.LightY - 5), new Point(model.Back - 4, model.LightY - 5));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0)), null, new Rect(0, -12, model.Back, 4));
            dc.Pop();
            model.Extra?.Invoke(dc, paint);
            if (model.Wells)
            {
                double r = model.WheelSize;
                dc.DrawEllipse(Well, null, new Point(model.FrontWheel, -r), r + 1.6, r + 1.6);
                dc.DrawEllipse(Well, null, new Point(model.RearWheel, -r), r + 1.6, r + 1.6);
            }

            // Head lamp at the nose, tail lamp at the back.
            dc.DrawEllipse(HeadLamp, null, new Point(2.6, model.LightY), 2.2, 2.6);
            dc.DrawRectangle(TailLamp, null, new Rect(model.Back - 3, model.LightY - 3, 3, 5.5));
        }
        group.Freeze();
        Bodies[(model, paint)] = group;
        return group;
    }

    /// <summary>A whole car: the body, its wheels turned by <paramref name="turn"/> radians, and lit brake lamps if it stands.</summary>
    public static void Draw(DrawingContext dc, CarModel model, Color paint, double turn, bool braking)
    {
        dc.DrawDrawing(Body(model, paint));
        double r = model.WheelSize;
        Wheel(dc, new Point(model.FrontWheel, -r), r, turn);
        Wheel(dc, new Point(model.RearWheel, -r), r, turn);
        if (braking)
        {
            dc.DrawEllipse(BrakeGlow, null, new Point(model.Back + 1, model.LightY), 9, 7);
            dc.DrawRectangle(BrakeLamp, null, new Rect(model.Back - 3, model.LightY - 3, 3, 5.5));
        }
    }

    private static void Wheel(DrawingContext dc, Point c, double r, double turn)
    {
        dc.DrawEllipse(Tyre, null, c, r, r);
        dc.DrawEllipse(Rim, null, c, r * 0.58, r * 0.58);
        for (int i = 0; i < 5; i++)
        {
            double a = turn + i * Math.PI * 2 / 5;
            dc.DrawLine(Spoke, new Point(c.X + Math.Cos(a) * 1.2, c.Y + Math.Sin(a) * 1.2),
                new Point(c.X + Math.Cos(a) * r * 0.52, c.Y + Math.Sin(a) * r * 0.52));
        }
    }

    public static Color Rgb(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    public static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
