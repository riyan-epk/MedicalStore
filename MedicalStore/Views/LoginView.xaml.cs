using System.Windows;
using System.Windows.Input;
using MedicalStore.BLL.Services;

namespace MedicalStore.Views
{
    public partial class LoginView : Window
    {
        private readonly AuthService _authService = new();

        public LoginView()
        {
            InitializeComponent();
            lblStoreName.Text = MedicalStore.Common.Constants.AppConstants.StoreName;
            this.Title = $"{MedicalStore.Common.Constants.AppConstants.StoreName} - Login";
            txtUsername.Focus();

            LoadLogo();
            ShowLicenseStatus();
        }

        private void ShowLicenseStatus()
        {
            var status = App.LicenseStatus;
            if (status == null) return;
            // Show a small banner while on trial, or when a paid license is near expiry.
            if (status.State == BLL.Services.LicenseState.Trial ||
                (status.State == BLL.Services.LicenseState.Licensed && !status.Lifetime && status.DaysRemaining <= 15))
            {
                lblTrial.Text = status.Message;
                trialBox.Visibility = Visibility.Visible;
            }
        }

        private void LoadLogo()
        {
            var logoPath = MedicalStore.Common.Constants.AppConstants.StoreLogoPath;
            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(logoPath);
                    bitmap.EndInit();
                    
                    imgStoreLogo.Source = bitmap;
                    imgStoreLogo.Visibility = Visibility.Visible;
                    iconStoreLogo.Visibility = Visibility.Collapsed;
                }
                catch (Exception)
                {
                    // If image fails to load, keep default icon
                    imgStoreLogo.Visibility = Visibility.Collapsed;
                    iconStoreLogo.Visibility = Visibility.Visible;
                }
            }
            else
            {
                imgStoreLogo.Visibility = Visibility.Collapsed;
                iconStoreLogo.Visibility = Visibility.Visible;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            DoLogin();
        }

        private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DoLogin();
        }

        private void DoLogin()
        {
            var username = txtUsername.Text.Trim();
            var password = txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            var result = _authService.Login(username, password);
            if (result.Success)
            {
                // Force the client to replace the well-known default admin password.
                if (username == MedicalStore.Common.Constants.AppConstants.DefaultAdminUsername &&
                    password == MedicalStore.Common.Constants.AppConstants.DefaultAdminPassword &&
                    result.User != null)
                {
                    var cpw = new ChangePasswordWindow(result.User.Id) { Owner = this };
                    if (cpw.ShowDialog() != true)
                    {
                        ShowError("You must change the default password to continue.");
                        return;
                    }
                }

                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            else
            {
                ShowError(result.Message);
            }
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.Visibility = Visibility.Visible;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
