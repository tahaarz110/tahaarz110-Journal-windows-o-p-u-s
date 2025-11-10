// مسیر فایل: UI/Views/ReportView.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TradingJournal.Core.ReportEngine;

namespace TradingJournal.UI.Views
{
    public partial class ReportView : UserControl
    {
        private readonly DynamicReportEngine _reportEngine;
        public ObservableCollection<ReportTemplate> Templates { get; set; }
        
        public ReportView(DynamicReportEngine reportEngine)
        {
            InitializeComponent();
            _reportEngine = reportEngine;
            
            Templates = new ObservableCollection<ReportTemplate>();
            DataContext = this;
            
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            Templates.Add(new ReportTemplate
            {
                Name = "Performance",
                Title = "گزارش عملکرد",
                Description = "گزارش کامل عملکرد معاملاتی",
                Icon = "ChartLine"
            });
            
            Templates.Add(new ReportTemplate
            {
                Name = "Analysis",
                Title = "گزارش تحلیلی",
                Description = "تحلیل‌های هوشمند معاملات",
                Icon = "Analytics"
            });
            
            Templates.Add(new ReportTemplate
            {
                Name = "Tax",
                Title = "گزارش مالیاتی",
                Description = "اطلاعات مالیاتی معاملات",
                Icon = "Calculator"
            });
            
            Templates.Add(new ReportTemplate
            {
                Name = "Custom",
                Title = "گزارش سفارشی",
                Description = "ایجاد گزارش دلخواه",
                Icon = "FileDocument"
            });
        }

        private async void OnGenerateReport(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is ReportTemplate template))
                return;

            try
            {
                ShowProgress(true);
                
                var request = new ReportRequest
                {
                    Name = template.Name,
                    Title = template.Title,
                    Type = template.Name,
                    Format = (ReportFormat)FormatComboBox.SelectedIndex,
                    StartDate = StartDatePicker.SelectedDate,
                    EndDate = EndDatePicker.SelectedDate,
                    IncludeAnalysis = IncludeAnalysisCheckBox.IsChecked ?? false,
                    IncludeCharts = IncludeChartsCheckBox.IsChecked ?? false
                };

                var result = await _reportEngine.GenerateReport(request);
                
                ShowProgress(false);
                
                if (result.Success)
                {
                    var openResult = MessageBox.Show(
                        "گزارش با موفقیت تولید شد. آیا می‌خواهید آن را باز کنید؟",
                        "موفقیت",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    
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
                    MessageBox.Show(result.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ShowProgress(false);
                MessageBox.Show($"خطا در تولید گزارش: {ex.Message}", "خطا", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowProgress(bool show)
        {
            ProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public class ReportTemplate
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }
}
// پایان کد