using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;

namespace MedicalStore.Views
{
    public partial class UsersView : Page
    {
        private readonly UserService _userService = new();

        public UsersView()
        {
            InitializeComponent();
            LoadUsers();
        }

        private void LoadUsers()
        {
            dgUsers.ItemsSource = _userService.GetAll();
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new UserDialog();
            if (dialog.ShowDialog() == true)
            {
                LoadUsers();
            }
        }

        private void dgUsers_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgUsers.SelectedItem is User user)
            {
                var dialog = new UserDialog(user);
                if (dialog.ShowDialog() == true)
                {
                    LoadUsers();
                }
            }
        }
    }
}
