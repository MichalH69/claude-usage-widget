using System.Windows;
using System.Windows.Media;

namespace ClaudeUsageWidget.Skins;

public enum FishKind { BlueTang, Clownfish, PurpleTang }

/// <summary>
/// Reef fish as flat illustrations with their real markings.
/// Each is laid out in a box 100 units long: tail tip at x = 0, nose at x = 100, midline at y = 0.
/// </summary>
/// <summary>A fish in pieces, with the points the tail and the pectoral fin swing about.</summary>
internal sealed record FishParts(Drawing Tail, Drawing Fins, Drawing Body, Drawing Pectoral, Point TailRoot, Point PectoralRoot);

internal static class FishArt
{
    private static readonly Dictionary<FishKind, FishParts> Cache = new();

    /// <summary>The fish in pieces that move on their own: the tail, the long fins, the body with the eye white, the pectoral fin.</summary>
    public static FishParts Get(FishKind kind)
    {
        if (Cache.TryGetValue(kind, out FishParts? cached)) return cached;

        var pieces = new DrawingGroup[4];
        var contexts = new DrawingContext[4];
        for (int i = 0; i < 4; i++)
        {
            pieces[i] = new DrawingGroup();
            contexts[i] = pieces[i].Open();
        }
        var parts = new Painter(contexts);
        switch (kind)
        {
            case FishKind.Clownfish: Clownfish(parts); break;
            case FishKind.PurpleTang: PurpleTang(parts); break;
            default: BlueTang(parts); break;
        }
        foreach (DrawingContext dc in contexts) dc.Close();
        foreach (DrawingGroup piece in pieces) piece.Freeze();

        var (tailRoot, pectoralRoot) = kind switch
        {
            FishKind.Clownfish => (new Point(28, 0), new Point(77, 3)),
            FishKind.PurpleTang => (new Point(28, 0), new Point(76, 6)),
            _ => (new Point(24, 0), new Point(78, 5)),
        };
        var result = new FishParts(pieces[0], pieces[1], pieces[2], pieces[3], tailRoot, pectoralRoot);
        Cache[kind] = result;
        return result;
    }

    /// <summary>Where each stroke of a fish goes. The fish are drawn in the same order as before, piece by piece.</summary>
    private sealed record Painter(DrawingContext[] Contexts)
    {
        public DrawingContext Tail => Contexts[0];
        public DrawingContext Fins => Contexts[1];
        public DrawingContext Body => Contexts[2];
        public DrawingContext Pectoral => Contexts[3];
    }

    /// <summary>Paracanthurus hepatus: royal blue, a black palette on the flank, yellow tail.</summary>
    private static void BlueTang(Painter p)
    {
        DrawingContext dc = p.Body;
        Brush ink = Solid("#0E1430");
        Pen finEdge = Stroke("#0E1430", 1.6);
        Brush fin = Shade("#3B78F2", "#2456D6");
        Geometry body = Shape("M 100,3 C 96,-10 84,-22 62,-24 C 44,-25 30,-14 21,-5 L 21,5 C 30,14 44,25 62,24 C 84,22 96,13 100,3 Z");

        p.Tail.DrawGeometry(ink, null, Shape("M 24,-6 L 1,-18 C 6,-7 6,7 1,18 L 24,6 Z"));
        p.Tail.DrawGeometry(Shade("#FFE34D", "#FFC414"), null, Shape("M 27,0 L 5,-11 C 7.5,-4 7.5,4 5,11 Z"));
        p.Fins.DrawGeometry(fin, finEdge, Shape("M 84,-17 C 74,-30 50,-35 34,-24 C 28,-20 25,-14 23,-7 L 40,-10 L 70,-14 Z"));
        p.Fins.DrawGeometry(fin, finEdge, Shape("M 62,22 C 54,30 42,31 34,24 C 28,20 25,14 23,7 L 40,10 Z"));

        dc.DrawGeometry(Shade("#1C40C2", "#2A63EA", "#69A4FF"), Stroke("#C00E1430", 1.2), body);
        dc.PushClip(body);
        dc.DrawGeometry(ink, null, Shape("M 86,-9 C 76,-21 48,-26 20,-7 L 20,3 C 34,-1 46,3 58,1 C 68,-1 76,-6 86,-9 Z"));
        dc.DrawGeometry(Solid("#2C62E6"), null, Shape("M 35,-8 C 40,-15 60,-17 67,-11 C 62,-5 42,-3 35,-8 Z"));
        Sheen(dc, 66, -17, 22, 5);
        dc.Pop();

        var pectoral = new LinearGradientBrush { StartPoint = new Point(1, 0), EndPoint = new Point(0, 0.8) };
        pectoral.GradientStops.Add(new GradientStop(Rgb("#0E1430"), 0.1));
        pectoral.GradientStops.Add(new GradientStop(Rgb("#FFD21F"), 0.5));
        p.Pectoral.DrawGeometry(pectoral, Stroke("#0E1430", 1), Shape("M 78,5 C 71,3 64,8 62,15 C 69,16 76,12 78,5 Z"));

        EyeWhite(dc, FishKind.BlueTang);
        dc.DrawGeometry(null, Stroke("#0E1430", 1.1), Shape("M 98.5,6 C 96.5,7.6 94.5,7.6 92.8,6.6"));
    }

