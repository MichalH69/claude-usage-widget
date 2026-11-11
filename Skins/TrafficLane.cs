using System.Windows;
using System.Windows.Media;

namespace ClaudeUsageWidget.Skins;

/// <summary>
/// One lane of the road. Cars come in from the right and queue up from the traffic light on the left, one car for
/// every ten percent of the limit used, so the queue is as long as the limit is full. When the limit resets the light
/// turns green, the queue drives off to the left, the light turns red again and cars for the new value come in.
/// <para>
/// The lane is laid out in its own box: the stop line at <see cref="StopX"/>, the road surface from y 30.4 to 52.4
/// with a solid line along each edge and a broken line down the middle, the cars' wheels on <see cref="Ground"/>,
/// so the cars stand in the middle of the road.
/// </para>
/// </summary>
public sealed class TrafficLane : TankLayer, ITankLife
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(TrafficLane),
        new FrameworkPropertyMetadata(double.NaN, (d, e) => ((TrafficLane)d).OnValueChanged((double)e.OldValue, (double)e.NewValue)));

    /// <summary>The percentage used, NaN while there is no data.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Which of the lanes this is. Each lane has its own run of cars, so a car keeps its look from one day to the next.</summary>
    public int Seed { get; set; }

    /// <summary>Which cars this lane's queues are made of.</summary>
    public Fleet Fleet { get; set; }

    /// <summary>Set for test pictures: cars stand in the queue at once instead of driving in.</summary>
    internal static bool StartSettled { get; set; }

    public const double StopX = 34;
    public const double Slot = 26;
    public const int Slots = 10;
    private const double Ground = 46;
    private const double CarScale = 0.25;

    /// <summary>Where cars come in from: just past the right edge of the road.</summary>
    private const double Entry = 312;

    private const double Cruise = 95, Braking = 150, Pickup = 120;
    private const double LeaveSpeed = 140, LeavePickup = 85;
    private const double AmberTime = 0.7, GreenAtLeast = 2.6;

    private enum Light { Red, ToGreen, Green, ToRed }

    private sealed class Car
    {
        public required DrawingVisual Visual;
        public required CarModel Model;
        public required Color Paint;
        public double X, Speed, Target, Delay, Turn;
        public bool Leaving;

        // What the picture last showed, so it is drawn again only when it changes.
        public double DrawnTurn = double.NaN, DrawnX = double.NaN;
        public bool DrawnBraking;
    }

    private readonly List<Car> queue = new();
    private readonly List<Car> leaving = new();
    private readonly DrawingVisual light;
    private Light state = Light.Red;
    private double stateTime;
    /// <summary>How many queues have formed in this lane. Each new queue is made of other cars.</summary>
    private int generation;

    public TrafficLane()
    {
        DrawRoad(AddPiece());
        light = AddPiece();
        DrawLight();
    }

    /// <summary>How many cars stand for a value: none at zero, otherwise one for every ten percent, and at least one.</summary>
    internal static int CarsFor(double percent) =>
        double.IsNaN(percent) || percent <= 0 ? 0 : Math.Clamp((int)Math.Round(percent / 10, MidpointRounding.AwayFromZero), 1, Slots);

    private void OnValueChanged(double before, double now)
    {
        if (double.IsNaN(now))
        {
            // No data: the road is empty.
            foreach (Car car in queue.Concat(leaving)) RemovePiece(car.Visual);
            queue.Clear();
            leaving.Clear();
            return;
        }

        // A limit that went down has reset: the light turns green and the queue drives off.
        if (!double.IsNaN(before) && now < before - 0.5 && queue.Count > 0 && !StartSettled)
        {
            if (state == Light.Red) SetLight(Light.ToGreen);
            return;
        }
        if (state == Light.Red) FillQueue();
    }

    /// <summary>Brings the queue to the length the value asks for: new cars drive in at its end, spare ones leave the road.</summary>
    private void FillQueue()
    {
        int wanted = CarsFor(Value);
        while (queue.Count > wanted)
        {
            Car spare = queue[^1];
            RemovePiece(spare.Visual);
            queue.RemoveAt(queue.Count - 1);
        }

        double delay = 0;
        while (queue.Count < wanted)
        {
            int slot = queue.Count;
            var (models, paints) = CarArt.Of(Fleet);
            CarModel model = models[Pick(slot, 3) % models.Length];
            var car = new Car
            {
                Visual = AddPiece(),
                Model = model,
                Paint = paints[Pick(slot, 7) % paints.Length],
                Target = StopX + slot * Slot,
            };
            if (StartSettled)
            {
                car.X = car.Target;
            }
            else
            {
                // One after another, each a little behind the one before, so they come in as traffic does.
                car.X = Math.Max(Entry, queue.Count > 0 ? queue[^1].X + Slot : Entry);
                car.Delay = delay;
                car.Speed = Cruise;
                delay += 0.32;
            }
            queue.Add(car);
            Draw(car, 0);
        }
    }

    /// <summary>
    /// A fixed choice for the car in a slot, the same every time the widget starts. HashCode is not used: it changes
    /// from one run to the next.
    /// </summary>
    private int Pick(int slot, int salt)
    {
        unchecked
        {
            uint n = (uint)(Seed * 7919 + generation * 104729 + slot * 1301 + salt * 31337);
            n ^= n >> 13;
            n *= 0x5bd1e995;
            n ^= n >> 15;
            return (int)(n & 0x7fffffff);
        }
    }

    private void SetLight(Light next)
    {
        state = next;
        stateTime = 0;
        DrawLight();

        if (next == Light.Green)
        {
            // Off they go, the front car first, each a moment after the one in front.
            for (int i = 0; i < queue.Count; i++)
            {
                Car car = queue[i];
                car.Leaving = true;
                car.Delay = i * 0.35;
                car.Speed = 0;
                leaving.Add(car);
            }
            queue.Clear();
        }
        else if (next == Light.Red)
        {
            generation++;
            FillQueue();
        }
    }

    public void Animate(double now, double step)
    {
        stateTime += step;
        switch (state)
        {
            case Light.ToGreen when stateTime >= AmberTime:
                SetLight(Light.Green);
                break;
            case Light.Green when stateTime >= GreenAtLeast && leaving.Count == 0:
                SetLight(Light.ToRed);
                break;
            case Light.ToRed when stateTime >= AmberTime:
                SetLight(Light.Red);
                break;
        }

        for (int i = 0; i < queue.Count; i++) Drive(queue[i], i > 0 ? queue[i - 1] : null, step);
        foreach (Car car in leaving) Drive(car, null, step);

        for (int i = leaving.Count - 1; i >= 0; i--)
        {
            if (leaving[i].X < -30)
            {
                RemovePiece(leaving[i].Visual);
                leaving.RemoveAt(i);
            }
        }

        foreach (Car car in queue.Concat(leaving)) Place(car, now);
    }

    private static void Drive(Car car, Car? ahead, double step)
    {
        if (car.Delay > 0)
        {
            car.Delay -= step;
            return;
        }

        double moved;
        if (car.Leaving)
        {
            car.Speed = Math.Min(LeaveSpeed, car.Speed + LeavePickup * step);
            moved = car.Speed * step;
        }
        else
        {
            // Brakes so as to stop on its slot, and never closer to the car in front than a slot.
            double room = car.X - car.Target;
            if (ahead is not null) room = Math.Min(room, car.X - (ahead.X + Slot));
            if (room <= 0.05)
            {
                car.Speed = 0;
                return;
            }
            double wanted = Math.Min(Cruise, Math.Sqrt(2 * Braking * room));
            car.Speed = wanted < car.Speed ? wanted : Math.Min(wanted, car.Speed + Pickup * step);
            moved = Math.Min(room, car.Speed * step);
        }
        car.X -= moved;
        // The wheels roll with the road: left is anticlockwise.
        car.Turn -= moved / (CarArt.WheelRadius * CarScale);
    }

    /// <summary>
    /// Puts the car where it is. A car that has not moved is left alone: the window then has nothing to paint again,
    /// which is what keeps a road of standing queues cheap.
    /// </summary>
    private void Place(Car car, double now)
    {
        bool braking = !car.Leaving && car.Delay <= 0 && (car.Speed == 0 || car.X - car.Target < 40);
        if (Math.Abs(car.Turn - car.DrawnTurn) > 0.05 || braking != car.DrawnBraking) Draw(car, now);
        if (car.X != car.DrawnX)
        {
            car.Visual.Transform = new TranslateTransform(car.X, Ground);
            car.DrawnX = car.X;
        }
    }

    private void Draw(Car car, double now)
    {
        bool braking = !car.Leaving && car.Delay <= 0 && (car.Speed == 0 || car.X - car.Target < 40);
        using (DrawingContext dc = car.Visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(CarScale, CarScale));
            CarArt.Draw(dc, car.Model, car.Paint, car.Turn, braking);
            dc.Pop();
        }
        car.DrawnTurn = car.Turn;
        car.DrawnBraking = braking;
        car.Visual.Transform = new TranslateTransform(car.X, Ground);
        car.DrawnX = car.X;
    }

    /// <summary>
    /// The markings: a solid line along each edge, a broken line down the middle with one dash for every ten percent,
    /// so the empty part of the road reads as a scale, and the stop line across the road.
    /// </summary>
    private static void DrawRoad(DrawingVisual piece)
    {
        using DrawingContext dc = piece.RenderOpen();
        var white = new SolidColorBrush(Color.FromArgb(0xE8, 0xF4, 0xF6, 0xF8));
        var dash = new SolidColorBrush(Color.FromArgb(0xC8, 0xF4, 0xF6, 0xF8));
        dc.DrawRectangle(white, null, new Rect(0, 32.2, 302, 0.9));
        dc.DrawRectangle(white, null, new Rect(0, 50.2, 302, 1.1));
        for (int i = -1; i < Slots; i++)
        {
            dc.DrawRectangle(dash, null, new Rect(StopX + i * Slot + 6, 40.8, Slot - 12, 1));
        }
        dc.DrawRectangle(white, null, new Rect(StopX - 4.5, 33.4, 2.4, 16.6));
    }

    /// <summary>The traffic light at the stop line, its lamps facing the onlooker.</summary>
    private void DrawLight()
    {
        using DrawingContext dc = light.RenderOpen();
        var metal = new LinearGradientBrush(CarArt.Rgb(0x8C959C), CarArt.Rgb(0x3E454B), 0);
        dc.DrawRectangle(metal, null, new Rect(15.6, 20, 2.2, 30));

        var box = new Rect(10.5, 2.5, 12.4, 27);
        var housing = new LinearGradientBrush(CarArt.Rgb(0x3A4148), CarArt.Rgb(0x15191C), 0);
        dc.DrawRoundedRectangle(housing, new Pen(new SolidColorBrush(CarArt.Rgb(0x0A0C0E)), 0.7), box, 2.4, 2.4);

        Lamp(dc, 7.5, CarArt.Rgb(0xFF2B1E), state is Light.Red or Light.ToGreen);
        Lamp(dc, 16, CarArt.Rgb(0xFFB21C), state is Light.ToGreen or Light.ToRed);
        Lamp(dc, 24.5, CarArt.Rgb(0x3BF27A), state == Light.Green);
    }

    private static void Lamp(DrawingContext dc, double y, Color color, bool lit)
    {
        var center = new Point(16.7, y);
        if (lit)
        {
            dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0x90, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B)),
                null, center, 8, 8);
            dc.DrawEllipse(new RadialGradientBrush(CarArt.Mix(color, Colors.White, 0.7), color) { GradientOrigin = new Point(0.4, 0.35) },
                null, center, 3.3, 3.3);
        }
        else
        {
            dc.DrawEllipse(new SolidColorBrush(CarArt.Mix(color, CarArt.Rgb(0x101214), 0.78)), null, center, 3.3, 3.3);
        }
    }
}
