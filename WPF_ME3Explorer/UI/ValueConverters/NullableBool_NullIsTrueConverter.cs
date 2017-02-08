using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WPF_ME3Explorer.UI.ValueConverters
{
    public class NullableBool_NullIsTrueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool?)value) != false? true : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool? val = (bool?)value;
            bool? param = (bool?)parameter;  // true = convert true to true, false = null = convert null to true
            return val == true ? (param == true ? (bool?)true : null) : false;
        }
    }
}
