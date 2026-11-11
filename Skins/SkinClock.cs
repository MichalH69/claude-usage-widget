using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClaudeUsageWidget.Skins;

/// <summary>
/// The clock of a moving skin. It ticks thirty times a second while the skin can be seen and once a second while
/// other windows cover it, and then only to look whether it has come back into view. Nothing jumps after a pause.
/// </summary>
internal sealed class SkinClock
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(33);

    /// <summary>How often a covered skin looks whether it has come back into view.</summary>
    private static readonly TimeSpan CoveredCheck = TimeSpan.FromSeconds(1);

    private readonly FrameworkElement owner;
    private readonly Func<bool> hovered;
    private readonly Action<double, double> tick;
    private readonly DispatcherTimer ticker = new(DispatcherPriority.Render) { Interval = Frame };
    private readonly Stopwatch clock = new();
    private double lastTick;
    private long lastLook;

    /// <param name="tick">Called with the time in seconds since the skin started and the time since the last tick.</param>
    public SkinClock(FrameworkElement owner, Func<bool> hovered, Action<double, double> tick)
    {
        this.owner = owner;
        this.hovered = hovered;
        this.tick = tick;

        ticker.Tick += (_, _) => Tick();
        owner.Loaded += (_, _) =>
        {
            clock.Start();
            ticker.Start();
        };
        owner.Unloaded += (_, _) =>
        {
            ticker.Stop();
            clock.Stop();
        };
    }

    private void Tick()
    {
        // A skin nobody can see does not need to move. Whether it can be seen is asked about once a second.
        if (Environment.TickCount64 - lastLook >= CoveredCheck.TotalMilliseconds)
        {
            lastLook = Environment.TickCount64;
            bool covered = !hovered() && IsCovered();
            ticker.Interval = covered ? CoveredCheck : Frame;
            if (covered) return;
        }
        else if (ticker.Interval != Frame)
        {
            return;
        }

        double now = clock.Elapsed.TotalSeconds;
        // After a pause, such as the computer waking from sleep or the skin coming back into view, nothing jumps.
        double step = Math.Clamp(now - lastTick, 0, 0.1);
        lastTick = now;
        tick(now, step);
    }

    /// <summary>
    /// True when the skin is hidden: its window is minimized or put away, or other windows cover it. A few spots
    /// across the skin are tried: if Windows says none of them belongs to the widget's window, it is out of sight.
    /// </summary>
    private bool IsCovered()
    {
        if (!owner.IsVisible) return true;
        if (PresentationSource.FromVisual(owner) is not HwndSource source) return false;
        if (IsIconic(source.Handle)) return true;

        foreach (var (x, y) in new[] { (0.5, 0.5), (0.15, 0.3), (0.85, 0.3), (0.15, 0.8), (0.85, 0.8), (0.5, 0.15) })
        {
            Point spot = owner.PointToScreen(new Point(owner.ActualWidth * x, owner.ActualHeight * y));
            nint hit = WindowFromPoint(new NativePoint { X = (int)spot.X, Y = (int)spot.Y });
            if (hit != 0 && GetAncestor(hit, RootWindow) == source.Handle) return false;
        }
        return true;
    }

    private const uint RootWindow = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint at);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);
}
