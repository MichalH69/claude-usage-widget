using System.IO;
using System.Windows;
using ClaudeUsageWidget.Data;
using ClaudeUsageWidget.ViewModels;

namespace ClaudeUsageWidget;

public static class Program
{
    [STAThread]
    public static int Main(string[] args) => args.FirstOrDefault() switch
    {
        "--snapshot" => Dev.SnapshotRenderer.Run(args),
        "--fetch" => Dev.DevTools.Fetch(args),
        "--renew" => Dev.DevTools.Renew(args),
        "--parse" => Dev.DevTools.Parse(args),
        "--shot" => Dev.DevTools.Shot(args),
        "--trayicon" => TrayIconPicture(args),
        _ => RunWidget(args),
    };

    /// <summary>Writes the notification area picture as PNG: --trayicon out.png size</summary>
    private static int TrayIconPicture(string[] args)
    {
        File.WriteAllBytes(Path.GetFullPath(args[1]), TrayIcon.FishRingPng(int.Parse(args[2])));
        return 0;
    }

    /// <summary>How often the widget looks on GitHub for a newer version, and how long after starting it first does.</summary>
    private static readonly TimeSpan UpdateCheck = TimeSpan.FromHours(24), FirstUpdateCheck = TimeSpan.FromMinutes(2);

    private static int RunWidget(string[] args)
    {
        // A freshly updated widget waits here for the version it replaced to close.
        Updater.FinishReplacing(args);

        using var onlyOne = new Mutex(initiallyOwned: true, @"Local\ClaudeUsageWidget", out bool first);
        if (!first) return 0;

        // The widget may live in the notification area with no window showing, so it ends only when it is closed.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => Log.Write($"Neošetřená chyba, widget končí: {e.Exception}");

        var viewModel = new WidgetViewModel();
        var poller = new UsagePoller();
        poller.Changed += () => viewModel.Update(poller);

        var window = new WidgetWindow(Settings.Load(), viewModel, poller);
        app.MainWindow = window;
        window.Closed += (_, _) =>
        {
            poller.Stop();
            app.Shutdown();
        };
        // The first request waits for the message loop, so its answer comes back to this thread.
        app.Dispatcher.BeginInvoke(poller.Start);
        window.Start();

        // Now and then a look on GitHub for a newer version. Finding one only offers it; nothing installs unasked.
        var updates = new System.Windows.Threading.DispatcherTimer { Interval = FirstUpdateCheck };
        updates.Tick += async (_, _) =>
        {
            updates.Interval = UpdateCheck;
            if (await Updater.CheckAsync() is { } offer) window.OfferUpdate(offer);
        };
        updates.Start();

        Log.Write($"Widget spuštěn, verze {Updater.Format(Updater.Current)}.");
        return app.Run();
    }
}
