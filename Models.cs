using CommunityToolkit.Mvvm.ComponentModel;

namespace AirlineEmpire.Models
{

    // ═══════════════════════════════════════════════════════
    //  ROUTE — 两机场之间的航线
    // ═══════════════════════════════════════════════════════
    public partial class Route : ObservableObject
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid AirportAId { get; init; }
        public Guid AirportBId { get; init; }
        public double DistanceKm { get; init; }

        // 开通费用 = 距离 * 150元/km（基础）
        public decimal OpeningCost => (decimal)(DistanceKm * 150);

        [ObservableProperty] private bool _isActive;
        [ObservableProperty] private int _totalFlightsOperated;
        [ObservableProperty] private decimal _totalRevenue;
        [ObservableProperty] private decimal _totalCost;

        public decimal Profit => TotalRevenue - TotalCost;

        /// <summary>获取另一端机场Id</summary>
        public Guid OtherAirport(Guid fromId)
            => fromId == AirportAId ? AirportBId : AirportAId;
    }

    // ═══════════════════════════════════════════════════════
    //  PASSENGER — 单个旅客
    // ═══════════════════════════════════════════════════════
    public class Passenger
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid OriginAirportId { get; init; }
        public Guid DestinationAirportId { get; init; }
        public double WaitingSeconds { get; set; }   // 已等待时间
        public bool IsAngry => WaitingSeconds > 1800; // 等超30分钟=愤怒
        public bool HasBoarded { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  FLIGHT — 一次飞行任务
    // ═══════════════════════════════════════════════════════
    public partial class Flight : ObservableObject
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Aircraft Aircraft { get; init; } = null!;
        public Route Route { get; init; } = null!;
        public Guid FromId { get; init; }
        public Guid ToId { get; init; }

        [ObservableProperty] private FlightStatus _status = FlightStatus.Boarding;
        [ObservableProperty] private double _progressRatio;   // 0-1
        [ObservableProperty] private double _elapsedSeconds;

        public double TotalDurationSeconds { get; init; }           // 预计飞行时长(游戏秒)
        public int PassengerCount => Aircraft.OnboardPassengers.Count;

        // 经济
        public decimal TicketRevenue { get; set; }
        public decimal FuelCost { get; set; }
        public decimal NetProfit => TicketRevenue - FuelCost;
    }

    public enum FlightStatus { Boarding, InFlight, Arrived, Cancelled }

    // ═══════════════════════════════════════════════════════
    //  PENDING ROUTE — 正在连线中（未完成的航线操作）
    // ═══════════════════════════════════════════════════════
    public class PendingRoute
    {
        public Airport FromAirport { get; init; } = null!;
        public double MouseX { get; set; }   // 当前鼠标位置（画布坐标）
        public double MouseY { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  GAME CONFIG — 游戏初始配置
    // ═══════════════════════════════════════════════════════
    public class GameConfig
    {
        public GameMode Mode { get; set; } = GameMode.Single;
        public decimal StartingFunds { get; set; } = 500_000m;
        public double TimeScale { get; set; } = 60.0;  // 1秒现实=60秒游戏
        public int AlarmGraceSecs { get; set; } = 60;    // 满员报警宽限时间
        public string? RoomId { get; set; }
        public string? RoomPassword { get; set; }
        public string PlayerName { get; set; } = "玩家";
    }

    public enum GameMode { Single, MultiPlayer }

    // ═══════════════════════════════════════════════════════
    //  GAME STATE — 全局状态快照（也用于多人同步）
    // ═══════════════════════════════════════════════════════
    public partial class GameState : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FundsText))]
        private decimal _funds;

        [ObservableProperty] private double _gameTimeSeconds;   // 游戏内已过时间(秒)
        [ObservableProperty] private bool _isPaused;
        [ObservableProperty] private bool _isGameOver;
        [ObservableProperty] private string _gameOverReason = "";

        public string FundsText => $"¥ {Funds:N0}";

        // 主要集合
        public List<Airport> Airports { get; } = new();
        public List<Aircraft> Fleet { get; } = new();
        public List<Route> Routes { get; } = new();
        public List<Flight> Flights { get; } = new();

        // 统计
        public int TotalOps { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => TotalRevenue - TotalExpenses;

        public bool SpendMoney(decimal amount)
        {
            if (Funds < amount) return false;
            Funds -= amount;
            TotalExpenses += amount;
            return true;
        }

        public void EarnMoney(decimal amount)
        {
            Funds += amount;
            TotalRevenue += amount;
        }

        // ─── Log ─────────────────────────────────────────────
        public enum LogLevel { Info, Alert, Error }
        public record LogEntry(string Time, string Message, LogLevel Level);
    }
}