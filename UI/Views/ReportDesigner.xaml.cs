// مسیر فایل: UI/Views/ReportDesigner.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TradingJournal.Core.ReportEngine;

namespace TradingJournal.UI.Views
{
    public partial class ReportDesigner : UserControl
    {
        public ObservableCollection<ReportTemplate> Templates { get; set; }
        public ObservableCollection<ReportElement> Elements { get; set; }
        
        public ReportDesigner()
        {
            InitializeComponent();
            
            Templates = new ObservableCollection<ReportTemplate>();
            Elements = new ObservableCollection<ReportElement>();
            
            DataContext = this;
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            Templates.Add(new ReportTemplate { Name = "عملکرد ماهانه", Type = "Performance" });
            Templates.Add(new ReportTemplate { Name = "تحلیل استراتژی", Type = "Analysis" });
            Templates.Add(new ReportTemplate { Name = "گزارش مالیاتی", Type = "Tax" });
            Templates.Add(new ReportTemplate { Name = "سفارشی", Type = "Custom" });
        }

        private void OnAddElement(object sender, RoutedEventArgs e)
        {
            var element = new ReportElement
            {
                Type = ReportElementType.Text,
                Content = "عنصر جدید"
            };
            Elements.Add(element);
        }

        private void OnRemoveElement(object sender, RoutedEventArgs e)
        {
            if (ElementsList.SelectedItem is ReportElement element)
            {
                Elements.Remove(element);
            }
        }

        private void OnSaveTemplate(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("قالب گزارش ذخیره شد", "موفقیت", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnPreviewReport(object sender, RoutedEventArgs e)
        {
            // Preview report
        }
    }
}
// پایان کد