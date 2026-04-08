using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using AirlineEmpire.Models;
using AirlineEmpire.Services;

namespace AirlineEmpire.ViewModels
{
    public partial class MapViewModel : ObservableObject
    {
        private readonly GameStateService _gs;

        // 改成 event Action 而非字段式 event，外部只能 += / -=
        public event Action<Airport?>? OnAirportSelected;
        public event Action<string>?   OnMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDrawingRoute))]
        private PendingRoute? _pendingRoute;

        public bool IsDrawingRoute => PendingRoute != null;

        [ObservableProperty] private Airport? _hoveredAirport;
        [ObservableProperty] private Airport? _selectedAirport;

        public double CanvasWidth  { get; set; } = 1;
        public double CanvasHeight { get; set; } = 1;

        public MapViewModel(GameStateService gs) => _gs = gs;

        public IReadOnlyList<Airport>  Airports => _gs.State.Airports;
        public IReadOnlyList<Route>    Routes   => _gs.State.Routes;
        public IReadOnlyList<Aircraft> Fleet    => _gs.State.Fleet;

        public Point AirportToCanvas(Airport a)
            => new Point(a.MapX * CanvasWidth, a.MapY * CanvasHeight);

        public Airport? HitTest(Point pos, double radius = 20)
        {
            double rx = pos.X / CanvasWidth;
            double ry = pos.Y / CanvasHeight;
            double rr = radius / Math.Max(CanvasWidth, CanvasHeight);

            Airport? best = null;
            double bestDist = double.MaxValue;
            foreach (var a in _gs.State.Airports)
            {
                double d = Math.Sqrt(Math.Pow(a.MapX - rx, 2) + Math.Pow(a.MapY - ry, 2));
                if (d < rr && d < bestDist) { bestDist = d; best = a; }
            }
            return best;
        }

        // 供 View 的 code-behind 调用
        public void RaiseAirportSelected(Airport? a) => OnAirportSelected?.Invoke(a);
        public void RaiseMessage(string msg)         => OnMessage?.Invoke(msg);

        public void OnCanvasLeftClick(Point pos)
        {
            var hit = HitTest(pos);

            if (IsDrawingRoute)
            {
                if (hit != null && hit != PendingRoute!.FromAirport)
                {
                    var (ok, msg) = _gs.TryOpenRoute(PendingRoute.FromAirport, hit);
                    RaiseMessage(ok ? $"✅ {msg}" : $"❌ {msg}");
                    PendingRoute = null;
                }
                return;
            }

            if (hit != null)
            {
                if (SelectedAirport == hit)
                {
                    // 二次点击同机场：进入连线模式
                    PendingRoute = new PendingRoute
                    {
                        FromAirport = hit,
                        MouseX      = pos.X,
                        MouseY      = pos.Y,
                    };
                    RaiseMessage($"🔗 从 {hit.Code} 开始连线 — 右键取消，点击目标机场完成");
                }
                else
                {
                    SelectedAirport = hit;
                    RaiseAirportSelected(hit);
                }
            }
            else
            {
                SelectedAirport = null;
                RaiseAirportSelected(null);
            }
        }

        public void OnCanvasRightClick(Point pos)
        {
            if (IsDrawingRoute)
            {
                PendingRoute = null;
                RaiseMessage("✗ 连线已取消");
            }
        }

        public void OnCanvasMouseMove(Point pos)
        {
            if (PendingRoute != null)
            {
                PendingRoute.MouseX = pos.X;
                PendingRoute.MouseY = pos.Y;
                OnPropertyChanged(nameof(PendingRoute));
            }
            HoveredAirport = HitTest(pos, 18);
        }
    }
}
