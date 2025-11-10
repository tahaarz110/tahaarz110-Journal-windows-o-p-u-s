// مسیر فایل: UI/Dialogs/NewTradeDialog.cs
// ابتدای کد
using System;
using System.Windows;
using TradingJournal.Data.Models;
using TradingJournal.Data.Repositories;

namespace TradingJournal.UI.Dialogs
{
    public partial class NewTradeDialog : Window
    {
        private readonly ITradeRepository _tradeRepository;
        public Trade Trade { get; private set; }

        public NewTradeDialog(ITradeRepository tradeRepository)
        {
            InitializeComponent();
            _tradeRepository = tradeRepository;
            Trade = new Trade
            {
                EntryDate = DateTime.Now,
                Type = TradeType.Buy
            };
            DataContext = Trade;
        }

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate
                if (string.IsNullOrEmpty(Trade.Symbol))
                {
                    MessageBox.Show("نماد اجباری است", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Calculate derived fields
                if (Trade.ExitPrice.HasValue && Trade.EntryPrice > 0)
                {
                    var priceDiff = Trade.ExitPrice.Value - Trade.EntryPrice;
                    Trade.ProfitPips = (int)(priceDiff * 10000); // For forex
                    Trade.Profit = priceDiff * Trade.Volume * 100000; // Standard lot calculation
                }

                // Save
                await _tradeRepository.AddAsync(Trade);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در ذخیره معامله: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public partial class EditTradeDialog : Window
    {
        private readonly ITradeRepository _tradeRepository;
        public Trade Trade { get; private set; }

        public EditTradeDialog(Trade trade, ITradeRepository tradeRepository)
        {
            InitializeComponent();
            _tradeRepository = tradeRepository;
            Trade = trade;
            DataContext = Trade;
        }

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            try
            {
                await _tradeRepository.UpdateAsync(Trade);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در ویرایش معامله: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public partial class TradeDetailsDialog : Window
    {
        public Trade Trade { get; private set; }

        public TradeDetailsDialog(Trade trade)
        {
            InitializeComponent();
            Trade = trade;
            DataContext = Trade;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public partial class HelpDialog : Window
    {
        public HelpDialog()
        {
            InitializeComponent();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
// پایان کد