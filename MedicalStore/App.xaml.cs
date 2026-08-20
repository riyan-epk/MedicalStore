using System.Windows;
using MedicalStore.DAL;

namespace MedicalStore
{
    public partial class App : Application
    {
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
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Prevent the application from closing immediately if possible
        }
    }
}
