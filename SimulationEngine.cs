using System.Windows.Threading;
using AirlineEmpire.Models;

namespace AirlineEmpire.Services
{

/// <summary>
/// 游戏主循环 — 驱动所有子系统，每帧推进游戏状态
/// </summary>
public class SimulationEngine
{
    private readonly GameStateService  _gs;
    private readonly PassengerSpawner  _spawner;
    private readonly FlightDispatcher  _dispatcher;
    private readonly ConflictMonitor   _monitor;

    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;

    // 60fps 物理帧 + 每N帧触发一次慢速逻辑
    private const int TARGET_FPS       = 60;
    private const int SPAWN_INTERVAL   = 180;   // 每3秒(游戏)生成旅客
    private const int DISPATCH_INTERVAL= 60;    // 每1秒检查派遣

    private int _frameCtr;

    public event Action<string>? OnLog;         // 日志事件（UI订阅）
    public event Action?         OnTick;        // 每帧通知（UI刷新）

    public SimulationEngine(
        GameStateService gs,
        PassengerSpawner spawner,
        FlightDispatcher dispatcher,
        ConflictMonitor  monitor)
    {
        _gs         = gs;
        _spawner    = spawner;
        _dispatcher = dispatcher;
        _monitor    = monitor;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / TARGET_FPS)
        };
        _timer.Tick += OnTimerTick;
    }

    public bool IsRunning => _timer.IsEnabled;

    public void Start()
    {
        _lastTick = DateTime.Now;
        _timer.Start();
        Log("▸ 模拟引擎启动");
    }

    public void Stop()
    {
        _timer.Stop();
        Log("▸ 模拟引擎暂停");
    }

    public void TogglePause()
    {
        _gs.State.IsPaused = !_gs.State.IsPaused;
        Log(_gs.State.IsPaused ? "⏸ 游戏已暂停" : "▶ 游戏继续");
    }

    public void SetTimeScale(double scale)
    {
        _gs.Config.TimeScale = scale;
        Log($"▸ 时间倍速 ×{scale}");
    }

    // ── 主循环 ────────────────────────────────────────────
    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_gs.State.IsGameOver) { _timer.Stop(); return; }
        if (_gs.State.IsPaused)   return;

        var now = DateTime.Now;
        double realDt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        // 游戏内 delta time（秒）= 现实时间 × 时间倍速
        double gameDt = realDt * _gs.Config.TimeScale;
        _gs.State.GameTimeSeconds += gameDt;

        _frameCtr++;

        // 每帧：飞机位置插值
        TickAircraftMovement(gameDt);

        // 每秒：派遣检查
        if (_frameCtr % DISPATCH_INTERVAL == 0)
            _dispatcher.Tick(gameDt * DISPATCH_INTERVAL, Log);

        // 每3秒：旅客生成
        if (_frameCtr % SPAWN_INTERVAL == 0)
            _spawner.SpawnPassengers(gameDt * SPAWN_INTERVAL);

        // 每帧：冲突/报警检查
        _monitor.Tick(gameDt, Log, OnGameOver);

        OnTick?.Invoke();
    }

    // ── 飞机移动插值 ──────────────────────────────────────
    private void TickAircraftMovement(double gameDt)
    {
        foreach (var flight in _gs.State.Flights.ToList())
        {
            if (flight.Status != FlightStatus.InFlight) continue;

            flight.ElapsedSeconds += gameDt;
            flight.ProgressRatio   = Math.Min(1.0,
                flight.ElapsedSeconds / flight.TotalDurationSeconds);

            // 插值飞机位置
            var from = _gs.GetAirport(flight.FromId)!;
            var to   = _gs.GetAirport(flight.ToId)!;
            var ac   = flight.Aircraft;

            ac.MapX = from.MapX + (to.MapX - from.MapX) * flight.ProgressRatio;
            ac.MapY = from.MapY + (to.MapY - from.MapY) * flight.ProgressRatio;
            ac.ProgressRatio = flight.ProgressRatio;

            // 计算朝向
            double dx = to.MapX - from.MapX;
            double dy = to.MapY - from.MapY;
            ac.HeadingDegrees = Math.Atan2(dy, dx) * 180 / Math.PI;

            // 到达判断
            if (flight.ProgressRatio >= 1.0)
                _dispatcher.HandleArrival(flight, Log);
        }
    }

    private void OnGameOver(string reason)
    {
        _gs.State.IsGameOver    = true;
        _gs.State.GameOverReason= reason;
        Log($"💀 游戏结束 — {reason}");
        _timer.Stop();
    }

    private void Log(string msg) => OnLog?.Invoke(msg);
}
}
