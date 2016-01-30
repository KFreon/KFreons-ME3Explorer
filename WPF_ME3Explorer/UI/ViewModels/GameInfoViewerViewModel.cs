using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UsefulThings.WPF;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class GameInfoViewModel : BasePathChangerViewModel
    {
        #region Properties
        bool allow = false;
        public override bool AllowExtraMods
        {
            get
            {
                return allow;
            }
            set
            {
                allow = value;
                OnPropertyChanged();
            }
        }

        string exepath = null;
        public override string ExePath
        {
            get
            {
                return exepath;
            }
            set
            {
                exepath = value;

                // KFreon: Adjust other properties if necessary
                if (!AllowExtraMods)
                {
                    BIOGamePath = MEDirectories.MEDirectories.GetBIOGameFromExe(exepath, WhichGame);
                    CookedPath = MEDirectories.MEDirectories.GetCookedFromBIOGame(BIOGamePath, WhichGame);
                    DLCPath = MEDirectories.MEDirectories.GetDLCFromBIOGame(BIOGamePath, WhichGame);
                }
                OnPropertyChanged();
            }
        }

        string bio = null;
        public override string BIOGamePath
        {
            get
            {
                return bio;
            }
            set
            {
                bio = value;
                OnPropertyChanged();
            }
        }

        string cooked = null;
        public override string CookedPath
        {
            get
            {
                return cooked;
            }
            set
            {
                cooked = value;
                OnPropertyChanged();
            }
        }

        string dlc = null;
        public override string DLCPath
        {
            get
            {
                return dlc;
            }
            set
            {
                dlc = value;
                OnPropertyChanged();
            }
        }
        #endregion

        public GameInfoViewModel(string title, int game) : base(game)
        {
            TitleText = title;
        }
    }
}
