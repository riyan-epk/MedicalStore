using System;
using System.Globalization;
using System.Windows.Data;

namespace MedicalStore.Helpers
{
    public class BalanceToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal balance)
            {
                if (balance > 0) return "Debt";
                if (balance < 0) return "Advance";
            }
            return "Neutral";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
