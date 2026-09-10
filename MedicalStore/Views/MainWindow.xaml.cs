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

            // License / trial indicator
            var lic = App.LicenseStatus;
            if (lic != null && (lic.State == LicenseState.Trial ||
                (lic.State == LicenseState.Licensed && !lic.Lifetime && lic.DaysRemaining <= 15)))
                lblLicense.Text = lic.Message;

            // Clock timer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => lblDateTime.Text = DateTime.Now.ToString("dddd, dd MMM yyyy  hh:mm tt");
            _timer.Start();
            lblDateTime.Text = DateTime.Now.ToString("dddd, dd MMM yyyy  hh:mm tt");

            // Apply role-based visibility
            ApplyPermissions();

            // Navigate to the first section the current user is allowed to open
            // (Dashboard when permitted). Guarantees a user never lands on a blank
            // frame if their permission set excludes the dashboard.
            var firstPage = navPanel.Children.OfType<Button>()
                .Select(b => b.Tag as string)
                .FirstOrDefault(p => !string.IsNullOrEmpty(p) && CanAccess(p!));
            NavigateTo(firstPage ?? "Dashboard");
        }

        /// <summary>
        /// Single source of truth for which permission each navigable section requires.
        /// Used both to hide sidebar buttons and to hard-block navigation (defense in
        /// depth) so a cashier can never reach admin/financial screens.
        /// </summary>
        public static bool CanAccess(string page) => page switch
        {
            "Dashboard"    => AppSession.HasPermission(Permission.ViewDashboard),
            "Products"     => AppSession.HasPermission(Permission.ManageProducts),
            "POS"          => AppSession.HasPermission(Permission.UsePOS),
            "Sales"        => HasAny(Permission.ViewReports | Permission.ViewFinancials),
            "Purchases"    => AppSession.HasPermission(Permission.ManagePurchases),
            "Suppliers"    => AppSession.HasPermission(Permission.ManagePurchases),
            "Customers"    => AppSession.HasPermission(Permission.ManageCustomers),
            "Returns"      => AppSession.HasPermission(Permission.ManageReturns),
            "ManualRefund" => AppSession.HasPermission(Permission.ManageReturns),
            "Reports"      => HasAny(Permission.ViewReports | Permission.ViewFinancials),
            "Users"        => AppSession.HasPermission(Permission.ManageUsers),
            "Expenses"     => AppSession.HasPermission(Permission.ViewFinancials),
            "Settings"     => HasAny(Permission.ManageSettings | Permission.SystemBackup),
            _              => true
        };

        /// <summary>True when the user holds ANY of the supplied permission flags.</summary>
        private static bool HasAny(Permission any) => (AppSession.CurrentPermissions & any) != 0;

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
            // Show a sidebar button only if the user may open that section. Every
            // navigable button is covered (previously Sales, Suppliers, Customers,
            // Returns, Manual Refund and Expenses were always visible to everyone).
            foreach (var btn in navPanel.Children.OfType<Button>())
            {
                if (btn.Tag is string page)
                    btn.Visibility = CanAccess(page) ? Visibility.Visible : Visibility.Collapsed;
            }
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
            // Hard authorization gate (defense in depth): even if a button were made
            // visible by mistake or a future code path calls this directly, a user
            // without the required permission cannot open the section.
            if (!CanAccess(page))
            {
                MessageBox.Show("You do not have permission to access this section.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
