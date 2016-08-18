using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpImageLibrary;
using UsefulThings;

namespace WPF_ME3Explorer.Textures
{
    public static class Misc
    {
        public static ImageEngineFormat ParseFormat(string formatString)
        {
            if (String.IsNullOrEmpty(formatString))
                return ImageEngineFormat.Unknown;

            if (formatString.Contains("normal", StringComparison.OrdinalIgnoreCase))
                return ImageEngineFormat.DDS_ATI2_3Dc;
            else
                return ImageFormats.FindFormatInString(formatString).SurfaceFormat;
        }

        public static string StringifyFormat(ImageEngineFormat format)
        {
            return format.ToString().Replace("DDS_", "").Replace("_3Dc", "");
        }
    }
}
