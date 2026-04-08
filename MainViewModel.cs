using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineEmpire.Models;
using AirlineEmpire.Converters;
using AirlineEmpire.Services;

namespace AirlineEmpire.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly GameStateService _gs;
        private readonly SimulationEngine _engine;
        private readonly ExportService _export;

        public GameState State => _gs.State;

        public MapViewModel MapVM { get; }
        public RankingViewModel RankingVM { get; }

        public ObservableCollection<LogEntry> Logs { get; } = new ObservableCollection<LogEntry>();

        [ObservableProperty] private string _gameTimeText = "00:00:00";
        [ObservableProperty] private double _currentTimeScale = 1.0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAirportPanelVisible))]
        private Airport? _selectedAirport;

        public bool IsAirportPanelVisible => SelectedAirport != null;

        [ObservableProperty] private bool _isBuyAircraftPanelOpen;
        [ObservableProperty] private bool _isGameOverVisible;

        public List<AircraftTypeConfig> AvailableTypes
            => Aircraft.TypeConfigs.Values.ToList();

        public MainViewModel(
            GameStateService gs,
            SimulationEngine engine,
            ExportService export,
            MapViewModel mapVM,
            RankingViewModel rankingVM)
        {
            _gs = gs;
            _engine = engine;
            _export = export;
            MapVM = mapVM;
            RankingVM = rankingVM;

            _engine.OnLog += AddLog;
            _engine.OnTick += OnTick;

            // 使用 RaiseXxx 方法订阅，不直接赋值 event
            MapVM.OnAirportSelected += a => SelectedAirport = a;

            _engine.Start();
        }

        private void OnTick()
        {
            RankingVM.Refresh();
            var t = TimeSpan.FromSeconds(_gs.State.GameTimeSeconds);
            GameTimeText = $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            if (_gs.State.IsGameOver && !IsGameOverVisible)
                IsGameOverVisible = true;
        }

        [RelayCommand] private void TogglePause() => _engine.TogglePause();

        [RelayCommand]
        private void SetSpeed(string speedStr)
        {
            if (double.TryParse(speedStr, out double s))
            {
                CurrentTimeScale = s;
                _engine.SetTimeScale(s);
            }
        }

        [RelayCommand] private void CloseAirportPanel() => SelectedAirport = null;

        [RelayCommand]
        private void UpgradeAirport()
        {
            if (SelectedAirport == null) return;
            var (ok, msg) = _gs.TryUpgradeAirport(SelectedAirport);
            AddLog(ok ? $"⬆ {msg}" : $"✗ {msg}");
        }

        [RelayCommand]
        private void OpenBuyAircraft()
        {
            if (SelectedAirport != null) IsBuyAircraftPanelOpen = true;
        }

        [RelayCommand]
        private void BuyAircraft(string typeCode)
        {
            if (SelectedAirport == null) return;
            var (ok, msg) = _gs.TryPurchaseAircraft(SelectedAirport, typeCode);
            AddLog(ok ? $"🛩 {msg}" : $"✗ {msg}");
            IsBuyAircraftPanelOpen = false;
        }

        [RelayCommand]
        private void ExportExcel()
        {
            try { var p = _export.ExportToExcel(_gs.Config.PlayerName); AddLog($"📊 已保存：{p}"); }
            catch (Exception ex) { AddLog($"✗ 导出失败：{ex.Message}"); }
        }

        [RelayCommand]
        private void ExportCsv()
        {
            try { var p = _export.ExportToCsv(); AddLog($"📄 CSV已保存：{p}"); }
            catch (Exception ex) { AddLog($"✗ 导出失败：{ex.Message}"); }
        }

        private void AddLog(string msg)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Logs.Insert(0, new LogEntry(
                    DateTime.Now.ToString("HH:mm:ss"), msg,
                    msg.StartsWith("⚠") || msg.StartsWith("💀") ? LogLevel.Alert :
                    msg.StartsWith("✗") ? LogLevel.Error : LogLevel.Info));
                while (Logs.Count > 100) Logs.RemoveAt(Logs.Count - 1);
            });
        }
    }
}