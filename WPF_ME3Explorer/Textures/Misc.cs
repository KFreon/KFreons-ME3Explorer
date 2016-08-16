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

        public static TextureFormat ConvertFormat(ImageEngineFormat format)
        {
            TextureFormat commonFormat = TextureFormat.Unknown;
            switch (format)
            {
                case ImageEngineFormat.BMP:
                    commonFormat = TextureFormat.BMP;
                    break;
                case ImageEngineFormat.DDS_ARGB:
                    commonFormat = TextureFormat.A8R8G8B8;
                    break;
                case ImageEngineFormat.DDS_ATI1:
                    commonFormat = TextureFormat.ATI1;
                    break;
                case ImageEngineFormat.DDS_ATI2_3Dc:
                    commonFormat = TextureFormat.ThreeDC;
                    break;
                case ImageEngineFormat.DDS_DXT1:
                    commonFormat = TextureFormat.DXT1;
                    break;
                case ImageEngineFormat.DDS_DXT2:
                    commonFormat = TextureFormat.DXT2;
                    break;
                case ImageEngineFormat.DDS_DXT3:
                    commonFormat = TextureFormat.DXT3;
                    break;
                case ImageEngineFormat.DDS_DXT4:
                    commonFormat = TextureFormat.DXT4;
                    break;
                case ImageEngineFormat.DDS_DXT5:
                    commonFormat = TextureFormat.DXT5;
                    break;
                case ImageEngineFormat.DDS_G8_L8:
                    commonFormat = TextureFormat.G8;
                    break;
                case ImageEngineFormat.DDS_V8U8:
                    commonFormat = TextureFormat.V8U8;
                    break;
                case ImageEngineFormat.JPG:
                    commonFormat = TextureFormat.JPG;
                    break;
                case ImageEngineFormat.PNG:
                    commonFormat = TextureFormat.PNG;
                    break;
                case ImageEngineFormat.TGA:
                    commonFormat = TextureFormat.TGA;
                    break;
                default:
                    commonFormat = TextureFormat.Unknown;
                    break;
            }

            return commonFormat;
        }

        public static ImageEngineFormat ConvertFormat(TextureFormat format)
        {
            ImageEngineFormat specificFormat = ImageEngineFormat.Unknown;
            switch (format)
            {
                case TextureFormat.BMP:
                    specificFormat = ImageEngineFormat.BMP;
                    break;
                case TextureFormat.A8R8G8B8:
                    specificFormat = ImageEngineFormat.DDS_ARGB;
                    break;
                case TextureFormat.ATI1:
                    specificFormat = ImageEngineFormat.DDS_ATI1;
                    break;
                case TextureFormat.ThreeDC:
                    specificFormat = ImageEngineFormat.DDS_ATI2_3Dc;
                    break;
                case TextureFormat.DXT1:
                    specificFormat = ImageEngineFormat.DDS_DXT1;
                    break;
                case TextureFormat.DXT2:
                    specificFormat = ImageEngineFormat.DDS_DXT2;
                    break;
                case TextureFormat.DXT3:
                    specificFormat = ImageEngineFormat.DDS_DXT3;
                    break;
                case TextureFormat.DXT4:
                    specificFormat = ImageEngineFormat.DDS_DXT4;
                    break;
                case TextureFormat.DXT5:
                    specificFormat = ImageEngineFormat.DDS_DXT5;
                    break;
                case TextureFormat.G8:
                    specificFormat = ImageEngineFormat.DDS_G8_L8;
                    break;
                case TextureFormat.V8U8:
                    specificFormat = ImageEngineFormat.DDS_V8U8;
                    break;
                case TextureFormat.JPG:
                    specificFormat = ImageEngineFormat.JPG;
                    break;
                case TextureFormat.PNG:
                    specificFormat = ImageEngineFormat.PNG;
                    break;
                case TextureFormat.TGA:
                    specificFormat = ImageEngineFormat.TGA;
                    break;
                default:
                    specificFormat = ImageEngineFormat.Unknown;
                    break;
            }

            return specificFormat;
        }
    }
}
