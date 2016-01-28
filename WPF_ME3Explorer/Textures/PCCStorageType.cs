using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer.Textures
{
    public enum PCCStorageType
    {
        //arcCpr = 0x3, // archive compressed
        arcCpr = 0x11, //archive compressed (guessing)
        arcUnc = 0x1, // archive uncompressed (DLC)
        pccSto = 0x0, // pcc local storage
        empty = 0x21,  // unused image (void pointer sorta)
        pccCpr = 0x10 // pcc Compressed (only ME1)
    }
}
