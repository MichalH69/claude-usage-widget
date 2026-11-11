using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudeUsageWidget.Skins;

/// <summary>
/// Three roads, one for each limit. On each, cars queue up from a traffic light on the left, one car for every ten
/// percent used, under a sign that gives the time left until the reset. At the reset the light turns green and the
/// queue drives off. Far behind, traffic runs on a motorway.
/// </summary>
public partial class RoadSkin : UserControl, IWidgetSkin
{
    /// <summary>The small version is the large one seen from further away, as with the aquarium.</summary>
    private const double SmallScale = 0.76;

    private bool compact, hovered;

    public RoadSkin()
    {
        InitializeComponent();
        _ = new SkinClock(this, () => hovered, Tick);
    }

    private IEnumerable<TrafficLane> Roads => new[] { SessionLane, WeeklyLane, FableLane };

    private IEnumerable<RoadSign> Signs => new[] { SessionSign, WeeklySign, FableSign };

    private void Tick(double now, double step)
    {
        Traffic.Animate(now, step);
        foreach (TrafficLane lane in Roads) lane.Animate(now, step);
    }

    // On top of the hood's light, which the gear replaces while the pointer is over the widget.
    public Point GearCenter => compact ? new(15 * SmallScale, 13.5 * SmallScale) : new(15, 13.5);

    public double GearScale => compact ? SmallScale : 1;

    public bool Compact
    {
        get => compact;
        set
        {
            compact = value;
            LayoutTransform = value ? new ScaleTransform(SmallScale, SmallScale) : Transform.Identity;
        }
    }

    /// <summary>Under the pointer the signs give the time of day of each reset instead of the time left.</summary>
    public bool Hovered
    {
        get => hovered;
        set
        {
            hovered = value;
            foreach (RoadSign sign in Signs) sign.Hovered = value;
        }
    }
}
