using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MedicalStore.Common.Enums;
using MedicalStore.Common.Helpers;
using MedicalStore.BLL.Services;

namespace MedicalStore.Views
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private Button? _activeNavButton;
        private PosView? _posViewInstance;

        public MainWindow()
        {
            InitializeComponent();
            UpdateStoreName();

            // Set user info
            lblCurrentUser.Text = AppSession.CurrentFullName;
            lblUserInfo.Text = $"{AppSession.CurrentFullName} ({AppSession.CurrentRole})";

            // Clock timer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => lblDateTime.Text = DateTime.Now.ToString("dddd, dd MMM yyyy  hh:mm tt");
            _timer.Start();
            lblDateTime.Text = DateTime.Now.ToString("dddd, dd MMM yyyy  hh:mm tt");

            // Apply role-based visibility
            ApplyPermissions();

            // Navigate to dashboard
            NavigateTo("Dashboard");
        }

        public void UpdateStoreName()
        {
            var storeName = MedicalStore.Common.Constants.AppConstants.StoreName;
            lblStoreName.Text = storeName;
            this.Title = storeName;
            LoadLogo();
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

        private void ApplyPermissions()
        {
            btnUsers.Visibility = AppSession.HasPermission(Permission.ManageUsers) ? Visibility.Visible : Visibility.Collapsed;
            btnSettings.Visibility = AppSession.HasPermission(Permission.ManageSettings) || AppSession.HasPermission(Permission.SystemBackup) ? Visibility.Visible : Visibility.Collapsed;
            btnProducts.Visibility = AppSession.HasPermission(Permission.ManageProducts) ? Visibility.Visible : Visibility.Collapsed;
            btnPurchases.Visibility = AppSession.HasPermission(Permission.ManagePurchases) ? Visibility.Visible : Visibility.Collapsed;
            btnReports.Visibility = AppSession.HasPermission(Permission.ViewReports) || AppSession.HasPermission(Permission.ViewFinancials) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string page)
            {
                NavigateTo(page);
            }
        }

        private void NavigateTo(string page)
        {
            // Update active button styling
            if (_activeNavButton is not  null)
                _activeNavButton.Style = (Style)FindResource("SidebarButton");

            var btnName = $"btn{page}";
            var btn = navPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == page);
            if (btn != null)
            {
                btn.Style = (Style)FindResource("SidebarActiveButton");
                _activeNavButton = btn;
            }

            // Update title
            lblPageTitle.Text = page == "POS" ? "POS Billing" : page;

            // Navigate
            switch (page)
            {
                case "Dashboard":
                    MainFrame.Navigate(new DashboardView());
                    break;
                case "Products":
                    MainFrame.Navigate(new ProductsView());
                    break;
                case "POS":
                    if (_posViewInstance == null)
                        _posViewInstance = new PosView();
                    else
                        _posViewInstance.RefreshData();
                    MainFrame.Navigate(_posViewInstance);
                    break;
                case "Purchases":
                    MainFrame.Navigate(new PurchaseView());
                    break;
                case "Suppliers":
                    MainFrame.Navigate(new SuppliersView());
                    break;
                case "Customers":
                    MainFrame.Navigate(new CustomersView());
                    break;
                case "Returns":
                    MainFrame.Navigate(new ReturnsView());
                    break;
                case "ManualRefund":
                    MainFrame.Navigate(new ManualRefundView());
                    break;
                case "Sales":
                    MainFrame.Navigate(new SalesView());
                    break;
                case "Reports":
                    MainFrame.Navigate(new ReportsView());
                    break;
                case "Users":
                    MainFrame.Navigate(new UsersView());
                    break;
                case "Expenses":
                    MainFrame.Navigate(new ExpensesView());
                    break;
                case "Settings":
                    MainFrame.Navigate(new SettingsView());
                    break;
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                new AuthService().Logout();
                var login = new LoginView();
                login.Show();
                _timer.Stop();
                this.Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }
    }
}
