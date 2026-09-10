namespace MedicalStore.Common.Constants
{
    public static class AppConstants
    {
        public const string AppName = "MedicalStore Pro";
        public const string DbFileName = "MedicalStore.db";
        public const int LowStockThreshold = 10;
        public static int NearExpiryDays { get; set; } = 90; // 3 months
        public const string DefaultAdminUsername = "admin";
        public const string DefaultAdminPassword = "admin123";
        public const string WalkInCustomerName = "Walk-in Customer";
        public const string ReceiptPolicyNote = "Medicines can be returned/exchanged within 3 days with original bill. Reopened or loose items are not returnable.";
        
        public static string StoreName { get; set; } = "Medical Store";
        public static string StorePhone { get; set; } = "";
        public static string StoreAddress { get; set; } = "";
        public static string DiscountType { get; set; } = "Percentage"; // Percentage or Value
        public static decimal TaxRate { get; set; } = 0;
        public static bool EnableTwoDecimalPlaces { get; set; } = true;
        public static string StoreLogoPath { get; set; } = "";

        // ── Developer / vendor branding ──────────────────────────────────────────
        public const string DeveloperName = "Riyan";
        public const string DeveloperPhone = "03098480389";
        public const string DeveloperBranding = "Developed by Riyan  •  0309 8480389";

        // ── Licensing ────────────────────────────────────────────────────────────
        public const int TrialDays = 7;
    }
}
