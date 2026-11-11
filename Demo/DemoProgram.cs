using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClaudeUsageWidget.Data;
using ClaudeUsageWidget.ViewModels;

namespace ClaudeUsageWidget.Demo;

/// <summary>
/// The test build of the widget. It shows the real window with values set by hand in a small panel beside it, and
/// never asks the server anything: the build has no way to. It saves no settings and leaves the real widget alone.
/// <para>
/// ClaudeUsageWidget.Demo.exe [gauges|aquarium|road] [large]: the widget and the panel.<br/>
/// ClaudeUsageWidget.Demo.exe --record &lt;folder&gt; [skin] [large]: plays a fixed scene and saves pictures of it along the way.
/// </para>
/// </summary>
public static class DemoProgram
{
    private static readonly TimeSpan SessionWindow = TimeSpan.FromHours(5), WeekWindow = TimeSpan.FromDays(7);

    [STAThread]
    public static int Main(string[] args)
    {
        bool record = args.FirstOrDefault() == "--record";
        string? folder = record && args.Length > 1 ? Path.GetFullPath(args[1]) : null;
        string skin = args.FirstOrDefault(a => a is Settings.Gauges or Settings.Aquarium or Settings.Road) ?? Settings.Road;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var state = new DemoState();
        Rect work = SystemParameters.WorkArea;
        var settings = new Settings
        {
            Skin = skin,
            Compact = !args.Contains("large"),
            Transient = true,
            // In front of everything: a widget that other windows cover stands still, and the test would show nothing.
            Topmost = true,
            // Away from the corner where the real widget usually sits.
            Left = work.Left + 300,
            Top = work.Top + 120,
        };
        var window = new WidgetWindow(settings, state.ViewModel, poller: null) { Title = "Využití Claude (test)" };
        window.Closed += (_, _) => app.Shutdown();

        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
            state.Push();
            window.Show();
            PlayScene(app, window, state, folder);
        }
        else
        {
            state.Push();
            window.Show();
            var panel = new DemoPanel(state) { Left = work.Left + 40, Top = work.Top + 120 };
            panel.Closed += (_, _) => window.Close();
            panel.Show();
        }
        return app.Run();
    }

    /// <summary>A fixed scene: queues drive in, the session resets, the week grows. A picture is saved at each step.</summary>
    private static void PlayScene(Application app, WidgetWindow window, DemoState state, string folder)
    {
        var steps = new List<(double At, Action Do)>();
        void Shot(double at, string name) => steps.Add((at, () => Dev.DevTools.SavePng(Dev.DevTools.Compose(window), Path.Combine(folder, name + ".png"))));
        void Act(double at, Action act) => steps.Add((at, act));

        Shot(0.5, "01-prijezd");
        Shot(1.5, "02-prijezd");
        Shot(4.0, "03-kolona");
        Act(4.2, () => { state.Session = 3; state.SessionLeft = 1; state.Push(); });
        Shot(4.6, "04-oranzova");
        Shot(5.6, "05-zelena-odjezd");
        Shot(7.0, "06-odjezd");
        Shot(8.6, "07-zpet-cervena");
        Shot(10.5, "08-nova-kolona");
        Act(10.7, () => { state.Week = 72; state.Push(); });
        Shot(11.6, "09-pribyvaji");
        Shot(14.0, "10-ustaleno");
        Act(14.2, () => window.ShowChromeForRender(false));
        Shot(15.0, "11-pod-kurzorem");
        Act(15.4, () => app.Shutdown());

        var clock = System.Diagnostics.Stopwatch.StartNew();
        int next = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            while (next < steps.Count && clock.Elapsed.TotalSeconds >= steps[next].At) steps[next++].Do();
        };
        timer.Start();
    }

    /// <summary>The made-up values, turned into a reading the way the real widget gets one.</summary>
    internal sealed class DemoState
    {
        public WidgetViewModel ViewModel { get; } = new();
        public double Session = 44, Week = 35, Fable = 25;
        public double SessionLeft = 0.84, WeekLeft = 0.37, FableLeft = 0.37;
        public bool NoData, Stale;

        public void Push()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            UsageReading? reading = NoData ? null : new UsageReading(
                new LimitReading(Session, now + SessionWindow * SessionLeft + TimeSpan.FromSeconds(30)),
                new LimitReading(Week, now + WeekWindow * WeekLeft + TimeSpan.FromSeconds(30)),
                new LimitReading(Fable, now + WeekWindow * FableLeft + TimeSpan.FromSeconds(30)));
            DateTimeOffset? readAt = NoData ? null : Stale ? now - TimeSpan.FromHours(1) : now;
            ViewModel.Update(reading, readAt, FetchStatus.Ok, hasPolled: !NoData, now);
        }
    }

    /// <summary>The panel that sets the values: a slider for each limit and for the time left, and a reset for each.</summary>
    private sealed class DemoPanel : Window
    {
        private readonly DemoState state;
        private readonly Random random = new();
        private readonly DispatcherTimer drift = new() { Interval = TimeSpan.FromSeconds(2) };
        private readonly Slider session, week, fable;

        public DemoPanel(DemoState state)
        {
            this.state = state;
            Title = "Testovací panel";
            Width = 300;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;

            var rows = new StackPanel { Margin = new Thickness(12) };
            rows.Children.Add(new TextBlock
            {
                Text = "Testovací verze. Hodnoty jsou smyšlené a na server nejde žádný dotaz.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
            });

            session = Limit(rows, "Session", state.Session, v => state.Session = v);
            TimeLeftSlider(rows, "Session: zbývá z okna", state.SessionLeft, v => state.SessionLeft = v);
            week = Limit(rows, "Týden", state.Week, v => state.Week = v);
            TimeLeftSlider(rows, "Týden: zbývá z okna", state.WeekLeft, v => state.WeekLeft = v);
            fable = Limit(rows, "Fable", state.Fable, v => state.Fable = v);
            TimeLeftSlider(rows, "Fable: zbývá z okna", state.FableLeft, v => state.FableLeft = v);

            rows.Children.Add(Check("Bez dat", v => state.NoData = v));
            rows.Children.Add(Check("Stará data", v => state.Stale = v));
            rows.Children.Add(Check("Samo se vyvíjí (každé 2 s)", v =>
            {
                if (v) drift.Start();
                else drift.Stop();
            }));
            Content = rows;

            drift.Tick += (_, _) => Drift();
            Closed += (_, _) => drift.Stop();
        }

        private Slider Limit(Panel rows, string name, double value, Action<double> set)
        {
            var label = new TextBlock { Margin = new Thickness(0, 6, 0, 0) };
            var slider = new Slider { Minimum = 0, Maximum = 100, Value = value, SmallChange = 1, LargeChange = 10 };
            var reset = new Button { Content = "Reset", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(8, 0, 0, 0) };
            void Show() => label.Text = $"{name}: {Math.Round(slider.Value)} %";
            slider.ValueChanged += (_, e) =>
            {
                set(Math.Round(e.NewValue));
                Show();
                state.Push();
            };
            reset.Click += (_, _) => slider.Value = 0;
            Show();

            var line = new DockPanel();
            DockPanel.SetDock(reset, Dock.Right);
            line.Children.Add(reset);
            line.Children.Add(slider);
            rows.Children.Add(label);
            rows.Children.Add(line);
            return slider;
        }

        private void TimeLeftSlider(Panel rows, string name, double value, Action<double> set)
        {
            var label = new TextBlock { Foreground = System.Windows.Media.Brushes.DimGray };
            var slider = new Slider { Minimum = 0, Maximum = 1, Value = value, SmallChange = 0.01, LargeChange = 0.1 };
            void Show() => label.Text = $"{name}: {Math.Round(slider.Value * 100)} %";
            slider.ValueChanged += (_, e) =>
            {
                set(e.NewValue);
                Show();
                state.Push();
            };
            Show();
            rows.Children.Add(label);
            rows.Children.Add(slider);
        }

        private CheckBox Check(string text, Action<bool> set)
        {
            var box = new CheckBox { Content = text, Margin = new Thickness(0, 8, 0, 0) };
            box.Checked += (_, _) =>
            {
                set(true);
                state.Push();
            };
            box.Unchecked += (_, _) =>
            {
                set(false);
                state.Push();
            };
            return box;
        }

        /// <summary>The limits fill up bit by bit, and now and then one of them resets.</summary>
        private void Drift()
        {
            foreach (Slider slider in new[] { session, week, fable })
            {
                slider.Value = random.NextDouble() < 0.06 ? 0 : Math.Min(100, slider.Value + random.Next(0, 5));
            }
        }
    }
}
