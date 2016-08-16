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
using System.Windows.Data;
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
            DLCEntry basegame = new DLCEntry("BaseGame", GameDirecs.Files.Where(file => !file.Contains(@"DLC\DLC_")).ToList());
            FTSDLCs.Add(basegame);
            GetDLCEntries();

            // Add all DLC files to global files list
            foreach (DLCEntry dlc in FTSDLCs)
                FTSGameFiles.AddRange(dlc.Files);

            // Find any existing exclusions from when tree was created.
            if (CurrentTree.Valid)
            {
                // Set excluded DLC's checked first
                FTSDLCs.ForEach(dlc => dlc.IsChecked = !dlc.Files.Any(file => CurrentTree.ScannedPCCs.Contains(file.FilePath)));

                // Then set all remaining exlusions
                foreach (DLCEntry dlc in FTSDLCs.Where(dlc => !dlc.IsChecked))
                    dlc.Files.ForEach(file => file.IsChecked = !CurrentTree.ScannedPCCs.Contains(file.FilePath));
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
                DLCEntry entry = new DLCEntry(name, GameDirecs.Files.Where(file => file.Contains(DLCName)).ToList());

                FTSDLCs.Add(entry);
            }
        }

        internal async Task BeginTreeScan()
        {
            Busy = true;

            // Populate Tree PCCs in light of exclusions
            foreach (GameFileEntry item in FTSGameFiles.Where(file => !file.IsChecked && !file.FilterOut))
                CurrentTree.ScannedPCCs.Add(item.FilePath);

            // Remove any existing thumbnails
            if (File.Exists(GameDirecs.ThumbnailCachePath))
                File.Delete(GameDirecs.ThumbnailCachePath);

            StartTime = Environment.TickCount;

            ThumbnailWriter.BeginAdding();

            await ScanAllPCCs();

            // Reorder ME2 Game files
            if (GameVersion == 2)
                await Task.Run(() => Parallel.ForEach(Textures, tex => tex.ReorderME2Files(GameDirecs.PathBIOGame)));  // This should be fairly quick so let the runtime deal with threading.

            StartTime = 0; // Stop Elapsed Time from counting

            ThumbnailWriter.FinishAdding();

            Busy = false;
        }


        /// <summary>
        /// Scans PCCs in Tree or given pccs e.g from adding textures to existing tree.
        /// </summary>
        /// <param name="pccs">PCCs to scan (to add to existing tree)</param>
        async Task ScanAllPCCs(List<string> pccs = null)
        {
            // Disable threading on ImageEngine to prevent a thread forest
            ImageEngine.EnableThreading = false;

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

            ImageEngine.EnableThreading = true;
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

                        Texture2D tex2D = new Texture2D(pcc, i, GameDirecs.PathBIOGame, GameVersion);

                        // Skip if no images
                        if (tex2D.ImageList.Count == 0)
                            continue;

                        TreeTexInfo info = new TreeTexInfo(tex2D, ThumbnailWriter, export);
                        Textures.Add(info);
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
    }
}
