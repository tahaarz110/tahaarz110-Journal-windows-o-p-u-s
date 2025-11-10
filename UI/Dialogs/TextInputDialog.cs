// مسیر فایل: UI/Dialogs/TextInputDialog.cs
// ابتدای کد
using System.Windows;

namespace TradingJournal.UI.Dialogs
{
    public partial class TextInputDialog : Window
    {
        public string InputText { get; private set; }

        public TextInputDialog(string prompt, string defaultValue = "")
        {
            InitializeComponent();
            PromptText.Text = prompt;
            InputTextBox.Text = defaultValue;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
// پایان کد