using CSharpImageLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Animation;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.PCCObjectsAndBits;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class TexplorerViewModel : MEViewModelBase<TreeTexInfo>
    {
        public bool Changes { get; set; }

        List<DLCEntry> FTSDLCs { get; set; } = new List<DLCEntry>();
        public MTRangedObservableCollection<GameFileEntry> FTSGameFiles { get; set; } = new MTRangedObservableCollection<GameFileEntry>();
        List<AbstractFileEntry> FTSExclusions { get; set; } = new List<AbstractFileEntry>();
        public ICollectionView DLCItemsView { get; set; }
        public ICollectionView ExclusionsItemsView { get; set; }
        public ICollectionView FileItemsView { get; set; }
        ThumbnailWriter ThumbnailWriter = null;
        public MTRangedObservableCollection<string> Errors { get; set; } = new MTRangedObservableCollection<string>();
        public MTRangedObservableCollection<TexplorerTextureFolder> TextureFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();


        bool isTreePanelRequired = true;
        public bool IsTreePanelRequired
        {
            get
            {
                return isTreePanelRequired;
            }
            set
            {
                SetProperty(ref isTreePanelRequired, value);
            }
        }
        


        public TexplorerViewModel() : base()
        {
            DebugOutput.StartDebugger("Texplorer");

            DLCItemsView = CollectionViewSource.GetDefaultView(FTSDLCs);
            DLCItemsView.Filter = item => !((DLCEntry)item).IsChecked;

            FileItemsView = CollectionViewSource.GetDefaultView(FTSGameFiles);
            FileItemsView.Filter = item => !((GameFileEntry)item).IsChecked && !((GameFileEntry)item).FilterOut;

            ExclusionsItemsView = CollectionViewSource.GetDefaultView(FTSExclusions);
            ExclusionsItemsView.Filter = item => ((AbstractFileEntry)item).IsChecked && ((item as GameFileEntry)?.FilterOut != true);

            GameDirecs.GameVersion = Properties.Settings.Default.TexplorerGameVersion;
            OnPropertyChanged(nameof(GameVersion));

            ThumbnailWriter = new ThumbnailWriter(GameDirecs);

            BeginTreeLoading();
        }

        async void BeginTreeLoading()
        {
            Busy = true;
            Status = "Loading Trees...";

            AbstractFileEntry.Updater = new Action(() => UpdateFTS());
            AbstractFileEntry.BasePathLength = GameDirecs.BasePath.Length + 9;  // BasePath is C:\etc\mass effect, also want to remove \BIOGame\.

            await Task.Run(() => base.LoadTrees());

            // KFreon: Populate game files info with tree info
            if (GameDirecs.Files?.Count <= 0)
            {
                DebugOutput.PrintLn($"Game files not found for ME{GameDirecs.GameVersion} at {GameDirecs.PathBIOGame}");
                Status = "Game Files not found!";
                Busy = false;
                return;
            }

            // Populate exclusions areas
            DLCEntry basegame = new DLCEntry("BaseGame", GameDirecs.Files.Where(file => !file.Contains(@"DLC\DLC_") && !file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList());
            FTSDLCs.Add(basegame);
            GetDLCEntries();

            // Add all DLC files to global files list
            foreach (DLCEntry dlc in FTSDLCs)
                FTSGameFiles.AddRange(dlc.Files);

            
            if (CurrentTree.Valid)
            {
                // Add textures to UI textures list.
                Textures.AddRange(CurrentTree.Textures);

                // Put away TreeScan Panel since it isn't required if tree is valid.
                IsTreePanelRequired = false;


                /* Find any existing exclusions from when tree was created.*/
                // Set excluded DLC's checked first
                FTSDLCs.ForEach(dlc => dlc.IsChecked = !dlc.Files.Any(file => CurrentTree.ScannedPCCs.Contains(file.FilePath)));

                // Then set all remaining exlusions
                foreach (DLCEntry dlc in FTSDLCs.Where(dlc => !dlc.IsChecked))
                    dlc.Files.ForEach(file => file.IsChecked = !CurrentTree.ScannedPCCs.Contains(file.FilePath));

                await Task.Run(() => ConstructTree());
            }

            FTSExclusions.AddRange(FTSDLCs);
            FTSExclusions.AddRange(FTSGameFiles);

            UpdateFTS();

            Status = "Ready!";
            Busy = false;
        }

        public void UpdateFTS()
        {
            DLCItemsView.Refresh();
            ExclusionsItemsView.Refresh();
            FileItemsView.Refresh();
        }

        void GetDLCEntries()
        {
            List<string> DLCs = Directory.EnumerateDirectories(GameDirecs.DLCPath).Where(direc => !direc.Contains("_metadata")).ToList();
            foreach(string dlc in DLCs)
            {
                string[] parts = dlc.Split('\\');
                string DLCName = parts.First(part => part.Contains("DLC_"));

                string name = MEDirectories.MEDirectories.GetCommonDLCName(DLCName);
                DLCEntry entry = new DLCEntry(name, GameDirecs.Files.Where(file => file.Contains(DLCName) && !file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList());

                FTSDLCs.Add(entry);
            }
        }

        internal async Task BeginTreeScan()
        {
            Busy = true;

            DebugOutput.PrintLn($"Beginning Tree scan for ME{GameVersion}.");

            // Populate Tree PCCs in light of exclusions
            foreach (GameFileEntry item in FTSGameFiles.Where(file => !file.IsChecked && !file.FilterOut))
                CurrentTree.ScannedPCCs.Add(item.FilePath);

            DebugOutput.PrintLn("Attempting to delete old thumbnails.");

            // Remove any existing thumbnails
            if (File.Exists(GameDirecs.ThumbnailCachePath))
                File.Delete(GameDirecs.ThumbnailCachePath);

            StartTime = Environment.TickCount;

            ThumbnailWriter.BeginAdding();

            await ScanAllPCCs();

            // Reorder ME2 Game files
            if (GameVersion == 2)
            {
                DebugOutput.PrintLn("Reordering ME2 textures...");
                await Task.Run(() => Parallel.ForEach(CurrentTree.Textures, tex => tex.ReorderME2Files()));  // This should be fairly quick so let the runtime deal with threading.
            }

            StartTime = 0; // Stop Elapsed Time from counting
            ThumbnailWriter.FinishAdding();

            // Update UI Texture display
            Textures.AddRange(CurrentTree.Textures);

            DebugOutput.PrintLn("Saving tree to disk...");
            CurrentTree.SaveToFile();

            DebugOutput.PrintLn($"Treescan completed. Elapsed time: {ElapsedTime}. Num Textures: {Textures.Count}.");

            await Task.Run(() => ConstructTree());

            // Put away TreeScanProgress Window
            Storyboard closer = (Storyboard)Application.Current.Resources.FindName("TreeScanProgressPanelCloser");
            closer.Begin();

            Busy = false;
        }


        /// <summary>
        /// Scans PCCs in Tree or given pccs e.g from adding textures to existing tree.
        /// </summary>
        /// <param name="pccs">PCCs to scan (to add to existing tree)</param>
        async Task ScanAllPCCs(List<string> pccs = null)
        {
            Progress = 0;
            MaxProgress = pccs?.Count ?? CurrentTree.ScannedPCCs.Count;
            Status = $"Scanned: 0 / {MaxProgress}";

            IList<string> PCCsToScan = CurrentTree.ScannedPCCs;  // Can't use ?? here as ScannedPCCs and pccs are different classes.
            if (pccs != null)
                PCCsToScan = pccs;

            // Parallel scanning
            ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = NumThreads;
            int count = 0;

            await Task.Run(() => Parallel.For(0, MaxProgress, po, (index, loopstate) =>
            {
                // TODO: Cancellation

                string file = PCCsToScan[index];
                DebugOutput.PrintLn($"Scanning: {file}");

                string error = ScanPCCForTextures(file);  // Ignore errors for now. Might display them later?
                if (error != null)
                    Errors.Add(error);

                Interlocked.Increment(ref count);  // Threadsafely increment count

                // Only change progress and status every 10 or so. Improves UI performance, otherwise it'll be updating the display 40 times a second on top of all the scanning.
                if (count % 10 == 0)
                {
                    Progress += 10;
                    Status = $"Scanning: {count} / {MaxProgress}";
                }
            }));

            Progress = MaxProgress;
            Status = $"Scan complete. Found {CurrentTree.Textures.Count} textures.";
        }

        string ScanPCCForTextures(string filename)
        {
            try
            {
                using (PCCObject pcc = new PCCObject(filename, GameVersion))
                {
                    for (int i = 0; i < pcc.Exports.Count; i++)
                    {
                        ExportEntry export = pcc.Exports[i];
                        if (!export.ValidTextureClass())
                            continue;

                        Texture2D tex2D = new Texture2D(pcc, i, GameVersion);

                        // Skip if no images
                        if (tex2D.ImageList.Count == 0)
                            continue;

                        TreeTexInfo info = new TreeTexInfo(tex2D, ThumbnailWriter, export);
                        CurrentTree.AddTexture(info);
                    }
                }
            }
            catch(Exception e)
            {
                DebugOutput.PrintLn($"Failed to scan PCC: {filename}. Reason: {e.ToString()}");
                return e.Message;
            }

            return null;
        }

        void ConstructTree()
        {
            DebugOutput.PrintLn("Constructing Tree...");

            // Top all encompassing node
            TexplorerTextureFolder TopTextureFolder = new TexplorerTextureFolder("All Texture Files", null);

            List<TexplorerTextureFolder> AllFolders = new List<TexplorerTextureFolder>();

            // Normal nodes
            foreach (var tex in CurrentTree.Textures)
            {
                int dotInd = tex.FullPackage.IndexOf('.') + 1;
                string filter = tex.FullPackage;
                if (dotInd != 0)
                    filter = tex.FullPackage.Substring(0, dotInd).Trim('.');

                TexplorerTextureFolder folder = RecursivelyConstructFolders(tex.FullPackage, filter, AllFolders);
                bool isExisting = RecursivelyCheckFolders(TopTextureFolder, folder);

                if (!isExisting)
                    TopTextureFolder.Folders.Add(folder);
            }

            // Assign textures to their folders as determined above.
            SortTexturesIntoFolders(AllFolders);

            TextureFolders.Add(TopTextureFolder);  // Only one item in this list. Chuckles.
            
            DebugOutput.PrintLn("Tree Constructed!");
        }

        TexplorerTextureFolder RecursivelyConstructFolders(string packageString, string filter, List<TexplorerTextureFolder> AllFolders)
        {
            string tempPackage = packageString.Trim('.');
            int dotInd = tempPackage.IndexOf('.') + 1;
            if (dotInd == 0)
            {
                var fold = new TexplorerTextureFolder(tempPackage, filter);
                AllFolders.Add(fold);
                return fold;
            }

            string name = tempPackage.Substring(0, dotInd).Trim('.');
            TexplorerTextureFolder folder = new TexplorerTextureFolder(name, filter);
            AllFolders.Add(folder);


            string newName = tempPackage.Substring(dotInd);
            string newFilter = filter + '.' + newName;
            folder.Folders.Add(RecursivelyConstructFolders(newName, newFilter, AllFolders));

            return folder;
        }

        bool RecursivelyCheckFolders(TexplorerTextureFolder TopTextureFolder, TexplorerTextureFolder folder)
        {
            if (TopTextureFolder.Folders?.Count < 1) // Empty or null
            {
                // New folder has subfolders
                if (folder.Folders?.Count > 0)
                    TopTextureFolder.Folders.Add(folder);

                // New folder has no subfolders
                return true;    
            }
            else if (folder.Folders?.Count < 1)
                return true;

            foreach (var texFolder in TopTextureFolder.Folders)
            {
                if (texFolder.Name == folder.Name)
                    foreach (var newFolder in folder.Folders)
                        return RecursivelyCheckFolders(texFolder, newFolder);
            }

            return false;
        }

        void SortTexturesIntoFolders(List<TexplorerTextureFolder> AllFolders)
        {
            Parallel.ForEach(CurrentTree.Textures, texture =>
            {
                foreach (var folder in AllFolders)
                {
                    if (folder.Filter == texture.FullPackage)
                    {
                        folder.Textures.Add(texture);
                        break;
                    }
                }
            });
        }
    }
}
