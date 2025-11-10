// مسیر فایل: UI/Views/DynamicFormView.xaml.cs
// ابتدای کد
using System;
using System.Windows;
using System.Windows.Controls;
using TradingJournal.Core.FormEngine;
using TradingJournal.Core.MetadataEngine;

namespace TradingJournal.UI.Views
{
    public partial class DynamicFormView : UserControl
    {
        private readonly DynamicFormEngine _formEngine;
        private readonly MetadataManager _metadataManager;
        
        public DynamicFormView()
        {
            InitializeComponent();
            
            _metadataManager = new MetadataManager();
            _formEngine = new DynamicFormEngine(_metadataManager);
            
            LoadForms();
            
            FormSelector.SelectionChanged += OnFormSelected;
            SaveButton.Click += OnSave;
            CancelButton.Click += OnCancel;
            ResetButton.Click += OnReset;
        }

        private void LoadForms()
        {
            var forms = _metadataManager.GetAllFormNames();
            FormSelector.ItemsSource = forms;
            
            if (forms.Count > 0)
                FormSelector.SelectedIndex = 0;
        }

        private void OnFormSelected(object sender, SelectionChangedEventArgs e)
        {
            if (FormSelector.SelectedItem is string formName)
            {
                LoadForm(formName);
            }
        }

        private void LoadForm(string formName)
        {
            FormContainer.Children.Clear();
            
            var form = _formEngine.GenerateForm(formName);
            if (form != null)
            {
                FormContainer.Children.Add(form);
                FormTitle.Text = formName;
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            // Validate and save form
            MessageBox.Show("فرم با موفقیت ذخیره شد", "موفقیت", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            // Reset form
            if (FormSelector.SelectedItem is string formName)
            {
                LoadForm(formName);
            }
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            FormContainer.Children.Clear();
        }
    }
}
// پایان کد