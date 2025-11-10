// مسیر فایل: UI/Views/QueryBuilder.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using TradingJournal.Core.QueryEngine;
using TradingJournal.Core.QueryEngine.Models;
using TradingJournal.Data.Models;

namespace TradingJournal.UI.Views
{
    public partial class QueryBuilder : UserControl
    {
        private readonly DynamicQueryBuilder<Trade> _queryBuilder;
        private readonly QueryRepository _queryRepository;
        private readonly ExportManager _exportManager;

        public ObservableCollection<QueryField> AvailableFields { get; set; }
        public ObservableCollection<FilterCondition> FilterConditions { get; set; }
        public ObservableCollection<SortField> SortFields { get; set; }
        public ObservableCollection<string> SavedQueries { get; set; }
        public ObservableCollection<Trade> QueryResults { get; set; }

        private QueryModel _currentQuery;

        public QueryBuilder()
        {
            InitializeComponent();

            _queryBuilder = new DynamicQueryBuilder<Trade>();
            _queryRepository = new QueryRepository();
            _exportManager = new ExportManager();

            AvailableFields = new ObservableCollection<QueryField>();
            FilterConditions = new ObservableCollection<FilterCondition>();
            SortFields = new ObservableCollection<SortField>();
            SavedQueries = new ObservableCollection<string>();
            QueryResults = new ObservableCollection<Trade>();

            DataContext = this;

            LoadAvailableFields();
            LoadSavedQueries();
        }

        private void LoadAvailableFields()
        {
            AvailableFields.Clear();
            var fields = _queryBuilder.GetAvailableFields();
            foreach (var field in fields)
            {
                AvailableFields.Add(field);
            }
        }

        private async void LoadSavedQueries()
        {
            SavedQueries.Clear();
            var queries = await _queryRepository.GetQueryNamesAsync();
            foreach (var query in queries)
            {
                SavedQueries.Add(query);
            }
        }

        private void OnAddFilter(object sender, RoutedEventArgs e)
        {
            var newFilter = new FilterCondition
            {
                FieldName = AvailableFields.FirstOrDefault()?.FieldName,
                Operator = FilterOperator.Equal
            };

            FilterConditions.Add(newFilter);
        }

        private void OnRemoveFilter(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is FilterCondition filter)
            {
                FilterConditions.Remove(filter);
            }
        }

        private void OnAddSort(object sender, RoutedEventArgs e)
        {
            var newSort = new SortField
            {
                FieldName = AvailableFields.FirstOrDefault()?.FieldName,
                Direction = SortDirection.Ascending,
                Order = SortFields.Count
            };

            SortFields.Add(newSort);
        }

