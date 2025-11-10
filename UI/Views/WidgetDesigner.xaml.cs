// مسیر فایل: UI/Views/WidgetDesigner.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TradingJournal.Core.MetadataEngine;
using TradingJournal.Core.MetadataEngine.Models;
using TradingJournal.Core.WidgetEngine;

namespace TradingJournal.UI.Views
{
    public partial class WidgetDesigner : UserControl
    {
        private readonly MetadataManager _metadataManager;
        private readonly DynamicWidgetEngine _widgetEngine;
        
        public ObservableCollection<WidgetMetadata> Widgets { get; set; }
        private WidgetMetadata _currentWidget;

        public WidgetDesigner()
        {
            InitializeComponent();
            
            _metadataManager = new MetadataManager();
            _widgetEngine = new DynamicWidgetEngine(null);
            
            Widgets = new ObservableCollection<WidgetMetadata>();
            DataContext = this;
            
            LoadWidgets();
        }

        private void LoadWidgets()
        {
            Widgets.Clear();
            var widgets = _metadataManager.GetAllWidgets();
            foreach (var widget in widgets)
            {
                Widgets.Add(widget);
            }
        }

        private void OnNewWidget(object sender, RoutedEventArgs e)
        {
            _currentWidget = new WidgetMetadata
            {
                Name = "ویجت جدید",
                Title = "ویجت جدید",
                Type = WidgetType.Card,
                Size = WidgetSize.Medium
            };
            
            LoadWidgetToForm(_currentWidget);
        }

        private void OnEditWidget(object sender, RoutedEventArgs e)
        {
            if (WidgetsList.SelectedItem is WidgetMetadata widget)
            {
                _currentWidget = widget;
                LoadWidgetToForm(widget);
            }
        }

        private async void OnSaveWidget(object sender, RoutedEventArgs e)
        {
            if (_currentWidget != null)
            {
                SaveFormToWidget();
                await _metadataManager.SaveWidgetMetadataAsync(_currentWidget);
                LoadWidgets();
                MessageBox.Show("ویجت با موفقیت ذخیره شد");
            }
        }

        private async void OnDeleteWidget(object sender, RoutedEventArgs e)
        {
            if (WidgetsList.SelectedItem is WidgetMetadata widget)
            {
                var result = MessageBox.Show($"آیا از حذف ویجت '{widget.Title}' مطمئن هستید؟", 
                    "تأیید حذف", MessageBoxButton.YesNo);
                
                if (result == MessageBoxResult.Yes)
                {
                    await _metadataManager.DeleteWidgetAsync(widget.Name);
                    LoadWidgets();
                }
            }
        }

        private void LoadWidgetToForm(WidgetMetadata widget)
        {
            WidgetNameTextBox.Text = widget.Name;
            WidgetTitleTextBox.Text = widget.Title;
            WidgetTypeComboBox.SelectedItem = widget.Type;
            WidgetSizeComboBox.SelectedItem = widget.Size;
            DataSourceTextBox.Text = widget.DataSource;
            RefreshIntervalTextBox.Text = widget.RefreshInterval;
        }

        private void SaveFormToWidget()
        {
            if (_currentWidget == null) return;
            
            _currentWidget.Name = WidgetNameTextBox.Text;
            _currentWidget.Title = WidgetTitleTextBox.Text;
            _currentWidget.Type = (WidgetType)(WidgetTypeComboBox.SelectedItem ?? WidgetType.Card);
            _currentWidget.Size = (WidgetSize)(WidgetSizeComboBox.SelectedItem ?? WidgetSize.Medium);
            _currentWidget.DataSource = DataSourceTextBox.Text;
            _currentWidget.RefreshInterval = RefreshIntervalTextBox.Text;
        }

        private void OnPreviewWidget(object sender, RoutedEventArgs e)
        {
            if (_currentWidget != null)
            {
                SaveFormToWidget();
                UpdatePreview();
            }
        }

        private async void UpdatePreview()
        {
            PreviewContainer.Children.Clear();
            
            if (_currentWidget != null)
            {
                var widgetElement = await _widgetEngine.RenderWidget(_currentWidget);
                if (widgetElement != null)
                {
                    PreviewContainer.Children.Add(widgetElement);
                }
            }
        }
    }
}
// پایان کد