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
        string texName = null;
        public virtual string TexName
        {
            get
            {
                return texName;
            }
            set
            {
                SetProperty(ref texName, value);
            }
        }
        public MTRangedObservableCollection<PCCEntry> PCCs { get; set; } = new MTRangedObservableCollection<PCCEntry>();

        bool isHidden = false;
        public bool IsHidden
        {
            get
            {
                return isHidden;
            }
            set
            {
                SetProperty(ref isHidden, value);
            }
        }

        public bool HashChanged
        {
            get
            {
                return OriginalHash != Hash;
            }
        }


        public virtual string DefaultSaveName
        {
            get
            {
                return $"{TexName}_{ToolsetTextureEngine.FormatTexmodHashAsString(Hash)}.dds";
            }
        }

        uint hash = 0;
        public uint Hash
        {
            get
            {
                return hash;
            }
            set
            {
                hash = value;
                if (OriginalHash == 0)
                    OriginalHash = value;
                OnPropertyChanged(nameof(HashChanged));

            }
        }
        public uint OriginalHash { get; set; }
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

        int width = 0;
        public int Width
        {
            get
            {
                return width;
            }

            set
            {
                SetProperty(ref width, value);
            }
        }

        int height = 0;
        public int Height
        {
            get
            {
                return height;
            }

            set
            {
                SetProperty(ref height, value);
            }
        }

        int mips = 0;
        public int Mips
        {
            get
            {
                return mips;
            }

            set
            {
                SetProperty(ref mips, value);
            }
        }

        public MEDirectories.MEDirectories GameDirecs { get; set; }

        public AbstractTexInfo()
        {
            Thumb = new Thumbnail();
        }

        public AbstractTexInfo(MEDirectories.MEDirectories direcs) : this()
        {
            GameDirecs = direcs;
        }

        List<string> searchables = null;
        public virtual List<string> Searchables
        {
            get
            {
                if (searchables == null)
                {
                    searchables = new List<string>();
                    searchables.Add(TexName);
                    var pccs = PCCs.Select(pcc => pcc.Name.Remove(0, GameDirecs.BasePathLength));
                    var expIDs = PCCs.Select(pcc => pcc.ExpID.ToString());
                    searchables.AddRange(pccs);
                    searchables.AddRange(expIDs);
                    searchables.Add(ToolsetTextureEngine.FormatTexmodHashAsString(Hash));
                    searchables.Add(Format.ToString());

                    searchables.RemoveAll(t => t == null);  // Remove empty items
                }
                
                return searchables;
            }
        }
    }
}
