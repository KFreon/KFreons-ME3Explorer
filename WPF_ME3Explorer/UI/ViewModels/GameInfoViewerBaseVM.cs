using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public abstract class BasePathChangerViewModel : ViewModelBase
    {
        #region Properties
        public string ImageURI { get; set; }
        public MEDirectories.MEDirectories MEExDirec = null;
        public virtual int WhichGame { get; set; }
        public virtual string ExePath { get; set; }
        public virtual string BIOGamePath { get; set; }
        public virtual string DLCPath { get; set; }
        public virtual string CookedPath { get; set; }
        public virtual bool AllowExtraMods { get; set; }
        public string TitleText { get; set; }

        public MTObservableCollection<string> DLCs { get; set; }

        #region Commands
        public ICommand ExeBrowseCommand
        {
            get
            {
                return new CommandHandler(() => ExePath = BrowseButton(true) ?? ExePath, true);
            }
        }
        public ICommand BIOGameBrowseCommand
        {
            get
            {
                return new CommandHandler(() => BIOGamePath = BrowseButton(folderName: "BIOGame") ?? BIOGamePath, true);
            }
        }
        public ICommand DLCBrowseCommand
        {
            get
            {
                return new CommandHandler(() => DLCPath = BrowseButton(folderName: "DLC") ?? DLCPath, true);
            }
        }
        public ICommand CookedBrowseCommand
        {
            get
            {
                return new CommandHandler(() => CookedPath = BrowseButton(folderName: "Cooked") ?? CookedPath, true);
            }
        }
        #endregion
        #endregion

        public BasePathChangerViewModel(int game)
        {
            // KFreon: Setup properties
            MEExDirec = new MEDirectories.MEDirectories(game);
            WhichGame = game;

            AllowExtraMods = false;

            try
            {
                ExePath = MEExDirec.ExePath;
                BIOGamePath = MEExDirec.PathBIOGame;
                DLCPath = MEExDirec.DLCPath;
                CookedPath = MEExDirec.pathCooked;

                DLCs = new MTObservableCollection<string>(MEDirectories.MEDirectories.GetInstalledDLC(DLCPath).Select(t => MEDirectories.MEDirectories.GetDLCNameFromPath(t)));
            }
            catch(Exception e)
            {
                DebugOutput.PrintLn("Failed to get paths", "GameInfoViewerBase", e);
            }


            ImageURI = "/WPF_ME3Explorer;component/Resources/Mass Effect " + game + ".jpg";
        }

        public string BrowseButton(bool isExe = false, string folderName = null)
        {
            string retval = null;
            if (isExe)
            {
                string game = WhichGame == 1 ? "" : WhichGame.ToString();

                Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Title = "Select Mass Effect " + game + ".exe";
                ofd.Filter = "MassEffect" + WhichGame + ".exe|MassEffect" + game + ".exe";
                if (ofd.ShowDialog() == true)
                    retval = ofd.FileName;
            }
            else
            {
                var dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                dialog.Title = "Select " + folderName + " location";

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    retval = dialog.FileName;
            }
            return retval;
        }

        internal virtual void Save()
        {
            if (!String.IsNullOrEmpty(BIOGamePath))
                MEExDirec.PathBIOGame = BIOGamePath;

            if (!String.IsNullOrEmpty(CookedPath))
                MEExDirec.pathCooked = CookedPath;

            if (!String.IsNullOrEmpty(ExePath))
                MEExDirec.ExePath = ExePath;

            if (!String.IsNullOrEmpty(DLCPath))
                MEExDirec.DLCPath = DLCPath;

            //MEExDirec.SaveInstanceSettings();
        }
    }
}
