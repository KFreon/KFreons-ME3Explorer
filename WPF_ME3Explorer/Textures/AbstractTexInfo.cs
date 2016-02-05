using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpImageLibrary.General;
using UsefulThings;
using UsefulThings.WPF;
using WPF_ME3Explorer.PCCObjects;

namespace WPF_ME3Explorer.Textures
{
    public class AbstractTexInfo : ViewModelBase, IToolEntry, ISearchable
    {
        #region Properties
        string entryname = null;
        public string EntryName
        {
            get
            {
                return entryname;
            }

            set
            {
                SetProperty(ref entryname, value);
            }
        }

        bool issearchvisibile = true;
        public bool IsSearchVisible
        {
            get
            {
                return issearchvisibile;
            }

            set
            {
                SetProperty(ref issearchvisibile, value);
            }
        }

        bool isselected = false;
        public bool IsSelected
        {
            get
            {
                return isselected;
            }

            set
            {
                SetProperty(ref isselected, value);
            }
        }

        public MTRangedObservableCollection<PCCEntry> PCCs { get; set; }


        uint hash = 0;
        public virtual uint Hash
        {
            get
            {
                return hash;
            }
            set
            {
                SetProperty(ref hash, value);
                OnPropertyChanged("IsHashChanged");
            }
        }

        string hashstring = null;
        public virtual string HashString
        {
            get
            {
                if (hashstring == null || IsHashChanged)
                    hashstring = WPF_ME3Explorer.General.FormatTexmodHashAsString(Hash);

                return hashstring;
            }
        }

        public uint OriginalHash { get; set; }

        public bool IsHashChanged
        {
            get
            {
                return OriginalHash != Hash;
            }
        }

        int gameversion = 0;
        public int GameVersion
        {
            get
            {
                return gameversion;
            }
            set
            {
                SetProperty(ref gameversion, value);
            }
        }

        string pathbio = null;
        public string PathBIOGame
        {
            get
            {
                return pathbio;
            }
            set
            {
                SetProperty(ref pathbio, value);
            }
        }

        int numMips = -1;
        public int NumMips
        {
            get
            {
                return numMips;
            }
            set
            {
                SetProperty(ref numMips, value);
            }
        }

        ImageEngineFormat format = ImageEngineFormat.Unknown;
        public ImageEngineFormat Format
        {
            get
            {
                return format;
            }
            set
            {
                SetProperty(ref format, value);
            }
        }

        int width = -1;
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

        int height = -1;
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

        public ICommand SelectAllCommand { get; set; }
        public ICommand DeSelectAllCommand { get; set; }
        #endregion Properties

        public AbstractTexInfo()
        {
            PCCs = new MTRangedObservableCollection<PCCEntry>();

            SelectAllCommand = new UsefulThings.WPF.CommandHandler(() => SelectAllPCCs());
            DeSelectAllCommand = new UsefulThings.WPF.CommandHandler(() => DeselectAllPCCs());
        }

        private void SelectAllPCCs()
        {
            foreach (var pcc in PCCs)
                pcc.Using = true;
        }

        private void DeselectAllPCCs()
        {
            foreach (var pcc in PCCs)
                pcc.Using = false;
        }

        public void Search(string text)
        {
            IsSearchVisible = EntryName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                PCCs.AsParallel().Any(pcc => pcc.File.Contains(text, StringComparison.OrdinalIgnoreCase) || pcc.ExpIDString.Contains(text)) ||
                HashString.Contains(text);
        }
    }
}
