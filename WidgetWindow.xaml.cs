using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClaudeUsageWidget.Data;
using ClaudeUsageWidget.Skins;
using ClaudeUsageWidget.ViewModels;

namespace ClaudeUsageWidget;

/// <summary>
/// The widget itself: a borderless, see-through window that holds one skin. A cog shows in its corner
/// while the pointer is over it and opens the row that switches skins. A click on the widget switches
/// between its small size and its large one.
/// <para>
/// The window is a frame the size of the large version. The small version sits in the corner of the frame
/// that is nearest a corner of the screen, so the widget grows out of that corner and shrinks back into it
/// while the window stays put. A window that moves and redraws shows the old picture in the new place for a moment.
/// </para>
/// </summary>
public partial class WidgetWindow : Window
{
    private const double ScreenMargin = 24;

    /// <summary>How visible the cog is while the pointer is over the widget but not over the cog.</summary>
    private const double GearAtRest = 0.55;

    /// <summary>How much larger the widget grows under the pointer, so it shows that it is pointed at.</summary>
    private const double HoverGrowth = 1.04;

    /// <summary>The room the frame keeps round the widget for that growth.</summary>
    public const double HoverPad = 8;
    public static readonly Thickness HoverRoom = new(HoverPad);

    /// <summary>The widget never turns quite invisible: a window that clear lets the pointer through and could not be found again.</summary>
    public const double MostSeeThrough = 0.8;

    private readonly Settings settings;
    private readonly WidgetViewModel viewModel;
    private readonly UsagePoller? poller;
    private readonly DispatcherTimer hideTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly TrayIcon? tray;
    private long pickerClosedAt = long.MinValue / 2;
    private bool skipGearClick;

    /// <summary>The corner of the frame the widget sits in.</summary>
    private bool anchorRight, anchorBottom;

    internal WidgetWindow(Settings settings, WidgetViewModel viewModel, UsagePoller? poller)
    {
        this.settings = settings;
        this.viewModel = viewModel;
        this.poller = poller;

        InitializeComponent();
        DataContext = viewModel;
        Topmost = settings.Topmost;
        settings.SeeThrough = Math.Clamp(settings.SeeThrough, 0, MostSeeThrough);
        SeeThrough.Value = settings.SeeThrough;
        ApplySeeThrough();
        ShowSkin(settings.Skin);
        PlaceOnScreen();

        MouseEnter += (_, _) => ShowChrome();
        MouseLeave += (_, _) => HideChromeSoon();
        hideTimer.Tick += (_, _) =>
        {
            if (SkinPicker.IsOpen)
            {
                hideTimer.Stop();
                return;
            }

            // Windows knows where the pointer is. WPF can be wrong about a pointer at rest when the widget changes
            // under it, as it does on a click: gone when the widget has grown under it, still there when the widget
            // has shrunk away from it.
            if (!PointerOnWindow())
            {
                hideTimer.Stop();
                HideChrome();
            }
            else if (IsMouseOver)
            {
                // WPF sees the pointer too and will say when it leaves.
                hideTimer.Stop();
            }
            // Otherwise the pointer is there and WPF has lost it, so the question is asked again in a moment.
        };
        // WPF does not announce a pointer that it believes never left.
        MouseMove += (_, _) =>
        {
            if (HoverSurface.Visibility != Visibility.Visible && PointerOnWindow()) ShowChrome();
        };
        Gear.MouseEnter += (_, _) => FadeGear(1);
        Gear.MouseLeave += (_, _) => FadeGear(SkinPicker.IsOpen ? 1 : GearAtRest);
        TrayButton.MouseEnter += (_, _) => FadeGear(1);
        TrayButton.MouseLeave += (_, _) => FadeGear(SkinPicker.IsOpen ? 1 : GearAtRest);
        Gear.PreviewMouseLeftButtonDown += (_, _) =>
        {
            // A press on the cog while the row is open closes the row first and then lands here. It must not reopen it.
            skipGearClick = Environment.TickCount64 - pickerClosedAt < 150;
        };
        Root.MouseLeftButtonDown += OnDragStart;

        // Test renders must not leave icons behind in the notification area.
        if (!settings.Transient)
        {
            tray = new TrayIcon(Root.ContextMenu, ToggleTray);
            UpdateTrayText();
            viewModel.PropertyChanged += (_, _) => UpdateTrayText();
            Closed += (_, _) => tray.Dispose();
        }
    }

