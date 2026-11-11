using System.Windows;
using System.Windows.Media;

namespace ClaudeUsageWidget;

/// <summary>The signs on the small round buttons: the cog of the settings button and the minimize bar, drawn for an 18 × 18 box.</summary>
public static class GearIcon
{
    public static Geometry Geometry { get; } = Build(new Point(9, 9), outer: 5.7, root: 4.2, hole: 1.8, teeth: 8);

    /// <summary>The sign on the button beside the cog: the bar of a window's minimize button, low in the circle.</summary>
    public static Geometry Minimize { get; } = Frozen(new RectangleGeometry(new Rect(5, 10.2, 8, 2.2), 1.1, 1.1));

    private static Geometry Frozen(Geometry geometry)
    {
        geometry.Freeze();
        return geometry;
    }

    private static Geometry Build(Point c, double outer, double root, double hole, int teeth)
    {
        var rim = new StreamGeometry();
        using (StreamGeometryContext g = rim.Open())
        {
            double step = 360.0 / teeth;
            for (int i = 0; i < teeth; i++)
            {
                double mid = i * step;
                // A tooth is a trapezoid: wide where it meets the wheel, narrower at the tip.
                Point[] tooth = { At(c, root, mid - 12.5), At(c, outer, mid - 7.5), At(c, outer, mid + 7.5), At(c, root, mid + 12.5) };
                if (i == 0) g.BeginFigure(tooth[0], isFilled: true, isClosed: true);
                else g.ArcTo(tooth[0], new Size(root, root), 0, false, SweepDirection.Clockwise, true, true);
                g.LineTo(tooth[1], true, true);
                g.LineTo(tooth[2], true, true);
                g.LineTo(tooth[3], true, true);
            }
            g.ArcTo(At(c, root, -12.5), new Size(root, root), 0, false, SweepDirection.Clockwise, true, true);
        }

        var gear = new GeometryGroup { FillRule = FillRule.EvenOdd };
        gear.Children.Add(rim);
        gear.Children.Add(new EllipseGeometry(c, hole, hole));
        gear.Freeze();
        return gear;
    }

    private static Point At(Point c, double radius, double degrees)
    {
        double a = degrees * Math.PI / 180;
        return new Point(c.X + radius * Math.Sin(a), c.Y - radius * Math.Cos(a));
    }
}
