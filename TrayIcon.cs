using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudeUsageWidget.Skins;
using Forms = System.Windows.Forms;

namespace ClaudeUsageWidget;

/// <summary>
/// The widget's icon in the notification area. It is there the whole time the widget runs, whether the widget
/// shows on the desktop or not. A left click shows or hides the widget; a right click opens the widget's own menu.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly ContextMenu menu;

    public TrayIcon(ContextMenu menu, Action toggle)
    {
        this.menu = menu;
        using var picture = new MemoryStream(FishRingIcon());
        icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(picture, Forms.SystemInformation.SmallIconSize),
            Text = "Využití Claude",
            Visible = true,
        };
        icon.MouseUp += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) toggle();
            else if (e.Button == Forms.MouseButtons.Right) OpenMenu();
        };
    }

    /// <summary>The words shown when the pointer rests on the icon. Windows takes at most 127 characters.</summary>
    public string Text
    {
        set => icon.Text = value.Length > 127 ? value[..127] : value;
    }

    /// <summary>A short message from the notification area, which Windows shows and then puts away.</summary>
    public void Tell(string title, string text) => icon.ShowBalloonTip(8000, title, text, Forms.ToolTipIcon.Info);

    /// <summary>The same menu as a right click on the widget, opened where the pointer is.</summary>
    private void OpenMenu()
    {
        menu.PlacementTarget = null;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        // A menu opened from the notification area closes on a click elsewhere only if its window is in front.
        if (PresentationSource.FromVisual(menu) is HwndSource source) SetForegroundWindow(source.Handle);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    // ---- the picture ----

    /// <summary>An .ico holding the picture at the sizes Windows asks for at the usual display scales.</summary>
    private static byte[] FishRingIcon()
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64 };
        byte[][] images = sizes.Select(FishRingPng).ToArray();

        using var ico = new MemoryStream();
        using var w = new BinaryWriter(ico);
        w.Write((short)0);
        w.Write((short)1);
        w.Write((short)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)sizes[i]);
            w.Write((byte)sizes[i]);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((short)1);
            w.Write((short)32);
            w.Write(images[i].Length);
            w.Write(offset);
            offset += images[i].Length;
        }
        foreach (byte[] image in images) w.Write(image);
        w.Flush();
        return ico.ToArray();
    }

    /// <summary>The picture at one size, as PNG.</summary>
    internal static byte[] FishRingPng(int size)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 100.0, size / 100.0));
            DrawFishRing(dc);
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        png.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// The three fish of the aquarium, a third of a circle apart, each swimming after the one ahead of it round
    /// the circle. Drawn in a box 100 units square. A fish on its way left is turned round, not upside down.
    /// </summary>
    private static void DrawFishRing(DrawingContext dc)
    {
        const double radius = 25, length = 56;
        var center = new Point(50, 50);
        FishKind[] kinds = { FishKind.BlueTang, FishKind.Clownfish, FishKind.PurpleTang };

        for (int i = 0; i < kinds.Length; i++)
        {
            double around = (-90 + i * 120) * Math.PI / 180;
            var at = new Point(center.X + radius * Math.Cos(around), center.Y + radius * Math.Sin(around));
            // Clockwise on the screen, the way is a quarter turn on from where the fish stands.
            double heading = around * 180 / Math.PI + 90;
            bool left = Math.Cos(heading * Math.PI / 180) < -0.01;

            var place = new TransformGroup();
            place.Children.Add(new TranslateTransform(-55, 0));
            place.Children.Add(new ScaleTransform(length / 100 * (left ? -1 : 1), length / 100));
            place.Children.Add(new RotateTransform(left ? heading - 180 : heading));
            place.Children.Add(new TranslateTransform(at.X, at.Y));

            FishParts fish = FishArt.Get(kinds[i]);
            dc.PushTransform(place);
            dc.DrawDrawing(fish.Tail);
            dc.DrawDrawing(fish.Fins);
            dc.DrawDrawing(fish.Body);
            dc.DrawDrawing(fish.Pectoral);
            FishArt.DrawPupil(dc, kinds[i], new Vector(0.5, 0));
            dc.Pop();
        }
    }
}
