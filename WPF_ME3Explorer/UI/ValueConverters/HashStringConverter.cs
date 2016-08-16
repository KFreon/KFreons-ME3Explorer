using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ValueConverters
{
    public class HashToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string hash = null;
            if (value.GetType() == typeof(uint))
                hash = ToolsetTextureEngine.FormatTexmodHashAsString((uint)value);

            return hash;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            uint hash = 0;
            if (value.GetType() == typeof(String))
                hash = ToolsetTextureEngine.FormatTexmodHashAsUint((string)value);

            return hash;
        }
    }
}
