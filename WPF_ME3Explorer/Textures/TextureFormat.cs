using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpImageLibrary;
using UsefulThings;

namespace WPF_ME3Explorer.Textures
{
    public enum TextureFormat
    {
        Unknown = 0,

        // KFreon: DXT formats
        DXT1 = 1,
        DXT2 = 2,
        DXT3 = 3,
        DXT4 = 4,
        DXT5 = 5,

        // KFreon: Normal maps. Pretty much going to be considered the same format.
        V8U8 = 6,
        ATI1 = 7,
        ThreeDC = 8,  // KFreon: This is also NormalMap_HQ and ATI2N
        /*PF_NormalMap_HQ = 8,
        NormalMap_HQ = 8,
        ATI2N = 8,*/

        // KFreon: Misc other formats
        A8R8G8B8 = 10,
        G8 = 11,


        // KFreon: Normal image formats
        BMP = 12,
        JPG = 13,
        JPEG = 13,
        PNG = 14,
        GIF = 15,
        TGA = 16 // KFreon: Dunno if this works...format, not the value = 16
    }
}
