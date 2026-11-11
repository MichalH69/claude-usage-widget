using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClaudeUsageWidget.Skins;

public partial class AquariumSkin : UserControl, IWidgetSkin
{
    /// <summary>The small tank is the large one seen from further away.</summary>
    private const double SmallScale = 0.76;

    private bool compact, hovered;

    /// <summary>Each plant sways about the foot of its stem, and each to its own rhythm.</summary>
    private readonly List<(SkewTransform Sway, double Phase, double Reach)> plants = new();

    public AquariumSkin()
    {
        InitializeComponent();

        int i = 0;
        foreach (Canvas bed in new[] { BackPlants, FrontPlants })
        {
            foreach (Path leaf in bed.Children.OfType<Path>())
            {
                Rect bounds = leaf.Data.Bounds;
                var sway = new SkewTransform(0, 0, bounds.X + bounds.Width / 2, bounds.Bottom);
                leaf.RenderTransform = sway;
                // Tall leaves swing further than short ones; the far ones move a little less.
                plants.Add((sway, i * 1.37, (bed == BackPlants ? 2.6 : 3.4) * Math.Min(1.3, bounds.Height / 50)));
                i++;
            }
        }

        _ = new SkinClock(this, () => hovered, Tick);
    }

    private IEnumerable<ITankLife> Life => new ITankLife[] { Bubbles, School, Hideout };

    private IEnumerable<FishLane> Lanes => new[] { SessionFish, WeeklyFish, FableFish };

    private void Tick(double now, double step)
    {
        foreach (var (sway, phase, reach) in plants)
        {
            sway.AngleX = reach * Math.Sin(now * 0.85 + phase) + reach * 0.3 * Math.Sin(now * 2.05 + phase * 1.6);
        }
        foreach (ITankLife life in Life) life.Animate(now, step);
        foreach (FishLane lane in Lanes) lane.Animate(now, step);
    }

    // On top of the hood's indicator light, which the gear replaces while the pointer is over the tank.
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

    public static readonly DependencyProperty ShowClockProperty =
        DependencyProperty.Register(nameof(ShowClock), typeof(bool), typeof(AquariumSkin), new PropertyMetadata(false));

    /// <summary>The reset lines give the time of day of each reset instead of the time left.</summary>
    public bool ShowClock { get => (bool)GetValue(ShowClockProperty); set => SetValue(ShowClockProperty, value); }

    // Each fish watches the pointer by itself. When the window says the pointer has gone, the fish stop looking.
    // Under the pointer the reset lines turn to the time of day of each reset.
    public bool Hovered
    {
        get => hovered;
        set
        {
            hovered = value;
            ShowClock = value;
            if (!value) foreach (FishLane lane in Lanes) lane.PointerGone();
        }
    }
}
