using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer.Textures
{
    /// <summary>
    /// TexInfo baseclass variables/properties/methods. Cannot be initialised.
    /// </summary>
    public abstract class AbstractTexInfo
    {
        public string TexName = null;
        public List<string> PCCS = null;
        public List<int> ExpIDs = null;
        public uint Hash = 0;
        public TextureFormat Format = TextureFormat.Unknown;
        public int GameVersion = -1;
        public Thumbnail Thumb { get; set; } = null;

        public abstract int Width { get; set; }
        public abstract int Height { get; set; }
        public abstract int Mips { get; set; }


        public AbstractTexInfo()
        {
            PCCS = new List<string>();
            ExpIDs = new List<int>();
        }
    }
}
