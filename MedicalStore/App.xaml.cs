using System.Windows;
using MedicalStore.DAL;
using MedicalStore.BLL.Services;
using MedicalStore.Views;

namespace MedicalStore
{
    public partial class App : Application
    {
        /// <summary>Current trial/license status, evaluated once at startup for the UI to display.</summary>
        public static LicenseStatus? LicenseStatus { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global Exception Handling
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Ensure database is created with seed data
            try
            {
                using var db = new AppDbContext();
                db.EnsureCreated();

                // Load Settings
                var settingService = new BLL.Services.SettingService();
                
                var discountType = settingService.GetSetting("DiscountType", "");
                if (!string.IsNullOrEmpty(discountType))
                    Common.Constants.AppConstants.DiscountType = discountType;

                var taxRateStr = settingService.GetSetting("TaxRate", "");
                if (decimal.TryParse(taxRateStr, out decimal parsedTax))
                    Common.Constants.AppConstants.TaxRate = parsedTax;

                var expiryDaysStr = settingService.GetSetting("NearExpiryDays", "");
                if (int.TryParse(expiryDaysStr, out int parsedExpiry))
                    Common.Constants.AppConstants.NearExpiryDays = parsedExpiry;

                var enableDecStr = settingService.GetSetting("EnableTwoDecimalPlaces", "");
                if (bool.TryParse(enableDecStr, out bool parsedDec))
                    Common.Constants.AppConstants.EnableTwoDecimalPlaces = parsedDec;

                var storeName = settingService.GetSetting("StoreName", "");
                if (!string.IsNullOrEmpty(storeName))
                    Common.Constants.AppConstants.StoreName = storeName;

                var storePhone = settingService.GetSetting("StorePhone", "");
                if (!string.IsNullOrEmpty(storePhone))
                    Common.Constants.AppConstants.StorePhone = storePhone;

                var storeAddress = settingService.GetSetting("StoreAddress", "");
                if (!string.IsNullOrEmpty(storeAddress))
                    Common.Constants.AppConstants.StoreAddress = storeAddress;

                var storeLogoPath = settingService.GetSetting("StoreLogoPath", "");
                if (!string.IsNullOrEmpty(storeLogoPath))
                    Common.Constants.AppConstants.StoreLogoPath = storeLogoPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database initialization failed: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // ── Licensing / trial gate ───────────────────────────────────────────
            // Don't let the app close between the activation window and the login
            // window just because no window is open for a moment.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var licenseService = new LicenseService();
            LicenseStatus = licenseService.Evaluate();

            if (LicenseStatus.State == LicenseState.Expired)
            {
                var win = new LicenseWindow(licenseService, trialStillActive: false);
                win.ShowDialog();
                LicenseStatus = licenseService.Evaluate();

                if (!win.Activated || LicenseStatus.State == LicenseState.Expired)
                {
                    Shutdown();   // trial ended and no valid key entered
                    return;
                }
            }

            // Proceed to login.
            var login = new LoginView();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            login.Show();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Prevent the application from closing immediately if possible
        }
    }
}
