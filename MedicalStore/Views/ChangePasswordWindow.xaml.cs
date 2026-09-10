using System.Windows;
using MedicalStore.BLL.Services;

namespace MedicalStore.Views
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly int _userId;
        private readonly UserService _users = new();

        public ChangePasswordWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            lblMsg.Visibility = Visibility.Collapsed;
            var pw = txtNew.Password;
            var confirm = txtConfirm.Password;

            if (string.IsNullOrWhiteSpace(pw) || pw.Length < 6)
            {
                Show("Password must be at least 6 characters.");
                return;
            }
            if (pw == "admin123")
            {
                Show("Please choose a password different from the default.");
                return;
            }
            if (pw != confirm)
            {
                Show("Passwords do not match.");
                return;
            }

            var res = _users.ResetPassword(_userId, pw);
            if (res.Success)
            {
                MessageBox.Show("Password updated successfully.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                Show(res.Message);
            }
        }

        private void Show(string m)
        {
            lblMsg.Text = m;
            lblMsg.Visibility = Visibility.Visible;
        }
    }
}
