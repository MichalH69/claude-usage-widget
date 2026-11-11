using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudeUsageWidget.Data;
using ClaudeUsageWidget.ViewModels;

namespace ClaudeUsageWidget.Dev;

/// <summary>Switches for checking the widget without waiting for the real thing to happen.</summary>
internal static class DevTools
{
    /// <summary>--fetch &lt;out.json&gt;: one real request. The response body goes to the file, the status to the exit code.</summary>
    public static int Fetch(string[] args)
    {
        FetchResult result = new UsageClient().FetchAsync().GetAwaiter().GetResult();
        if (result.Body is not null && args.Length > 1) File.WriteAllText(Path.GetFullPath(args[1]), result.Body);
        return (int)result.Status;
    }

    /// <summary>
    /// --renew &lt;out.txt&gt;: what one poll does about an expired token. Writes the status before, whether Claude Code
    /// moved the expiry, and the status after. Meant for a throwaway CLAUDE_CONFIG_DIR, never for the real login.
    /// </summary>
    public static int Renew(string[] args)
    {
        Log.Write("Zkouška obnovení tokenu přepínačem --renew.");
        var client = new UsageClient();
        FetchStatus before = client.FetchAsync().GetAwaiter().GetResult().Status;
        bool renewed = ClaudeCli.TryRenewAsync().GetAwaiter().GetResult();
        FetchStatus after = client.FetchAsync().GetAwaiter().GetResult().Status;
        if (args.Length > 1)
        {
            File.WriteAllLines(Path.GetFullPath(args[1]), new[] { $"before: {before}", $"renewed: {renewed}", $"after: {after}" });
        }
        return renewed ? 0 : 1;
    }

    /// <summary>
    /// --parse &lt;in.json&gt; &lt;out.txt&gt; [status] [minutesLater]: what the widget would show for a saved response,
    /// optionally as if the last request had ended with the given status and that many minutes had passed since.
    /// </summary>
    public static int Parse(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 2 ? args[2] : "parse.txt");
        try
        {
            WidgetViewModel vm = Simulate(args[1], args.Skip(3).ToArray());
            static string Line(Model.LimitView limit) =>
                $"{limit.PercentText} | {limit.ResetText} | {limit.Countdown} | {limit.ResetClock} | {limit.Level}";
            File.WriteAllLines(output, new[]
            {
                $"session: {Line(vm.Session)}",
                $"weekly:  {Line(vm.Weekly)}",
                $"fable:   {Line(vm.Fable)}",
                $"updated: {vm.UpdatedText}",
                $"status:  {vm.StatusLine}",
                $"problem: {vm.Problem ?? "(none)"}",
                $"stale:   {vm.IsStale}",
            });
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(output + ".error.txt", e.ToString());
            return 1;
        }
    }

    /// <summary>
    /// --shot &lt;out.png&gt; &lt;gauges|aquarium|road&gt; &lt;in.json&gt; [large] [hover|popup] [status] [minutesLater]: shows the real
    /// window with a saved response, lets its animations finish and saves a picture of it on the wallpaper.
    /// </summary>
    public static int Shot(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 1 ? args[1] : "shot.png");
        try
        {
            if (args.Length < 4) throw new ArgumentException("Usage: --shot <out.png> <gauges|aquarium|road> <in.json> [large] [hover|popup] [status] [minutesLater]");
            bool popup = args.Contains("popup"), hover = popup || args.Contains("hover"), large = args.Contains("large");
            WidgetViewModel vm = Simulate(args[3], args.Skip(4).Where(a => a is not ("hover" or "popup" or "large")).ToArray());

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var window = new WidgetWindow(new Settings { Skin = args[2], Compact = !large, Transient = true }, vm, poller: null);
            window.Show();

            // First wait for the needles and fish to settle, then for the cog and the row to fade in
            // and for the needles to stop trembling.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
            bool settled = false;
            timer.Tick += (_, _) =>
            {
                if (!settled)
                {
                    settled = true;
                    if (hover) window.ShowChromeForRender(popup);
                    timer.Interval = TimeSpan.FromMilliseconds(1400);
                    return;
                }
                timer.Stop();
                SavePng(Compose(window), output);
                app.Shutdown();
            };
            timer.Start();
            app.Run();
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(output + ".error.txt", e.ToString());
            return 1;
        }
    }

    private static WidgetViewModel Simulate(string jsonPath, string[] options)
    {
        string json = File.ReadAllText(jsonPath);
        UsageReading? reading = UsageParser.Parse(json) ?? UsageParser.ParseStateFile(json)?.Reading;

        FetchStatus status = options.Length > 0 ? Enum.Parse<FetchStatus>(options[0], ignoreCase: true) : FetchStatus.Ok;
        double minutesLater = options.Length > 1 ? double.Parse(options[1], CultureInfo.InvariantCulture) : 0;
        if (reading is null && status == FetchStatus.Ok) status = FetchStatus.UnknownFormat;

        DateTimeOffset readAt = DateTimeOffset.Now;
        var vm = new WidgetViewModel();
        vm.Update(reading, reading is null ? null : readAt, status, hasPolled: true, readAt.AddMinutes(minutesLater));
        return vm;
    }

    internal static BitmapSource Compose(WidgetWindow window)
    {
        const double pad = 44, scale = 2;
        FrameworkElement root = window.RenderRoot;
        double width = root.ActualWidth + 2 * pad, height = root.ActualHeight + 2 * pad;

        var picture = new DrawingVisual();
        using (DrawingContext dc = picture.RenderOpen())
        {
            dc.DrawRectangle(SnapshotRenderer.LoadWallpaper(width, height) ?? SnapshotRenderer.FallbackBackdrop(), null, new Rect(0, 0, width, height));
            Draw(dc, root, new Point(pad, pad), bleed: 0);

            if (window.OpenPicker is { } row)
            {
                // The row lives in a window of its own. Find where that window sits relative to the widget.
                double pixelsPerDip = VisualTreeHelper.GetDpi(window).DpiScaleX;
                Vector offset = (row.PointToScreen(default) - root.PointToScreen(default)) / pixelsPerDip;
                Draw(dc, row, new Point(pad, pad) + offset, bleed: 7);
            }
        }

        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(picture);
        return bitmap;
    }

    /// <summary>Paints an element as it is on screen. The bleed takes in what it draws past its own edge, such as a shadow.</summary>
    private static void Draw(DrawingContext dc, FrameworkElement element, Point at, double bleed)
    {
        // A brush sees the element where its parent has put it, not at its own zero.
        Vector own = VisualTreeHelper.GetOffset(element);
        var area = new Rect(own.X - bleed, own.Y - bleed, element.ActualWidth + 2 * bleed, element.ActualHeight + 2 * bleed);
        var brush = new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = area, Stretch = Stretch.Fill };
        dc.DrawRectangle(brush, null, new Rect(at.X - bleed, at.Y - bleed, area.Width, area.Height));
    }

    internal static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
