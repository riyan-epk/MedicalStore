using System.Windows;

namespace MedicalStore.Views
{
    public partial class InputDialog : Window
    {
        public string Answer { get; private set; } = "";

        public InputDialog(string title, string prompt)
        {
            InitializeComponent();
            Title = title;
            lblPrompt.Text = prompt;
            txtInput.Focus();
            txtInput.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Answer = txtInput.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
