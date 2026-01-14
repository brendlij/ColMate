using System;
using System.Globalization;
using System.Windows.Data;

namespace ColMate.ViewModels
{
    public class CenterConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is double pos) || !(values[1] is double size))
                return 0.0;

            return pos - (size / 2.0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}