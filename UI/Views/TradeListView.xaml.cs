// مسیر فایل: UI/Views/TradeListView.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TradingJournal.Core.Commands;
using TradingJournal.Data.Models;
using TradingJournal.Data.Repositories;

namespace TradingJournal.UI.Views
{
    public partial class TradeListView : UserControl
    {
        private readonly ITradeRepository _tradeRepository;
        public ObservableCollection<Trade> Trades { get; set; }
        public ObservableCollection<Trade> FilteredTrades { get; set; }

        // Commands
        public ICommand EditTradeCommand { get; set; }
        public ICommand DeleteTradeCommand { get; set; }
        public ICommand ViewDetailsCommand { get; set; }
        public ICommand ExportCommand { get; set; }
        public ICommand RefreshCommand { get; set; }

        public TradeListView()
        {
            InitializeComponent();
            
            Trades = new ObservableCollection<Trade>();
            FilteredTrades = new ObservableCollection<Trade>();
            
            DataContext = this;
            InitializeCommands();
            LoadTrades();
        }

        private void InitializeCommands()
        {
            EditTradeCommand = new RelayCommand<Trade>(ExecuteEditTrade);
            DeleteTradeCommand = new RelayCommand<Trade>(ExecuteDeleteTrade);
            ViewDetailsCommand = new RelayCommand<Trade>(ExecuteViewDetails);
            ExportCommand = new RelayCommand(ExecuteExport);
            RefreshCommand = new RelayCommand(ExecuteRefresh);
        }

        private async void LoadTrades()
        {
            try
            {
                ShowProgress(true);
                
                var trades = await _tradeRepository.GetAllAsync();
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Trades.Clear();
                    FilteredTrades.Clear();
                    
                    foreach (var trade in trades)
                    {
                        Trades.Add(trade);
                        FilteredTrades.Add(trade);
                    }
                });
                
                UpdateStatistics();
                ShowProgress(false);
            }
            catch (Exception ex)
            {
                ShowProgress(false);
                MessageBox.Show($"خطا در بارگذاری معاملات: {ex.Message}");
            }
        }

        private void ExecuteEditTrade(Trade trade)
        {
            if (trade == null) return;
            
            var dialog = new EditTradeDialog(trade);
            if (dialog.ShowDialog() == true)
            {
                LoadTrades();
            }
        }

        private async void ExecuteDeleteTrade(Trade trade)
        {
            if (trade == null) return;
            
            var result = MessageBox.Show(
                $"آیا از حذف معامله {trade.Symbol} مطمئن هستید؟",
                "تأیید حذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                await _tradeRepository.DeleteAsync(trade.Id);
                LoadTrades();
            }
        }

        private void ExecuteViewDetails(Trade trade)
        {
            if (trade == null) return;
            
            var dialog = new TradeDetailsDialog(trade);
            dialog.ShowDialog();
        }

        private void ExecuteExport(object parameter)
        {
            // Export logic
        }

        private void ExecuteRefresh(object parameter)
        {
            LoadTrades();
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTrades();
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterTrades();
        }

        private void FilterTrades()
        {
            var searchText = SearchTextBox.Text?.ToLower() ?? "";
            var selectedFilter = (FilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            
            var filtered = Trades.AsEnumerable();
            
            // Apply text search
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(t => 
                    t.Symbol.ToLower().Contains(searchText) ||
                    t.Strategy?.ToLower().Contains(searchText) == true ||
                    t.Notes?.ToLower().Contains(searchText) == true);
            }
            
            // Apply filter
            switch (selectedFilter)
            {
                case "Open":
                    filtered = filtered.Where(t => t.IsOpen);
                    break;
                case "Closed":
                    filtered = filtered.Where(t => !t.IsOpen);
                    break;
                case "Profit":
                    filtered = filtered.Where(t => t.IsWin);
                    break;
                case "Loss":
                    filtered = filtered.Where(t => !t.IsWin && t.Profit.HasValue);
                    break;
                case "Today":
                    filtered = filtered.Where(t => t.EntryDate.Date == DateTime.Today);
                    break;
                case "Week":
                    var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    filtered = filtered.Where(t => t.EntryDate >= weekStart);
                    break;
                case "Month":
                    filtered = filtered.Where(t => t.EntryDate.Month == DateTime.Now.Month);
                    break;
            }
            
            // Update filtered collection
            FilteredTrades.Clear();
            foreach (var trade in filtered)
            {
                FilteredTrades.Add(trade);
            }
            
            UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            var totalTrades = FilteredTrades.Count;
            var openTrades = FilteredTrades.Count(t => t.IsOpen);
            var winningTrades = FilteredTrades.Count(t => t.IsWin);
            var losingTrades = FilteredTrades.Count(t => !t.IsWin && t.Profit.HasValue);
            var totalProfit = FilteredTrades.Sum(t => t.Profit ?? 0);
            var winRate = totalTrades > 0 ? (double)winningTrades / totalTrades * 100 : 0;
            
            TotalTradesText.Text = totalTrades.ToString();
            OpenTradesText.Text = openTrades.ToString();
            WinningTradesText.Text = winningTrades.ToString();
            LosingTradesText.Text = losingTrades.ToString();
            TotalProfitText.Text = totalProfit.ToString("N0");
            WinRateText.Text = $"{winRate:F1}%";
            
            // Set profit color
            TotalProfitText.Foreground = totalProfit >= 0 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
        }

        private void ShowProgress(bool show)
        {
            ProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
// پایان کد