    /// <summary>Amphiprion ocellaris: orange, three white bands edged in black, rounded black-rimmed fins.</summary>
    private static void Clownfish(Painter p)
    {
        DrawingContext dc = p.Body;
        Pen rim = Stroke("#17110D", 1.9);
        Brush fin = Shade("#FF8A22", "#F26410");
        Brush white = Shade("#FFFFFF", "#E9EEF2");
        Geometry body = Shape("M 100,1 C 99,-10 90,-19 72,-20 C 54,-21 36,-15 25,-6 L 25,6 C 36,15 54,21 72,20 C 90,19 99,11 100,1 Z");

        p.Tail.DrawGeometry(fin, rim, Shape("M 28,-7 C 20,-16 5,-17 1,-7 C 0,-2 0,2 1,7 C 5,17 20,16 28,7 Z"));
        p.Fins.DrawGeometry(fin, rim, Shape("M 82,-17 C 78,-31 62,-31 58,-18 Z"));
        p.Fins.DrawGeometry(fin, rim, Shape("M 56,-19 C 52,-32 38,-30 33,-12 Z"));
        p.Fins.DrawGeometry(fin, rim, Shape("M 52,18 C 50,31 38,30 33,12 Z"));
        p.Fins.DrawGeometry(fin, rim, Shape("M 76,18 C 78,30 66,33 62,20 Z"));

        dc.DrawGeometry(Shade("#F2590C", "#FF7E1C", "#FFAA42"), Stroke("#C06A2400", 1.2), body);
        dc.PushClip(body);
        Pen bandEdge = Stroke("#17110D", 1.5);
        dc.DrawGeometry(white, bandEdge, Shape("M 81,-24 C 86,-10 86,10 80,24 L 72,24 C 78,10 78,-10 73,-24 Z"));
        dc.DrawGeometry(white, bandEdge, Shape("M 58,-26 C 65,-9 65,9 57,26 L 45,26 C 50,10 50,-9 46,-26 Z"));
        dc.DrawGeometry(white, bandEdge, Shape("M 38,-18 C 41,-6 41,6 38,18 L 28,18 C 31,6 31,-6 28,-18 Z"));
        Sheen(dc, 70, -14, 24, 4.5);
        dc.Pop();

        p.Pectoral.DrawGeometry(Shade("#FFA23A", "#FF7A18"), Stroke("#17110D", 1.6), Shape("M 77,3 C 70,0 64,5 63,12 C 69,15 76,11 77,3 Z"));
        EyeWhite(dc, FishKind.Clownfish);
        dc.DrawGeometry(null, Stroke("#7A2A00", 1.1), Shape("M 99,5 C 97,7 95,7 93.5,6"));
    }

