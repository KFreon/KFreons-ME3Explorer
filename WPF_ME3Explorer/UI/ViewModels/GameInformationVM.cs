using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class GameInformationVM : ViewModelBase
    {
        string biogame = null;
        public bool SaveEnabled
        {
            get
            {
                return ThumbnailWriter.IsWriting;
            }
        }

        public GameInformationVM(int version)
        {
            GameVersion = version;
            switch (GameVersion)
            {
                case 1:
                    PathBIOGame = MEDirectories.MEDirectories.ME1BIOGame;
                    break;
                case 2:
                    PathBIOGame = MEDirectories.MEDirectories.ME2BIOGame;
                    break;
                case 3:
                    PathBIOGame = MEDirectories.MEDirectories.ME3BIOGame;
                    break;
            }
        }

        public string PathBIOGame
        {
            get
            {
                return biogame;
            }
            set
            {
                SetProperty(ref biogame, value);
                UpdatePathing();
            }
        }

        public int GameVersion { get; set; }

        public string DLCPath
        {
            get
            {
                if (String.IsNullOrEmpty(PathBIOGame))
                    return null;

                return Path.Combine(GameVersion == 1 ? Path.GetDirectoryName(PathBIOGame) : PathBIOGame, "DLC");
            }
        }

        public string CookedPath
        {
            get
            {
                if (String.IsNullOrEmpty(PathBIOGame))
                    return null;

                return Path.Combine(PathBIOGame, GameVersion == 3 ? "CookedPCConsole" : "CookedPC");
            }
        }

        void UpdatePathing()
        {
            switch (GameVersion)
            {
                case 1:
                    MEDirectories.MEDirectories.ME1BIOGame = PathBIOGame;
                    break;
                case 2:
                    MEDirectories.MEDirectories.ME2BIOGame = PathBIOGame;
                    break;
                case 3:
                    MEDirectories.MEDirectories.ME3BIOGame = PathBIOGame;
                    break;
            }

            OnPropertyChanged(nameof(PathBIOGame));
            OnPropertyChanged(nameof(DLCPath));
            OnPropertyChanged(nameof(CookedPath));
        }

        internal void SavePathing()
        {
            MEDirectories.MEDirectories.SaveSettings();
        }
    }
}
