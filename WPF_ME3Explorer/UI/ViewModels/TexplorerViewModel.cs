using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using UsefulThings.WPF;
using WPF_ME3Explorer.Textures;
using DLCFileEntry = WPF_ME3Explorer.MEDirectories.DLCFileEntry;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class TexplorerViewModel : MEViewModelBase<TreeTexInfo>
    {
        bool onlyNew = true;
        public bool DLCFilterNewOnly
        {
            get
            {
                return onlyNew;
            }
            set
            {
                SetProperty(ref onlyNew, value);
            }
        }

        public bool Changes { get; set; }

        MTRangedObservableCollection<DLCFileEntry> DLCs { get; set; } = new MTRangedObservableCollection<DLCFileEntry>();
        public ICollectionView DLCItemsView { get; set; }



        public TexplorerViewModel() : base(Properties.Settings.Default.TexplorerGameVersion)
        {
            Trees.Add(new TreeDB(MEExDirecs, 1, MEExDirecs.GameVersion == 1, null, ToolsetRevision, DontLoad: true));
            Trees.Add(new TreeDB(MEExDirecs, 2, MEExDirecs.GameVersion == 2, null, ToolsetRevision, DontLoad: true));
            Trees.Add(new TreeDB(MEExDirecs, 3, MEExDirecs.GameVersion == 3, null, ToolsetRevision, DontLoad: true));

            DLCItemsView = CollectionViewSource.GetDefaultView(DLCs);
            DLCItemsView.Filter = item =>
            {
                if (!DLCFilterNewOnly)
                    return true;

                return !((DLCFileEntry)item).IsVisible;
            };

            BeginTreeLoading();
        }

        async void BeginTreeLoading()
        {
            Busy = true;
            PrimaryStatus = "Loading Trees...";

            await Task.Run(() => base.LoadTrees(Trees, CurrentTree, false));

            // KFreon: Populate game files info with tree info
            GetGameFileInformation();

            foreach (var dlc in DLCs)
                foreach (var file in dlc.Files)
                    file.inTree = CurrentTree.TreeTexes.Any(t => file.FullPath == (t.FolderPath + t.FolderName));

            DLCFilterNewOnly = CurrentTree.Valid;

            PrimaryStatus = "Ready!";
            Busy = false;
        }

        public void UpdateView(Predicate<object> filter)
        {
            ItemsView.Filter = filter;
            ItemsView.Refresh();
        }

        public void GetGameFileInformation()
        {
            if (!MEExDirecs.DoesGameExist(GameVersion))
            {

            }

            DLCs.Clear();
            Func<bool> test = () => DLCFilterNewOnly;
            DLCFileEntry basegame = new DLCFileEntry("BaseGame", test);
            foreach (var item in MEExDirecs.BasegameFiles)
                basegame.Files.Add(new MEDirectories.FileEntry(item));

            List<DLCFileEntry> DLC = GetDLCInformation(test);

            DLCs.Add(basegame);
            DLCs.AddRange(DLC);

            foreach (var dlc in DLCs)
                dlc.CalculateSize();
        }

        public List<DLCFileEntry> GetDLCInformation(Func<bool> getOnlyCheckbox)
        {
            List<DLCFileEntry> tempDLCs = new List<MEDirectories.DLCFileEntry>();
            foreach (string file in MEExDirecs.DLCFiles)
            {
                string DLCName = MEDirectories.MEDirectories.GetDLCNameFromPath(file);
                DLCFileEntry DLC = null;
                if (!tempDLCs.Any(dlc => dlc.Name == DLCName))
                {
                    DLC = new DLCFileEntry(DLCName, getOnlyCheckbox);
                    tempDLCs.Add(DLC);
                }
                else
                    DLC = tempDLCs.First(dlc => dlc.Name == DLCName);

                DLC.Files.Add(new MEDirectories.FileEntry(file));
            }

            return tempDLCs;
        }
    }
}