    // ---- the notification area ----

    /// <summary>The widget leaves the desktop. Its icon in the notification area brings it back.</summary>
    private void ToTray()
    {
        SkinPicker.IsOpen = false;
        HideChrome();
        Hide();
        settings.InTray = true;
        settings.Save();
    }

    private void FromTray()
    {
        settings.InTray = false;
        settings.Save();
        Show();
    }

    private void ToggleTray()
    {
        if (IsVisible) ToTray();
        else FromTray();
    }

    private void OnTrayButtonClick(object sender, RoutedEventArgs e) => ToTray();

    private void OnMenuTrayClick(object sender, RoutedEventArgs e) => ToggleTray();

    /// <summary>Pointing at the icon gives the readings without bringing the widget back.</summary>
    private void UpdateTrayText()
    {
        if (tray is null) return;
        tray.Text = $"Využití Claude\nSession {viewModel.Session.PercentText} · Týden {viewModel.Weekly.PercentText} · Fable {viewModel.Fable.PercentText}";
    }

    // ---- skins ----

    private IWidgetSkin Skin => (IWidgetSkin)SkinHost.Content;

    private void ShowSkin(string name)
    {
        settings.Skin = name is Settings.Gauges or Settings.Road ? name : Settings.Aquarium;

        IWidgetSkin skin = settings.Skin switch
        {
            Settings.Gauges => new GaugesSkin(),
            Settings.Road => new RoadSkin(),
            _ => new AquariumSkin(),
        };
        skin.Compact = false;
        Size frame = RoomFor(skin);
        (Width, Height) = (frame.Width + 2 * HoverPad, frame.Height + 2 * HoverPad);
        skin.Compact = settings.Compact;
        skin.Hovered = HoverSurface.Visibility == Visibility.Visible;
        SkinHost.Content = skin;
        FitStage();

        PickGauges.IsChecked = settings.Skin == Settings.Gauges;
        PickAquarium.IsChecked = settings.Skin == Settings.Aquarium;
        PickRoad.IsChecked = settings.Skin == Settings.Road;
    }

    /// <summary>The stage takes the size of the skin as it is now, and the cog takes its place on the skin.</summary>
    private void FitStage()
    {
        Size skin = RoomFor(Skin);
        (Stage.Width, Stage.Height) = (skin.Width, skin.Height);

        Point center = Skin.GearCenter;
        double scale = Skin.GearScale;
        Gear.LayoutTransform = new ScaleTransform(scale, scale);
        Canvas.SetLeft(Gear, center.X - Gear.Width * scale / 2);
        Canvas.SetTop(Gear, center.Y - Gear.Height * scale / 2);

        // The minimize button sits just right of the cog.
        TrayButton.LayoutTransform = new ScaleTransform(scale, scale);
        Canvas.SetLeft(TrayButton, center.X + Gear.Width * scale / 2 + 3 * scale);
        Canvas.SetTop(TrayButton, center.Y - TrayButton.Height * scale / 2);
    }

    /// <summary>
    /// The room a skin takes up right now, in whole units, so that the widget and the frame around it differ
    /// by whole units too. The skin's own Width and Height do not tell: a skin may scale itself.
    /// </summary>
    private static Size RoomFor(IWidgetSkin skin)
    {
        var element = (FrameworkElement)skin;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(Math.Ceiling(element.DesiredSize.Width), Math.Ceiling(element.DesiredSize.Height));
    }

