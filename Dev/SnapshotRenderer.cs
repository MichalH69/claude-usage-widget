using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClaudeUsageWidget.Model;
using ClaudeUsageWidget.Skins;

namespace ClaudeUsageWidget.Dev;

/// <summary>
/// Renders a skin with made-up values to a PNG, on top of the current desktop wallpaper.
/// Usage: --snapshot &lt;gauges|aquarium|road&gt; &lt;out.png&gt; [large] [hover] [left=s,w,f] &lt;session,week,fable[,stale]&gt; [more frames...]
/// Each group of values becomes one frame; frames are stacked vertically. The skin is the small one unless
/// "large" is given, and "hover" shows it as it looks under the pointer. "left" sets how much of each limit's window
/// is still to run, from 0 to 1; the road's signs colour their time by it.
/// </summary>
public static class SnapshotRenderer
{
    private const double FrameWidth = 420;
    private const double FrameHeight = 320;
    private const double Scale = 2;

    public static int Run(string[] args)
    {
        string output = args.Length > 2 ? System.IO.Path.GetFullPath(args[2]) : "snapshot.png";
        try
        {
            bool large = args.Contains("large"), hover = args.Contains("hover");
            double[] timeLeft = args.FirstOrDefault(a => a.StartsWith("left="))?[5..].Split(',')
                .Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray() ?? new[] { 0.84, 0.37, 0.37 };
            var frames = args.Skip(3).Where(a => a is not ("large" or "hover") && !a.StartsWith("left=")).Select(ParseTriple).ToList();
            // A picture shows the queues standing, not driving in.
            TrafficLane.StartSettled = true;
            if (frames.Count == 0)
                throw new ArgumentException("Usage: --snapshot <gauges|aquarium|road> <out.png> [large] [hover] <session,week,fable[,stale]> [...]");

            if (Application.Current == null)
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            string skinName = args[1];

            double width = FrameWidth;
            double height = FrameHeight * frames.Count;
            var root = new Canvas { Width = width, Height = height, ClipToBounds = true };

            Brush backdrop = LoadWallpaper(width, height) ?? FallbackBackdrop();
            root.Children.Add(new Rectangle { Width = width, Height = height, Fill = backdrop });

            for (int i = 0; i < frames.Count; i++)
            {
                var (session, week, fable, stale) = frames[i];
                FrameworkElement skin = skinName switch
                {
                    "gauges" => new GaugesSkin(),
                    "aquarium" => new AquariumSkin(),
                    "road" => new RoadSkin(),
                    _ => throw new ArgumentException($"Unknown skin '{skinName}'."),
                };
                skin.DataContext = new WidgetState
                {
                    Session = Sample(session, "za 4 h 12 min", "4 h 12 min", "18:45", timeLeft[0]),
                    Weekly = Sample(week, "čt 15:00", "2 d 13 h", "čt 15:00", timeLeft[1]),
                    Fable = Sample(fable, "čt 15:00", "2 d 13 h", "čt 15:00", timeLeft[2]),
                    UpdatedText = stale ? "čeká na Claude Code" : "14:32",
                    IsStale = stale,
                };
                ((IWidgetSkin)skin).Compact = !large;
                ((IWidgetSkin)skin).Hovered = hover;

                // The skin's own Width and Height do not tell its size: a skin may scale itself.
                skin.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double left = Math.Round((FrameWidth - skin.DesiredSize.Width) / 2);
                double top = Math.Round(i * FrameHeight + (FrameHeight - skin.DesiredSize.Height) / 2);

                Canvas.SetLeft(skin, left);
                Canvas.SetTop(skin, top);
                root.Children.Add(skin);
            }

            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var bitmap = new RenderTargetBitmap((int)(width * Scale), (int)(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
            bitmap.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
            using (var stream = System.IO.File.Create(output))
                encoder.Save(stream);
            return 0;
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(output + ".error.txt", ex.ToString());
            return 1;
        }
    }

    /// <summary>A limit with made-up reset times, or the empty one the live widget shows while it has no data.</summary>
    private static LimitView Sample(double percent, string reset, string countdown, string clock, double left) =>
        double.IsNaN(percent) ? LimitView.Empty : new LimitView(percent, reset) { Countdown = countdown, ResetClock = clock, TimeLeft = left };

    private static (double Session, double Week, double Fable, bool Stale) ParseTriple(string text)
    {
        string[] parts = text.Split(',');
        bool stale = parts.Length == 4 && parts[3] == "stale";
        if (parts.Length != (stale ? 4 : 3)) throw new ArgumentException($"Expected session,week,fable[,stale] but got '{text}'.");

        double[] p = parts.Take(3).Select(s => s == "-" ? double.NaN : double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        return (p[0], p[1], p[2], stale);
    }

    /// <summary>Cuts the part of the wallpaper a widget in the upper right of a 1080p-high desktop would sit on.</summary>
    internal static Brush? LoadWallpaper(double width, double height)
    {
        try
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper");
            if (!System.IO.File.Exists(path)) return null;

            BitmapSource source;
            using (var stream = System.IO.File.OpenRead(path))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                source = decoder.Frames[0];
            }

            double pixelsPerDip = source.PixelHeight / 1080.0;
            int cropWidth = (int)Math.Min(source.PixelWidth, width * pixelsPerDip);
            int cropHeight = (int)Math.Min(source.PixelHeight, height * pixelsPerDip);
            int x = (int)((source.PixelWidth - cropWidth) * 0.80);
            int y = (int)((source.PixelHeight - cropHeight) * 0.18);

            var crop = new CroppedBitmap(source, new Int32Rect(x, y, cropWidth, cropHeight));
            crop.Freeze();
            var brush = new ImageBrush(crop) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            return brush;
        }
        catch
        {
            return null;
        }
    }

    internal static Brush FallbackBackdrop()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x2C, 0x5A, 0x8C), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x6F, 0xA8, 0xC9), 0.55));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xD9, 0xC7, 0xA8), 1));
        brush.Freeze();
        return brush;
    }
}
