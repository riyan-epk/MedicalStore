using System;
using System.Globalization;
using System.Windows.Data;
using MedicalStore.Common.Helpers;

namespace MedicalStore.Converters
{
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
            {
                return d.FormatRs();
            }
            if (value is int i)
            {
                return ((decimal)i).FormatRs();
            }
            if (value is double db)
            {
                return ((decimal)db).FormatRs();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RawCurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
            {
                return d.Format();
            }
            if (value is int i)
            {
                return ((decimal)i).Format();
            }
            if (value is double db)
            {
                return ((decimal)db).Format();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