    private void Choose(string skin)
    {
        SkinPicker.IsOpen = false;
        if (settings.Skin == skin) return;
        Rect before = WidgetRect();
        ShowSkin(skin);
        KeepCorner(before);
        settings.Save();
    }

    private void OnPickGauges(object sender, RoutedEventArgs e) => Choose(Settings.Gauges);
    private void OnPickAquarium(object sender, RoutedEventArgs e) => Choose(Settings.Aquarium);
    private void OnMenuGaugesClick(object sender, RoutedEventArgs e) => Choose(Settings.Gauges);
    private void OnMenuAquariumClick(object sender, RoutedEventArgs e) => Choose(Settings.Aquarium);
    private void OnPickRoad(object sender, RoutedEventArgs e) => Choose(Settings.Road);
    private void OnMenuRoadClick(object sender, RoutedEventArgs e) => Choose(Settings.Road);

    // ---- small and large ----

    private void SetCompact(bool compact)
    {
        if (settings.Compact == compact) return;
        Rect before = WidgetRect();
        settings.Compact = compact;
        Skin.Compact = compact;
        FitStage();
        KeepCorner(before);
        settings.Save();

        // The widget may have shrunk away from under the pointer.
        if (HoverSurface.Visibility == Visibility.Visible) HideChromeSoon();
    }

    private void OnLargeClick(object sender, RoutedEventArgs e) => SetCompact(!MenuLarge.IsChecked);

    // ---- the cog and its row ----

    private void ShowChrome()
    {
        hideTimer.Stop();
        HoverSurface.Visibility = Visibility.Visible;
        Gear.IsHitTestVisible = true;
        TrayButton.IsHitTestVisible = true;
        FadeGear(Gear.IsMouseOver || SkinPicker.IsOpen ? 1 : GearAtRest);
        Skin.Hovered = true;
        Zoom(HoverGrowth);
    }

    private void Zoom(double to)
    {
        var grow = new DoubleAnimation(to, TimeSpan.FromMilliseconds(170)) { EasingFunction = new QuadraticEase() };
        HoverZoom.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        HoverZoom.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    private void HideChrome()
    {
        HoverSurface.Visibility = Visibility.Collapsed;
        Gear.IsHitTestVisible = false;
        TrayButton.IsHitTestVisible = false;
        FadeGear(0);
        Skin.Hovered = false;
        Zoom(1);
    }

    // The pointer often crosses the edge for a moment on its way to the cog in the corner.
    private void HideChromeSoon()
    {
        hideTimer.Stop();
        hideTimer.Start();
    }

    private bool PointerOnWindow()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        return handle != 0 && GetCursorPos(out NativePoint at) && WindowFromPoint(at) == handle;
    }

