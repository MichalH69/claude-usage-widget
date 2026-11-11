using System.Windows;
using System.Windows.Media;

namespace ClaudeUsageWidget.Skins;

/// <summary>
/// Traffic on the far motorway behind the crash barrier: small cars going both ways at their own speeds. They are
/// far off, so they are small and hazy, and nobody takes them for the cars that show the limits.
/// </summary>
public sealed class BackgroundTraffic : TankLayer, ITankLife
{
    private const double RoadLength = 302;
    private const double Scale = 0.105;

    private sealed record Lane(double Y, int Direction);

    /// <summary>The far carriageway runs to the right, the near one to the left.</summary>
    private static readonly Lane[] Lanes = { new(48.6, 1), new(54.2, -1) };

    private sealed class Car
    {
        public required DrawingVisual Visual;
        public required Lane Lane;
        public double X, Speed;
    }

    private readonly Random random = new(23);
    private readonly List<Car> cars = new();

    public BackgroundTraffic()
    {
        (int Lane, double X)[] start = { (0, 30), (0, 130), (0, 236), (1, 60), (1, 170), (1, 262), (1, 330) };
        foreach (var (lane, x) in start)
        {
            var car = new Car { Visual = AddPiece(), Lane = Lanes[lane], X = x };
            Respray(car);
            cars.Add(car);
        }
    }

    /// <summary>A car that has left the picture comes back in at the other end as another car.</summary>
    private void Respray(Car car)
    {
        car.Speed = 26 + random.NextDouble() * 22;
        CarModel model = CarArt.Models[random.Next(CarArt.Models.Length)];
        Color paint = CarArt.Paints[random.Next(CarArt.Paints.Length)];
        using DrawingContext dc = car.Visual.RenderOpen();
        // Seen from the far side of the road, a car going right is the mirror of one going left.
        dc.PushTransform(new ScaleTransform(-car.Lane.Direction * Scale, Scale));
        CarArt.Draw(dc, model, paint, 0, braking: false);
        dc.Pop();
        Place(car);
    }

    public void Animate(double now, double step)
    {
        foreach (Car car in cars)
        {
            car.X += car.Lane.Direction * car.Speed * step;
            bool gone = car.Lane.Direction > 0 ? car.X > RoadLength + 20 : car.X < -20;
            if (gone)
            {
                car.X = car.Lane.Direction > 0 ? -20 - random.NextDouble() * 60 : RoadLength + 20 + random.NextDouble() * 60;
                Respray(car);
            }
            else
            {
                Place(car);
            }
        }
    }

    private static void Place(Car car) => car.Visual.Transform = new TranslateTransform(car.X, car.Lane.Y);
}