        private void OnRemoveSort(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SortField sort)
            {
                SortFields.Remove(sort);
                ReorderSortFields();
            }
        }

        private void ReorderSortFields()
        {
            for (int i = 0; i < SortFields.Count; i++)
            {
                SortFields[i].Order = i;
            }
        }

        private async void OnExecuteQuery(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowProgress(true, "در حال اجرای پرس‌وجو...");

                // ساخت QueryModel از UI
                _currentQuery = BuildQueryFromUI();

                // اجرای query
                await Task.Run(() =>
                {
                    // این‌جا باید از دیتابیس واقعی داده بگیریم
                    // فعلاً داده‌های نمونه
                    var trades = GetSampleTrades().AsQueryable();
                    var results = _queryBuilder.BuildQuery(trades, _currentQuery).ToList();

                    Dispatcher.Invoke(() =>
                    {
                        QueryResults.Clear();
                        foreach (var result in results)
                        {
                            QueryResults.Add(result);
                        }
                    });
                });

                ShowProgress(false);
                ShowSnackbar($"{QueryResults.Count} رکورد یافت شد");
            }
            catch (Exception ex)
            {
                ShowProgress(false);
                ShowError($"خطا در اجرای پرس‌وجو: {ex.Message}");
            }
        }

        private QueryModel BuildQueryFromUI()
        {
            var query = new QueryModel
            {
                Name = QueryNameTextBox.Text,
                Description = QueryDescriptionTextBox.Text,
                SelectedFields = AvailableFields.Where(f => f.IsSelected).ToList(),
                SortFields = SortFields.ToList()
            };

            // ساخت فیلترها
            var rootFilter = new QueryFilter
            {
                Logic = (FilterLogic)FilterLogicComboBox.SelectedIndex,
                Conditions = FilterConditions.ToList()
            };

            query.RootFilter = rootFilter;

            return query;
        }

        private async void OnSaveQuery(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(QueryNameTextBox.Text))
            {
                ShowError("لطفاً نام پرس‌وجو را وارد کنید");
                return;
            }

            try
            {
                _currentQuery = BuildQueryFromUI();
                await _queryRepository.SaveQueryAsync(_currentQuery);
                LoadSavedQueries();
                ShowSnackbar("پرس‌وجو با موفقیت ذخیره شد");
            }
            catch (Exception ex)
            {
                ShowError($"خطا در ذخیره پرس‌وجو: {ex.Message}");
            }
        }

        private async void OnLoadQuery(object sender, RoutedEventArgs e)
        {
            if (SavedQueriesComboBox.SelectedItem is string queryName)
            {
                try
                {
                    var query = await _queryRepository.LoadQueryAsync(queryName);
                    if (query != null)
                    {
                        LoadQueryToUI(query);
                        ShowSnackbar("پرس‌وجو بارگذاری شد");
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"خطا در بارگذاری پرس‌وجو: {ex.Message}");
                }
            }
        }

        private void LoadQueryToUI(QueryModel query)
        {
            QueryNameTextBox.Text = query.Name;
            QueryDescriptionTextBox.Text = query.Description;

            // بارگذاری فیلدهای انتخابی
            foreach (var field in AvailableFields)
            {
                field.IsSelected = query.SelectedFields.Any(f => f.FieldName == field.FieldName);
            }

            // بارگذاری فیلترها
            FilterConditions.Clear();
            if (query.RootFilter != null)
            {
                FilterLogicComboBox.SelectedIndex = (int)query.RootFilter.Logic;
                foreach (var condition in query.RootFilter.Conditions)
                {
                    FilterConditions.Add(condition);
                }
            }

            // بارگذاری مرتب‌سازی
            SortFields.Clear();
            foreach (var sort in query.SortFields)
            {
                SortFields.Add(sort);
            }
        }

        private async void OnExportResults(object sender, RoutedEventArgs e)
        {
            if (!QueryResults.Any())
            {
                ShowError("نتیجه‌ای برای خروجی وجود ندارد");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx|CSV Files|*.csv|PDF Files|*.pdf|JSON Files|*.json|HTML Files|*.html",
                FileName = $"Export_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var format = dialog.FilterIndex switch
                {
                    1 => ExportFormat.Excel,
                    2 => ExportFormat.Csv,
                    3 => ExportFormat.Pdf,
                    4 => ExportFormat.Json,
                    5 => ExportFormat.Html,
                    _ => ExportFormat.Excel
                };

                var options = new ExportOptions
                {
                    Format = format,
                    FilePath = dialog.FileName,
                    Title = _currentQuery?.Name ?? "گزارش معاملات"
                };

                ShowProgress(true, "در حال ایجاد خروجی...");

                var result = await _exportManager.ExportWithOptionsAsync(QueryResults, options);

                ShowProgress(false);

                if (result.Success)
                {
                    ShowSnackbar("خروجی با موفقیت ایجاد شد");
                    
                    // باز کردن فایل
                    var openResult = MessageBox.Show("آیا می‌خواهید فایل را باز کنید؟", "باز کردن فایل",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (openResult == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = result.FilePath,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    ShowError(result.Message);
                }
            }
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            QueryNameTextBox.Clear();
            QueryDescriptionTextBox.Clear();
            FilterConditions.Clear();
            SortFields.Clear();
            QueryResults.Clear();

            foreach (var field in AvailableFields)
            {
                field.IsSelected = false;
            }

            ShowSnackbar("همه موارد پاک شد");
        }

        private void ShowProgress(bool show, string message = "")
        {
            ProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ProgressText.Text = message;
            ProgressText.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowSnackbar(string message)
        {
            MainSnackbar.MessageQueue?.Enqueue(message);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // داده‌های نمونه برای تست
        private List<Trade> GetSampleTrades()
        {
            return new List<Trade>
            {
                new Trade { Id = 1, Symbol = "EUR/USD", EntryPrice = 1.2000m, ExitPrice = 1.2050m, 
                    Profit = 50, EntryDate = DateTime.Now.AddDays(-10) },
                new Trade { Id = 2, Symbol = "GBP/USD", EntryPrice = 1.3500m, ExitPrice = 1.3450m, 
                    Profit = -50, EntryDate = DateTime.Now.AddDays(-8) },
                new Trade { Id = 3, Symbol = "USD/JPY", EntryPrice = 110.00m, ExitPrice = 110.50m, 
                    Profit = 50, EntryDate = DateTime.Now.AddDays(-5) },
            };
        }
    }
}
// پایان کد