    /// <summary>The cog and the minimize button beside it come and go together.</summary>
    private void FadeGear(double opacity)
    {
        var fade = new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(160));
        Gear.BeginAnimation(OpacityProperty, fade);
        TrayButton.BeginAnimation(OpacityProperty, fade);
    }

    private void OnGearClick(object sender, RoutedEventArgs e)
    {
        if (skipGearClick)
        {
            skipGearClick = false;
            return;
        }
        SkinPicker.IsOpen = true;
        FadeGear(1);
    }

    // ---- see-through ----

    private void OnSeeThroughChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        settings.SeeThrough = Math.Round(e.NewValue, 2);
        ApplySeeThrough();
    }

    /// <summary>Only the skin fades. The sheet that catches the pointer and the cog stay as they are, so the widget can still be found.</summary>
    private void ApplySeeThrough()
    {
        SkinHost.Opacity = 1 - settings.SeeThrough;
        SeeThroughText.Text = $"{Math.Round(settings.SeeThrough * 100)} %";
    }

    private void OnPickerClosed(object? sender, EventArgs e)
    {
        pickerClosedAt = Environment.TickCount64;
        settings.Save();
        // The widget may have changed its shape under the pointer, so whether the pointer is still on it is asked afresh.
        FadeGear(Gear.IsMouseOver ? 1 : GearAtRest);
        HideChromeSoon();
    }

    // The row belongs to the widget's element tree, so without this a press on its rim would start dragging the widget.
    private void OnPickerMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    /// <summary>For test renders: the widget as it looks under the pointer, with or without the skin row.</summary>
    internal void ShowChromeForRender(bool openPicker)
    {
        HoverSurface.Visibility = Visibility.Visible;
        Gear.BeginAnimation(OpacityProperty, null);
        Gear.Opacity = openPicker ? 1 : GearAtRest;
        TrayButton.BeginAnimation(OpacityProperty, null);
        TrayButton.Opacity = Gear.Opacity;
        Skin.Hovered = true;
        SkinPicker.StaysOpen = true;
        SkinPicker.IsOpen = openPicker;
    }

    internal FrameworkElement RenderRoot => Stage;
    internal FrameworkElement? OpenPicker => SkinPicker.IsOpen ? PickerPill : null;

    // ---- place on screen ----

    private void PlaceOnScreen()
    {
        var size = new Size(Stage.Width, Stage.Height);
        Rect work = SystemParameters.WorkArea;
        var at = new Point(work.Right - size.Width - ScreenMargin, work.Top + ScreenMargin);

        if (settings.Left is { } x && settings.Top is { } y && double.IsFinite(x) && double.IsFinite(y))
        {
            // The saved spot counts only while enough of the widget would show to grab it,
            // which it would not after a second monitor is unplugged.
            var visible = new Rect(x, y, size.Width, size.Height);
            visible.Intersect(new Rect(
                SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight));
            if (!visible.IsEmpty && visible.Width >= 60 && visible.Height >= 60) at = new Point(x, y);
        }

        KeepCorner(new Rect(at, size));
    }

    /// <summary>Where the widget shows on the screen: the part of the frame it fills.</summary>
    private Rect WidgetRect() => new(
        Left + HoverPad + (anchorRight ? Width - 2 * HoverPad - Stage.Width : 0),
        Top + HoverPad + (anchorBottom ? Height - 2 * HoverPad - Stage.Height : 0),
        Stage.Width, Stage.Height);

    /// <summary>
    /// Puts the widget, at the size it has now, where it was at the size it had before: the corner of it nearest
    /// a corner of the screen stays, so a widget parked by an edge stays by that edge and stays whole. The frame
    /// reaches from that corner towards the middle of the screen. Unless the widget has been dragged to another
    /// part of the screen since the last time, that is where the frame already is.
    /// </summary>
    private void KeepCorner(Rect before)
    {
        Rect work = WorkAreaAt(before);
        anchorRight = before.Left + before.Width / 2 > work.Left + work.Width / 2;
        anchorBottom = before.Top + before.Height / 2 > work.Top + work.Height / 2;
        Stage.HorizontalAlignment = anchorRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Stage.VerticalAlignment = anchorBottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;

        Left = anchorRight ? before.Right + HoverPad - Width : before.Left - HoverPad;
        Top = anchorBottom ? before.Bottom + HoverPad - Height : before.Top - HoverPad;
        RememberPlace();
    }

    /// <summary>The saved place is that of the widget, not of the frame, so it holds whatever size the widget starts at.</summary>
    private void RememberPlace()
    {
        Rect widget = WidgetRect();
        settings.Left = widget.Left;
        settings.Top = widget.Top;
    }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        (double left, double top) = (Left, Top);
        DragMove(); // Returns once the button is released.

        if (Math.Abs(Left - left) < ClickSlack && Math.Abs(Top - top) < ClickSlack)
        {
            // The widget went nowhere, so this was a click. A click switches between the small widget and the large one.
            (Left, Top) = (left, top);
            SetCompact(!settings.Compact);
            return;
        }
        RememberPlace();
        settings.Save();
    }

    /// <summary>How far the widget may slip under a pressed button and still count as clicked, not moved.</summary>
    private const double ClickSlack = 3;

    /// <summary>The usable part of the monitor that shows most of a rectangle, in the units of Left and Top.</summary>
    private Rect WorkAreaAt(Rect widget)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        var pixels = new NativeRect
        {
            Left = (int)Math.Round(widget.Left * dpi.DpiScaleX),
            Top = (int)Math.Round(widget.Top * dpi.DpiScaleY),
            Right = (int)Math.Round(widget.Right * dpi.DpiScaleX),
            Bottom = (int)Math.Round(widget.Bottom * dpi.DpiScaleY),
        };
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromRect(in pixels, NearestMonitor), ref info)) return SystemParameters.WorkArea;

        return new Rect(
            info.Work.Left / dpi.DpiScaleX, info.Work.Top / dpi.DpiScaleY,
            (info.Work.Right - info.Work.Left) / dpi.DpiScaleX, (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // A see-through window drawn by the graphics card has every frame copied back to the processor, which costs
        // far more than drawing this small a window on the processor in the first place. Measured with the tank moving.
        if (PresentationSource.FromVisual(this) is HwndSource source) source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;

        // A tool window stays out of Alt+Tab.
        nint handle = new WindowInteropHelper(this).Handle;
        SetWindowLongPtr(handle, ExtendedStyle, GetWindowLongPtr(handle, ExtendedStyle) | ToolWindow);
    }

    private const int ExtendedStyle = -20;
    private const nint ToolWindow = 0x80;
    private const uint NearestMonitor = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint at);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint at);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(in NativeRect rect, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    // ---- right-click menu ----

    private void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        MenuStatus.Header = viewModel.StatusLine;
        MenuVersion.Header = $"Verze {Updater.Format(Updater.Current)}";
        MenuTray.Header = IsVisible ? "Minimalizovat" : "Maximalizovat";
        MenuRefresh.IsEnabled = poller?.CanRefresh == true;
        MenuGauges.IsChecked = settings.Skin == Settings.Gauges;
        MenuAquarium.IsChecked = settings.Skin == Settings.Aquarium;
        MenuRoad.IsChecked = settings.Skin == Settings.Road;
        MenuLarge.IsChecked = !settings.Compact;
        MenuTopmost.IsChecked = Topmost;
        MenuAutostart.IsChecked = Autostart.IsOn;
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => poller?.RefreshNow();

    // ---- updates ----

    private UpdateOffer? offer;

    /// <summary>A newer version is out: the menu offers it, and the notification area says so once.</summary>
    internal void OfferUpdate(UpdateOffer found)
    {
        bool fresh = offer?.Version != found.Version;
        offer = found;
        MenuUpdate.Header = $"Aktualizovat na verzi {Updater.Format(found.Version)}";
        MenuUpdate.IsEnabled = true;
        MenuUpdate.Visibility = Visibility.Visible;
        if (fresh) tray?.Tell("Nová verze widgetu", $"Je k dispozici verze {Updater.Format(found.Version)}. Nainstaluješ ji z menu widgetu.");
    }

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (offer is null) return;
        MenuUpdate.IsEnabled = false;
        MenuUpdate.Header = "Stahuji aktualizaci…";
        if (await Updater.InstallAsync(offer))
        {
            Close();
            return;
        }
        MenuUpdate.Header = "Aktualizace se nepovedla, zkusit znovu";
        MenuUpdate.IsEnabled = true;
    }

    private void OnTopmostClick(object sender, RoutedEventArgs e)
    {
        Topmost = MenuTopmost.IsChecked;
        settings.Topmost = Topmost;
        settings.Save();
    }

    private void OnAutostartClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Autostart.Set(MenuAutostart.IsChecked);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            Log.Write($"Spouštění s Windows nejde nastavit ({ex.GetType().Name}).");
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Starts the widget on the desktop, or only in the notification area if that is where it was left.</summary>
    internal void Start()
    {
        if (settings.InTray) new WindowInteropHelper(this).EnsureHandle();
        else Show();
    }
}
