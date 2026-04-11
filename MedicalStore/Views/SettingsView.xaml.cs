using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MedicalStore.BLL.Services;
using MedicalStore.Common.Constants;

namespace MedicalStore.Views
{
    public partial class SettingsView : Page
    {
        private readonly BackupService _backupService = new();
        private readonly SettingService _settingService = new();

        public SettingsView()
        {
            InitializeComponent();
            // Load current store info
            txtStoreName.Text = AppConstants.StoreName;
            txtStorePhone.Text = AppConstants.StorePhone;
            txtStoreAddress.Text = AppConstants.StoreAddress;
            txtStoreLogo.Text = AppConstants.StoreLogoPath;

            // Load POS settings
            cmbDiscountType.Text = AppConstants.DiscountType;
            txtTaxRate.Text = AppConstants.TaxRate.ToString();
            txtExpiryDays.Text = AppConstants.NearExpiryDays.ToString();
            chkTwoDecimals.IsChecked = AppConstants.EnableTwoDecimalPlaces;

        }

        private void SavePosSettings_Click(object sender, RoutedEventArgs e)
        {
            AppConstants.DiscountType = (cmbDiscountType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Percentage";
            _settingService.SaveSetting("DiscountType", AppConstants.DiscountType);

            if (decimal.TryParse(txtTaxRate.Text, out decimal tax))
            {
                AppConstants.TaxRate = tax;
                _settingService.SaveSetting("TaxRate", tax.ToString());
            }
            if (int.TryParse(txtExpiryDays.Text, out int expiryDays))
            {
                AppConstants.NearExpiryDays = expiryDays;
                _settingService.SaveSetting("NearExpiryDays", expiryDays.ToString());
            }
            if (chkTwoDecimals.IsChecked.HasValue)
            {
                AppConstants.EnableTwoDecimalPlaces = chkTwoDecimals.IsChecked.Value;
                _settingService.SaveSetting("EnableTwoDecimalPlaces", chkTwoDecimals.IsChecked.Value.ToString());
            }
            MessageBox.Show("POS settings updated and saved.", "Success");
        }

        private void SaveStoreInfo_Click(object sender, RoutedEventArgs e)
        {
            AppConstants.StoreName = txtStoreName.Text.Trim();
            AppConstants.StorePhone = txtStorePhone.Text.Trim();
            AppConstants.StoreAddress = txtStoreAddress.Text.Trim();
            AppConstants.StoreLogoPath = txtStoreLogo.Text.Trim();

            _settingService.SaveSetting("StoreName", AppConstants.StoreName);
            _settingService.SaveSetting("StorePhone", AppConstants.StorePhone);
            _settingService.SaveSetting("StoreAddress", AppConstants.StoreAddress);
            _settingService.SaveSetting("StoreLogoPath", AppConstants.StoreLogoPath);

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateStoreName();
            }

            MessageBox.Show("Store information saved successfully.", "Saved");
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select backup location"
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var result = _backupService.BackupDatabase(dialog.SelectedPath);
                MessageBox.Show(result.Message, result.Success ? "Backup Complete" : "Backup Failed");
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "This will replace your current database. Are you sure?",
                "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Database files (*.db)|*.db",
                    Title = "Select backup file"
                };
                if (dialog.ShowDialog() == true)
                {
                    var result = _backupService.RestoreDatabase(dialog.FileName);
                    MessageBox.Show(result.Message, result.Success ? "Restore Complete" : "Restore Failed");
                }
            }
        }

        private void BrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Select Store Logo"
            };
            
            if (dialog.ShowDialog() == true)
            {
                txtStoreLogo.Text = dialog.FileName;
            }
        }

        private void ClearLogo_Click(object sender, RoutedEventArgs e)
        {
            txtStoreLogo.Text = "";
        }
    }
}
