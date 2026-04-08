using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AirlineEmpire.Models;
using AirlineEmpire.ViewModels;

namespace AirlineEmpire.Views
{
    public partial class MapCanvas : UserControl
    {
        private MapViewModel? VM => DataContext as MapViewModel;

        private readonly DispatcherTimer _renderTimer;
        private readonly DrawingGroup _drawing = new DrawingGroup();

        private static readonly Pen RouteLinePen = new Pen(new SolidColorBrush(Color.FromArgb(120, 43, 127, 255)), 1.5);
        private static readonly Pen PendingLinePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 193, 7)), 1.5) { DashStyle = DashStyles.Dash };
        private static readonly Pen AirportRingPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 43, 127, 255)), 1.5);
        private static readonly Pen AlarmRingPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 61, 61)), 2.0);
        private static readonly Brush DotBlue = new SolidColorBrush(Color.FromRgb(43, 127, 255));
        private static readonly Brush DotGreen = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        private static readonly Brush DotAmber = new SolidColorBrush(Color.FromRgb(255, 193, 7));
        private static readonly Brush DotRed = new SolidColorBrush(Color.FromRgb(255, 61, 61));
        private static readonly Brush AircraftBrush = new SolidColorBrush(Color.FromRgb(100, 200, 255));
        private static readonly Typeface LabelFont = new Typeface("Microsoft YaHei");
        private static readonly Typeface MonoFont = new Typeface("Consolas");

        static MapCanvas()
        {
            // 冻结所有静态画笔/画笔提升渲染性能
            RouteLinePen.Freeze();
            PendingLinePen.Freeze();
            AirportRingPen.Freeze();
            AlarmRingPen.Freeze();
            DotBlue.Freeze();
            DotGreen.Freeze();
            DotAmber.Freeze();
            DotRed.Freeze();
            AircraftBrush.Freeze();
        }

        public MapCanvas()
        {
            InitializeComponent();

            Cursor = Cursors.Cross;  // 用代码设置，避免 XAML 解析 Cursor 字符串失败

            var host = new DrawingImage(_drawing);
            RootImage.Source = host;

            _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _renderTimer.Tick += (_, _) => Render();
            _renderTimer.Start();

            MouseLeftButtonDown += (s, e) => VM?.OnCanvasLeftClick(e.GetPosition(this));
            MouseRightButtonDown += (s, e) => VM?.OnCanvasRightClick(e.GetPosition(this));
            MouseMove += (s, e) => VM?.OnCanvasMouseMove(e.GetPosition(this));
            SizeChanged += (s, e) => UpdateCanvasSize();
            Loaded += (s, e) => UpdateCanvasSize();
        }

        private void UpdateCanvasSize()
        {
            if (VM == null) return;
            VM.CanvasWidth = ActualWidth;
            VM.CanvasHeight = ActualHeight;
        }

        private double Dpi => 96.0;  // 固定值，避免 VisualTreeHelper 在未加载时崩溃

        private void Render()
        {
            if (VM == null || ActualWidth < 1 || ActualHeight < 1) return;

            using var dc = _drawing.Open();

            dc.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(6, 13, 24)), null,
                new Rect(0, 0, ActualWidth, ActualHeight));

            DrawGrid(dc);
            DrawRoutes(dc);
            DrawPendingRoute(dc);
            DrawAircraft(dc);
            DrawAirports(dc);
        }

        private void DrawGrid(DrawingContext dc)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(20, 43, 127, 255)), 0.5);
            for (double x = 0; x < ActualWidth; x += 60)
                dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
            for (double y = 0; y < ActualHeight; y += 60)
                dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
        }

        private void DrawRoutes(DrawingContext dc)
        {
            if (VM == null) return;
            foreach (var route in VM.Routes)
            {
                var a = VM.Airports.FirstOrDefault(x => x.Id == route.AirportAId);
                var b = VM.Airports.FirstOrDefault(x => x.Id == route.AirportBId);
                if (a == null || b == null) continue;

                var pa = VM.AirportToCanvas(a);
                var pb = VM.AirportToCanvas(b);
                dc.DrawLine(RouteLinePen, pa, pb);

                var mid = new Point((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2);
                var txt = new FormattedText(
                    $"{route.DistanceKm:N0}km",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, MonoFont, 9,
                    new SolidColorBrush(Color.FromArgb(120, 100, 140, 200)), Dpi);
                dc.DrawText(txt, new Point(mid.X - txt.Width / 2, mid.Y - 8));
            }
        }

        private void DrawPendingRoute(DrawingContext dc)
        {
            if (VM?.PendingRoute == null) return;
            var pr = VM.PendingRoute;
            var pa = VM.AirportToCanvas(pr.FromAirport);
            dc.DrawLine(PendingLinePen, pa, new Point(pr.MouseX, pr.MouseY));

            var nearby = VM.HitTest(new Point(pr.MouseX, pr.MouseY), 30);
            if (nearby != null && nearby != pr.FromAirport)
            {
                double dist = Math.Sqrt(
                    Math.Pow((pr.FromAirport.MapX - nearby.MapX) * 5000, 2) +
                    Math.Pow((pr.FromAirport.MapY - nearby.MapY) * 3500, 2));
                decimal cost = (decimal)dist * 150;
                var txt = new FormattedText(
                    $"预计费用 ¥{cost:N0}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, LabelFont, 11,
                    new SolidColorBrush(Color.FromRgb(255, 193, 7)), Dpi);
                dc.DrawText(txt, new Point(pr.MouseX + 12, pr.MouseY - 18));
            }
        }

        private void DrawAircraft(DrawingContext dc)
        {
            if (VM == null) return;
            foreach (var ac in VM.Fleet.Where(a => a.IsFlying))
            {
                double px = ac.MapX * ActualWidth;
                double py = ac.MapY * ActualHeight;

                dc.PushTransform(new RotateTransform(ac.HeadingDegrees, px, py));

                var geo = new StreamGeometry();
                using (var sgc = geo.Open())
                {
                    sgc.BeginFigure(new Point(px + 8, py), true, true);
                    sgc.LineTo(new Point(px - 4, py + 4), true, false);
                    sgc.LineTo(new Point(px - 2, py), true, false);
                    sgc.LineTo(new Point(px - 4, py - 4), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(AircraftBrush, null, geo);
                dc.Pop();

                var label = new FormattedText(
                    $"{ac.Registration}  {(int)(ac.ProgressRatio * 100)}%",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, MonoFont, 9,
                    new SolidColorBrush(Color.FromArgb(180, 100, 200, 255)), Dpi);
                dc.DrawText(label, new Point(px + 10, py - 14));
            }
        }

        private void DrawAirports(DrawingContext dc)
        {
            if (VM == null) return;

            foreach (var airport in VM.Airports)
            {
                var pos = VM.AirportToCanvas(airport);
                bool sel = airport == VM.SelectedAirport;
                bool hov = airport == VM.HoveredAirport;
                double r = sel ? 10 : (hov ? 9 : 7);

                dc.DrawEllipse(null,
                    airport.IsAlarming ? AlarmRingPen : AirportRingPen,
                    pos, r + 5, r + 5);

                Brush dot = airport.UrgencyLevel switch
                {
                    UrgencyLevel.Critical => DotRed,
                    UrgencyLevel.High => DotAmber,
                    UrgencyLevel.Medium => DotGreen,
                    _ => DotBlue,
                };
                dc.DrawEllipse(dot, null, pos, r, r);

                if (sel)
                    dc.DrawEllipse(null,
                        new Pen(new SolidColorBrush(Color.FromArgb(200, 43, 127, 255)), 2),
                        pos, r + 9, r + 9);

                var codeText = new FormattedText(
                    airport.Code,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, LabelFont, 11,
                    new SolidColorBrush(Color.FromRgb(220, 232, 255)), Dpi);
                dc.DrawText(codeText, new Point(pos.X - codeText.Width / 2, pos.Y + r + 3));

                var paxText = new FormattedText(
                    $"{airport.CurrentPassengers}/{airport.MaxCapacity}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, MonoFont, 9,
                    new SolidColorBrush(Color.FromArgb(160, 96, 120, 152)), Dpi);
                dc.DrawText(paxText, new Point(pos.X - paxText.Width / 2, pos.Y + r + 16));

                if (airport.IsAlarming)
                {
                    var alarm = new FormattedText(
                        $"⚠ {(int)airport.AlarmTimeRemaining}s",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, LabelFont, 11,
                        new SolidColorBrush(Color.FromRgb(255, 61, 61)), Dpi);
                    dc.DrawText(alarm, new Point(pos.X - alarm.Width / 2, pos.Y - r - 22));
                }
            }
        }
    }
}