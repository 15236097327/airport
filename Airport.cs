using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AirlineEmpire.Models
{

/// <summary>
/// 机场等级配置
/// </summary>
public record AirportLevelConfig(
    int Level,
    int MaxCapacity,       // 最大旅客容纳量
    int MaxAircraftSlots,  // 停机位数量
    decimal UpgradeCost,   // 升级费用
    string LevelName       // 等级名称
);

/// <summary>
/// 机场实体 - 地图上的核心节点
/// </summary>
public partial class Airport : ObservableObject
{
    // ── 静态配置 ──────────────────────────────────────────
    public static readonly AirportLevelConfig[] LevelConfigs =
    [
        new(1, 50,  2, 0,       "小型机场"),
        new(2, 120, 4, 80_000m, "区域机场"),
        new(3, 300, 6, 200_000m,"国内枢纽"),
        new(4, 600, 8, 500_000m,"国际枢纽"),
        new(5, 1200,12,1_200_000m,"超级枢纽"),
    ];

    // ── 基本信息 ──────────────────────────────────────────
    public Guid   Id       { get; init; } = Guid.NewGuid();
    public string Code     { get; init; } = "";   // IATA: PEK, SHA...
    public string Name     { get; init; } = "";   // 首都国际机场
    public string City     { get; init; } = "";
    public double MapX     { get; init; }          // 地图坐标 (0-1 归一化)
    public double MapY     { get; init; }

    // ── 等级与容量 ────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxCapacity))]
    [NotifyPropertyChangedFor(nameof(MaxAircraftSlots))]
    [NotifyPropertyChangedFor(nameof(UpgradeCost))]
    [NotifyPropertyChangedFor(nameof(CanUpgrade))]
    [NotifyPropertyChangedFor(nameof(LevelName))]
    private int _level = 1;

    public int     MaxCapacity     => LevelConfigs[Level - 1].MaxCapacity;
    public int     MaxAircraftSlots=> LevelConfigs[Level - 1].MaxAircraftSlots;
    public decimal UpgradeCost     => Level < 5 ? LevelConfigs[Level].UpgradeCost : 0;
    public bool    CanUpgrade      => Level < 5;
    public string  LevelName       => LevelConfigs[Level - 1].LevelName;

    // ── 动态状态 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UrgencyRatio))]
    [NotifyPropertyChangedFor(nameof(UrgencyLevel))]
    private int _currentPassengers;

    /// <summary>旅客按目的地分组 Key=目的地机场Id</summary>
    public Dictionary<Guid, Queue<Passenger>> PassengerGroups { get; } = new();

    /// <summary>当前停在本机场的飞机</summary>
    public ObservableCollection<Aircraft> StationedAircraft { get; } = new();

    // ── 统计数据（游戏结算用） ────────────────────────────
    public int     TotalDeparted     { get; set; }  // 累计出发旅客
    public int     TotalArrived      { get; set; }  // 累计抵达旅客
    public decimal TotalRevenue      { get; set; }  // 累计收益
    public int     HistoricalMaxPax  { get; set; }  // 历史最大滞留量

    // ── 报警状态 ──────────────────────────────────────────
    [ObservableProperty] private bool   _isAlarming;
    [ObservableProperty] private double _alarmTimeRemaining; // 秒

    // ── 计算属性 ──────────────────────────────────────────
    public double UrgencyRatio => MaxCapacity > 0
        ? (double)CurrentPassengers / MaxCapacity
        : 0;

    public UrgencyLevel UrgencyLevel => UrgencyRatio switch
    {
        >= 1.0 => UrgencyLevel.Critical,
        >= 0.8 => UrgencyLevel.High,
        >= 0.5 => UrgencyLevel.Medium,
        _      => UrgencyLevel.Low,
    };

    public bool IsFull => CurrentPassengers >= MaxCapacity;

    // ── 方法 ──────────────────────────────────────────────
    public void AddPassengers(IEnumerable<Passenger> passengers)
    {
        foreach (var p in passengers)
        {
            if (!PassengerGroups.ContainsKey(p.DestinationAirportId))
                PassengerGroups[p.DestinationAirportId] = new Queue<Passenger>();

            PassengerGroups[p.DestinationAirportId].Enqueue(p);
            CurrentPassengers++;
        }

        if (CurrentPassengers > HistoricalMaxPax)
            HistoricalMaxPax = CurrentPassengers;
    }

    /// <summary>
    /// 尝试装载旅客到飞机（同目的地优先，按座位数量装载）
    /// </summary>
    public List<Passenger> BoardPassengers(Guid destinationId, int seats)
    {
        var boarded = new List<Passenger>();
        if (!PassengerGroups.TryGetValue(destinationId, out var queue))
            return boarded;

        while (boarded.Count < seats && queue.Count > 0)
        {
            var p = queue.Dequeue();
            boarded.Add(p);
            CurrentPassengers--;
        }

        if (queue.Count == 0)
            PassengerGroups.Remove(destinationId);

        return boarded;
    }

    public void LevelUp() { if (CanUpgrade) Level++; }

    public override string ToString() => $"[{Code}] {Name} Lv{Level} ({CurrentPassengers}/{MaxCapacity})";
}

public enum UrgencyLevel { Low, Medium, High, Critical }
}
