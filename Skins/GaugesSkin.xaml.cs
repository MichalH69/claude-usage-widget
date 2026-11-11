using System.Windows;
using System.Windows.Controls;

namespace ClaudeUsageWidget.Skins;

public partial class GaugesSkin : UserControl, IWidgetSkin
{
    private bool compact, hovered;

    public GaugesSkin()
    {
        InitializeComponent();
        Compact = false;
    }

    // The empty corner beside the large dial.
    public Point GearCenter => compact ? new(11, 11) : new(13, 13);

    public double GearScale => 1;

    public bool Compact
    {
        get => compact;
        set
        {
            compact = value;

            // The small version is not the large one scaled down: the two side dials give up less of their
            // size than the main one, or nobody could read them.
            (Width, Height) = value ? (208, 174) : (306, 244);
            Place(SessionDial, value ? new Rect(2, 15, 142, 142) : new Rect(0, 10, 222, 222));
            Place(WeeklyDial, value ? new Rect(120, 2, 86, 86) : new Rect(184, 2, 122, 122));
            Place(FableDial, value ? new Rect(120, 84, 86, 86) : new Rect(184, 118, 122, 122));
            foreach (GaugeDial dial in Dials) dial.Compact = value;
        }
    }

    public bool Hovered
    {
        get => hovered;
        set
        {
            hovered = value;
            foreach (GaugeDial dial in Dials) dial.Hovered = value;
        }
    }

    private GaugeDial[] Dials => new[] { SessionDial, WeeklyDial, FableDial };

    private static void Place(GaugeDial dial, Rect at)
    {
        Canvas.SetLeft(dial, at.X);
        Canvas.SetTop(dial, at.Y);
        dial.Width = at.Width;
        dial.Height = at.Height;
    }
}
