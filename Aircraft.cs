using CommunityToolkit.Mvvm.ComponentModel;

namespace AirlineEmpire.Models
{

public record AircraftTypeConfig(
    string TypeCode,       // B738, A320...
    string DisplayName,
    int    Seats,
    decimal PurchaseCost,
    double  SpeedKmH,
    double  FuelBurnPerKm, // 升/公里
    double  FuelCostPerL,  // 元/升
    int     MaxAgeYears,   // 报废年限（游戏内年）
    double  WearPerFlight  // 每次起降磨损值 (0-1)
);

public partial class Aircraft : ObservableObject
{
    // ── 机型配置库 ────────────────────────────────────────
    public static readonly Dictionary<string, AircraftTypeConfig> TypeConfigs = new()
    {
        ["B738"] = new("B738", "波音 737-800",    189, 120_000m, 842, 3.2, 6.5, 25, 0.008),
        ["A320"] = new("A320", "空客 A320",        180, 115_000m, 833, 3.0, 6.5, 25, 0.008),
        ["B77W"] = new("B77W", "波音 777-300ER",   396, 380_000m, 905, 7.8, 6.5, 30, 0.006),
        ["A321"] = new("A321", "空客 A321",        220, 150_000m, 833, 3.8, 6.5, 26, 0.007),
        ["B787"] = new("B787", "波音 787-9",       296, 280_000m, 903, 5.5, 6.5, 30, 0.006),
        ["ATR7"] = new("ATR7", "ATR 72-600",        70,  35_000m, 510, 1.1, 6.5, 20, 0.012),
    };

    // ── 标识 ──────────────────────────────────────────────
    public Guid   Id           { get; init; } = Guid.NewGuid();
    public string Registration { get; init; } = "";   // B-1234
    public string TypeCode     { get; init; } = "B738";
    public AircraftTypeConfig Config => TypeConfigs[TypeCode];

    // ── 位置与状态 ────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFlying))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private AircraftStatus _status = AircraftStatus.Idle;

    [ObservableProperty] private Guid?   _currentAirportId;   // 当前停靠机场 null=飞行中
    [ObservableProperty] private Guid?   _destinationAirportId;
    [ObservableProperty] private double  _mapX;               // 实时地图坐标
    [ObservableProperty] private double  _mapY;
    [ObservableProperty] private double  _progressRatio;      // 飞行进度 0-1
    [ObservableProperty] private double  _headingDegrees;     // 机头朝向

    public bool IsFlying => Status == AircraftStatus.Flying;

    // ── 老化与磨损 ────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WearPercent))]
    [NotifyPropertyChangedFor(nameof(IsScraped))]
    [NotifyPropertyChangedFor(nameof(WearLevel))]
    private double _wearValue;      // 0.0 ~ 1.0

    public double WearPercent => WearValue * 100;
    public bool   IsScraped   => WearValue >= 1.0;
    public WearLevel WearLevel => WearValue switch
    {
        >= 1.0 => WearLevel.Scrapped,
        >= 0.8 => WearLevel.Critical,
        >= 0.5 => WearLevel.Warning,
        _      => WearLevel.Good,
    };

    [ObservableProperty] private int    _totalFlights;
    [ObservableProperty] private int    _totalPassengersCarried;
    [ObservableProperty] private decimal _totalRevenue;

    // ── 当前航班信息 ──────────────────────────────────────
    public List<Passenger> OnboardPassengers { get; } = new();
    public int CurrentLoad => OnboardPassengers.Count;
    public int Seats       => Config.Seats;

    // ── 维修 ──────────────────────────────────────────────
    [ObservableProperty] private bool    _isUnderMaintenance;
    [ObservableProperty] private double  _maintenanceTimeRemaining; // 秒

    public decimal MaintenanceCost => (decimal)(WearValue * (double)Config.PurchaseCost * 0.3);

    // ── 状态文本 ──────────────────────────────────────────
    public string StatusText => Status switch
    {
        AircraftStatus.Idle        => "待命",
        AircraftStatus.Flying      => $"飞行中 {(int)(ProgressRatio * 100)}%",
        AircraftStatus.Boarding    => "登机中",
        AircraftStatus.Maintenance => "维修中",
        AircraftStatus.Scrapped    => "已报废",
        _ => "未知"
    };

    // ── 方法 ──────────────────────────────────────────────
    public void ApplyWear()
    {
        WearValue = Math.Min(1.0, WearValue + Config.WearPerFlight);
        TotalFlights++;
    }

    public decimal CalcFuelCost(double distanceKm)
    {
        return (decimal)(distanceKm * Config.FuelBurnPerKm * Config.FuelCostPerL);
    }

    public decimal CalcTicketRevenue(double distanceKm)
    {
        // 基础票价: 0.8元/公里/人，远程适当溢价
        double baseRate = distanceKm > 1500 ? 1.1 : 0.8;
        return (decimal)(distanceKm * baseRate) * OnboardPassengers.Count;
    }

    public override string ToString() => $"{Registration} ({Config.DisplayName}) - {StatusText}";
}

public enum AircraftStatus { Idle, Boarding, Flying, Maintenance, Scrapped }
public enum WearLevel      { Good, Warning, Critical, Scrapped }
}
