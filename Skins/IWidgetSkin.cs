using System.Windows;

namespace ClaudeUsageWidget.Skins;

public interface IWidgetSkin
{
    /// <summary>Where the settings gear sits, in the widget's coordinates. Each skin picks a spot in its top left corner.</summary>
    Point GearCenter { get; }

    /// <summary>How large the gear is drawn there, 1 being its full size. A skin shrinks it when the spot has less room.</summary>
    double GearScale { get; }

    /// <summary>The small version of the skin rather than the large one. The skin changes its own size.</summary>
    bool Compact { get; set; }

    /// <summary>The pointer is over the widget.</summary>
    bool Hovered { get; set; }
}
