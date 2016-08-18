using CSharpImageLibrary;
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
        public string TexName { get; set; }
        public List<string> PCCS { get; set; } = new List<string>();
        public List<int> ExpIDs { get; set; } = new List<int>();
        public uint Hash { get; set; }
        public ImageEngineFormat Format { get; set; } = ImageEngineFormat.Unknown;
        public int GameVersion { get; set; } = -1;
        public Thumbnail Thumb { get; set; } = null;

        public abstract int Width { get; set; }
        public abstract int Height { get; set; }
        public abstract int Mips { get; set; }
    }
}