    /// <summary>Zebrasoma xanthurum: a tall violet disc with sail fins, pointed snout, yellow tail.</summary>
    private static void PurpleTang(Painter p)
    {
        DrawingContext dc = p.Body;
        Pen finEdge = Stroke("#33146A", 1.4);
        Brush sail = Shade("#6B36B8", "#4E2296");
        Pen ray = Stroke("#70C9A8F5", 0.9);
        Geometry body = Shape("M 100,5 C 95,2 91,-7 84,-14 C 76,-22 62,-27 48,-23 C 38,-20 31,-11 27,-5 L 27,5 C 31,12 38,21 48,24 C 62,28 78,25 86,16 C 90,12 96,10 100,5 Z");

        p.Tail.DrawGeometry(Shade("#FFE34D", "#FFC414"), Stroke("#C48600", 1.2), Shape("M 28,-5 L 2,-16 C 7,-6 7,6 2,16 L 28,5 Z"));
        p.Tail.DrawGeometry(null, Stroke("#90C48600", 0.8), Shape("M 24,-2 L 6,-8 M 24,0 L 6,0 M 24,2 L 6,8"));

        p.Fins.DrawGeometry(sail, finEdge, Shape("M 84,-14 C 78,-28 62,-35 46,-33 C 36,-32 30,-21 27,-5 L 46,-12 Z"));
        p.Fins.DrawGeometry(sail, finEdge, Shape("M 82,18 C 76,29 62,35 46,33 C 36,32 30,21 27,5 L 46,12 Z"));
        p.Fins.DrawGeometry(null, ray, Shape("M 74,-20 L 71,-28 M 64,-24 L 61,-32 M 54,-24 L 51,-33 M 44,-21 L 41,-31 M 36,-15 L 33,-25"));
        p.Fins.DrawGeometry(null, ray, Shape("M 73,23 L 70,30 M 63,25 L 60,32 M 54,24 L 51,33 M 44,21 L 41,31 M 36,15 L 33,25"));

        dc.DrawGeometry(Shade("#5B2AA6", "#7C42C9", "#A06EE6"), Stroke("#C033146A", 1.2), body);
        dc.PushClip(body);
        dc.DrawGeometry(null, Stroke("#A0461C88", 1), Shape("M 30,-3 C 42,-7 54,-7 64,-4 M 30,2 C 44,3 56,3 66,1 M 34,9 C 44,11 54,11 62,9"));
        Brush spot = Solid("#3F1A7A");
        foreach (var (x, y) in new[] { (78.0, -11.0), (74, -4), (80, 3), (76, 9), (71, -15), (70, 2), (84, 9), (68, -8) })
        {
            dc.DrawEllipse(spot, null, new Point(x, y), 1.1, 1.1);
        }
        Sheen(dc, 62, -18, 20, 4.5);
        dc.Pop();

        var pectoral = new LinearGradientBrush { StartPoint = new Point(1, 0), EndPoint = new Point(0, 0.8) };
        pectoral.GradientStops.Add(new GradientStop(Rgb("#5B2AA6"), 0.1));
        pectoral.GradientStops.Add(new GradientStop(Rgb("#FFD21F"), 0.55));
        p.Pectoral.DrawGeometry(pectoral, Stroke("#33146A", 1), Shape("M 76,6 C 69,3 61,8 58,16 C 66,18 74,14 76,6 Z"));

        EyeWhite(dc, FishKind.PurpleTang);
        dc.DrawGeometry(null, Stroke("#33146A", 1.1), Shape("M 99,6 C 97.5,7.5 96,7.8 94.5,7.4"));
    }

    /// <summary>Centre and radius of the eye, in the fish's own 100-unit box.</summary>
    public static (Point Center, double Radius) EyeOf(FishKind kind) => kind switch
    {
        FishKind.Clownfish => (new Point(91, -4.5), 4.6),
        FishKind.PurpleTang => (new Point(84, -5), 4.4),
        _ => (new Point(88, -5), 4.8),
    };

    /// <summary>
    /// The pupil is left out of the cached drawing so it can move. <paramref name="gaze"/> points where
    /// the fish looks; at length 1 the pupil touches the rim of the eye.
    /// </summary>
    public static void DrawPupil(DrawingContext dc, FishKind kind, Vector gaze)
    {
        var (eye, r) = EyeOf(kind);
        if (gaze.Length > 1) gaze.Normalize();

        double pupil = r * 0.56;
        Point at = eye + gaze * (r - pupil - r * 0.06);
        dc.DrawEllipse(PupilInk, null, at, pupil, pupil);
        dc.DrawEllipse(Brushes.White, null, new Point(at.X - pupil * 0.32, at.Y - pupil * 0.38), pupil * 0.3, pupil * 0.3);
    }

    private static readonly Brush PupilInk = Solid("#0C1018");

    private static void EyeWhite(DrawingContext dc, FishKind kind)
    {
        var (eye, r) = EyeOf(kind);
        dc.DrawEllipse(Brushes.White, Stroke("#B0101622", 0.7), eye, r, r);
    }

    /// <summary>A soft light along the back, the one concession to gloss.</summary>
    private static void Sheen(DrawingContext dc, double x, double y, double rx, double ry)
    {
        var light = new LinearGradientBrush(Color.FromArgb(105, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90);
        dc.DrawEllipse(light, null, new Point(x, y), rx, ry);
    }

    private static Geometry Shape(string data)
    {
        Geometry geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }

    private static Color Rgb(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Brush Solid(string hex) => new SolidColorBrush(Rgb(hex));

    private static Pen Stroke(string hex, double thickness) => new(Solid(hex), thickness)
    {
        LineJoin = PenLineJoin.Round,
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
    };

    /// <summary>Top-to-bottom gradient through evenly spaced colours.</summary>
    private static Brush Shade(params string[] hex)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        for (int i = 0; i < hex.Length; i++)
        {
            brush.GradientStops.Add(new GradientStop(Rgb(hex[i]), hex.Length == 1 ? 0 : (double)i / (hex.Length - 1)));
        }
        return brush;
    }
}
