using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WPF_ME3Explorer
{
    public class HashToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string hash = null;
            if (value.GetType() == typeof(uint))
                hash = WPF_ME3Explorer.General.FormatTexmodHashAsString((uint)value);

            return hash;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            uint hash = 0;
            if (value.GetType() == typeof(String))
                hash = WPF_ME3Explorer.General.FormatTexmodHashAsUint((string)value);

            return hash;
        }
    }
}
