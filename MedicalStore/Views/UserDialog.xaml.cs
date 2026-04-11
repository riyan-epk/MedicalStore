using System.Windows;
using System.Windows.Controls;
using MedicalStore.BLL.Services;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Enums;

namespace MedicalStore.Views
{
    public partial class UserDialog : Window
    {
        private readonly UserService _userService = new();
        private User? _existingUser;
        private bool _isEdit;

        public UserDialog(User? user = null)
        {
            InitializeComponent();
            cmbRole.SelectedIndex = 1; // Default Cashier

            if (user != null)
            {
                _isEdit = true;
                _existingUser = user;
                lblTitle.Text = "Edit User";
                txtUsername.Text = user.Username;
                txtUsername.IsEnabled = false;
                txtFullName.Text = user.FullName;
                txtPassword.Visibility = Visibility.Collapsed;
                lblPassword.Visibility = Visibility.Collapsed;
                chkActive.IsChecked = user.IsActive;
                pnlResetPassword.Visibility = Visibility.Visible;

                // Set role combobox
                foreach (ComboBoxItem item in cmbRole.Items)
                {
                    if (item.Tag?.ToString() == user.Role.ToString())
                    {
                        cmbRole.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFullName.Text))
            {
                MessageBox.Show("Full name is required.", "Validation");
                return;
            }

            var roleTag = (cmbRole.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Cashier";
            var role = Enum.Parse<UserRole>(roleTag);
            var permissions = role switch
            {
                UserRole.Admin => Permission.AdminAll,
                UserRole.Manager => Permission.ManagerDefault,
                _ => Permission.CashierDefault
            };

            if (_isEdit && _existingUser != null)
            {
                var result = _userService.Update(_existingUser.Id, txtFullName.Text.Trim(), role, permissions, chkActive.IsChecked ?? true);
                if (result.Success)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(result.Message, "Error");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrEmpty(txtPassword.Password))
                {
                    MessageBox.Show("Username and password are required.", "Validation");
                    return;
                }

                var result = _userService.Create(txtUsername.Text.Trim(), txtPassword.Password, txtFullName.Text.Trim(), role, permissions);
                if (result.Success)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(result.Message, "Error");
                }
            }
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (_existingUser == null) return;
            var newPass = Microsoft.VisualBasic.Interaction.InputBox("Enter new password:", "Reset Password");
            if (!string.IsNullOrEmpty(newPass))
            {
                var result = _userService.ResetPassword(_existingUser.Id, newPass);
                MessageBox.Show(result.Message, result.Success ? "Success" : "Error");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
