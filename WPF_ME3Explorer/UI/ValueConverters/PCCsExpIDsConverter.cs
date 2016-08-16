using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ValueConverters
{
    public class PCCsExpIDsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TreeTexInfo tex = value as TreeTexInfo;
            if (tex == null)
                return null;

            return null; // Check target Type
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
