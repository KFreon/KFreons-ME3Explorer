using CSharpImageLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.Textures
{
    /// <summary>
    /// TexInfo baseclass variables/properties/methods. Cannot be initialised.
    /// </summary>
    public abstract class AbstractTexInfo : ViewModelBase
    {
        public string TexName { get; set; }
        public List<PCCEntry> PCCS { get; set; } = new List<PCCEntry>();
        public uint Hash { get; set; }
        public ImageEngineFormat Format { get; set; } = ImageEngineFormat.Unknown;
        public int GameVersion
        {
            get
            {
                return GameDirecs.GameVersion;
            }
        }

        Thumbnail thumb = null;
        public Thumbnail Thumb
        {
            get
            {
                return thumb;
            }
            set
            {
                SetProperty(ref thumb, value);
            }
        }

        public abstract int Width { get; set; }
        public abstract int Height { get; set; }
        public abstract int Mips { get; set; }
        public MEDirectories.MEDirectories GameDirecs { get; set; }

        public AbstractTexInfo()
        {
            
        }

        public AbstractTexInfo(MEDirectories.MEDirectories direcs)
        {
            GameDirecs = direcs;
        }

        public virtual List<string> Searchables
        {
            get
            {
                List<string> searchables = new List<string>();

                searchables.Add(TexName);
                var pccs = PCCS.Select(pcc => pcc.Name);
                var expIDs = PCCS.Select(pcc => pcc.ExpID.ToString());
                searchables.AddRange(pccs);
                searchables.AddRange(expIDs);
                searchables.Add(ToolsetTextureEngine.FormatTexmodHashAsString(Hash));
                searchables.Add(Format.ToString());

                return searchables;
            }
        }
    }
}
