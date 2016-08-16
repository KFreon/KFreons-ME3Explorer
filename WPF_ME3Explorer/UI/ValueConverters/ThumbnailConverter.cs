using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ValueConverters
{
    public class ThumbnailConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Thumbnail thumb = value as Thumbnail;
            if (thumb == null)
            {
                // Default image
                return (BitmapImage)Application.Current.Resources.FindName("DefaultTexture");
            }
            else
                return thumb.GetImage();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
