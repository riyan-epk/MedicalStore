using System;
using MedicalStore.Common.Constants;

namespace MedicalStore.Common.Helpers
{
    public static class CurrencyHelper
    {
        public static string Format(this decimal amount)
        {
            return amount.ToString(AppConstants.EnableTwoDecimalPlaces ? "N2" : "N0");
        }
        
        public static string FormatRs(this decimal amount)
        {
            return "Rs " + amount.ToString(AppConstants.EnableTwoDecimalPlaces ? "N2" : "N0");
        }

        public static decimal RoundCurrency(this decimal amount)
        {
            return AppConstants.EnableTwoDecimalPlaces ? Math.Round(amount, 2) : Math.Round(amount, 0);
        }
    }
}
