using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineEmpire.Models;
using AirlineEmpire.Services;

namespace AirlineEmpire.ViewModels
{
    // ═══════════════════════════════════════════════════════
    //  RANKING VIEW MODEL
    // ═══════════════════════════════════════════════════════
    public partial class RankingViewModel : ObservableObject
    {
        private readonly GameStateService _gs;
        public event Action<Airport>? OnJumpToAirport;
        public ObservableCollection<RankingItem> Items { get; } = new ObservableCollection<RankingItem>();

        public RankingViewModel(GameStateService gs) => _gs = gs;

        public void Refresh()
        {
            var sorted = _gs.State.Airports
                .OrderByDescending(a => a.UrgencyRatio)
                .Take(10)
                .ToList();

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Items.Clear();
                for (int i = 0; i < sorted.Count; i++)
                {
                    var a = sorted[i];
                    Items.Add(new RankingItem(i + 1, a, a.UrgencyRatio,
                        a.CurrentPassengers, a.MaxCapacity, a.UrgencyLevel, a.IsAlarming));
                }
            });
        }

        [RelayCommand]
        private void JumpTo(RankingItem item) => OnJumpToAirport?.Invoke(item.Airport);
    }

    public record RankingItem(
        int Rank, Airport Airport, double Ratio,
        int Current, int Max, UrgencyLevel Level, bool IsAlarming);

    // ═══════════════════════════════════════════════════════
    //  LOBBY VIEW MODEL
    // ═══════════════════════════════════════════════════════
    public partial class LobbyViewModel : ObservableObject
    {
        private readonly GameStateService _gs;

        [ObservableProperty] private string  _playerName   = "玩家1";
        [ObservableProperty] private decimal _startFunds   = 500_000m;
        [ObservableProperty] private string  _roomId       = "";
        [ObservableProperty] private string  _roomPassword = "";
        [ObservableProperty] private string  _statusMsg    = "";

        // 用 bool 代替枚举，避免 XAML 引用枚举类型
        [ObservableProperty] private bool _isMultiPlayer;

        // IsSingleSelected 不能用 NotifyPropertyChangedFor 指向自动生成属性
        // 改成手动在 partial 方法里通知
        public bool IsSingleSelected => !IsMultiPlayer;

        partial void OnIsMultiPlayerChanged(bool value)
        {
            OnPropertyChanged(nameof(IsSingleSelected));
        }

        public event Action<GameConfig>? OnStartGame;

        public LobbyViewModel(GameStateService gs) => _gs = gs;

        [RelayCommand]
        private void StartSinglePlayer()
        {
            if (string.IsNullOrWhiteSpace(PlayerName)) PlayerName = "玩家1";
            var cfg = new GameConfig
            {
                Mode          = GameMode.Single,
                PlayerName    = PlayerName,
                StartingFunds = StartFunds,
                TimeScale     = 60.0,
            };
            _gs.InitNewGame(cfg);
            OnStartGame?.Invoke(cfg);
        }

        [RelayCommand]
        private async Task CreateRoom()
        {
            StatusMsg = "多人功能开发中...";
            await Task.Delay(500);
        }

        [RelayCommand]
        private async Task JoinRoom()
        {
            StatusMsg = "多人功能开发中...";
            await Task.Delay(500);
        }
    }
}
