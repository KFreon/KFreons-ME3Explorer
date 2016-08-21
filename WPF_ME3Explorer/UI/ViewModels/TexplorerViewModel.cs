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
using System.Windows.Media.Imaging;
using UsefulThings;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.PCCObjectsAndBits;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class TexplorerViewModel : MEViewModelBase<TreeTexInfo>
    {
        public Action TreePanelCloser = null;
        public Action TreeScanProgressCloser = null;

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

        string ftsFilesSearch = null;
        public string FTSFilesSearch
        {
            get
            {
                return ftsFilesSearch;
            }
            set
            {
                SetProperty(ref ftsFilesSearch, value);
                FileItemsView.Refresh();
            }
        }

        string ftsExclusionsSearch = null;
        public string FTSExclusionsSearch
        {
            get
            {
                return ftsExclusionsSearch;
            }
            set
            {
                SetProperty(ref ftsExclusionsSearch, value);
                ExclusionsItemsView.Refresh();
            }
        }
        

        bool showingPreview = false;
        public bool ShowingPreview
        {
            get
            {
                return showingPreview;
            }
            set
            {
                SetProperty(ref showingPreview, value);
            }
        }

        BitmapSource previewImage = null;
        public BitmapSource PreviewImage
        {
            get
            {
                return previewImage;
            }
            set
            {
                SetProperty(ref previewImage, value);
            }
        }

        bool ftsReady = false;
        public bool FTSReady
        {
            get
            {
                return ftsReady;
            }
            set
            {
                SetProperty(ref ftsReady, value);
            }
        }


        public TexplorerViewModel() : base()
        {
            DebugOutput.StartDebugger("Texplorer");

            DLCItemsView = CollectionViewSource.GetDefaultView(FTSDLCs);
            DLCItemsView.Filter = item => !((DLCEntry)item).IsChecked;

            FileItemsView = CollectionViewSource.GetDefaultView(FTSGameFiles);
            FileItemsView.Filter = item =>
            {
                GameFileEntry entry = (GameFileEntry)item;
                return !entry.IsChecked && !entry.FilterOut && 
                    (String.IsNullOrEmpty(FTSFilesSearch) ? true : 
                    entry.Name.Contains(FTSFilesSearch, StringComparison.OrdinalIgnoreCase) || entry.FilePath?.Contains(FTSFilesSearch, StringComparison.OrdinalIgnoreCase) == true);
            };

            ExclusionsItemsView = CollectionViewSource.GetDefaultView(FTSExclusions);
            ExclusionsItemsView.Filter = item =>
            {
                AbstractFileEntry entry = (AbstractFileEntry)item;
                return entry.IsChecked && ((entry as GameFileEntry)?.FilterOut != true) && 
                    (String.IsNullOrEmpty(FTSExclusionsSearch) ? true : 
                    entry.Name.Contains(FTSExclusionsSearch, StringComparison.OrdinalIgnoreCase) || entry.FilePath?.Contains(FTSExclusionsSearch, StringComparison.OrdinalIgnoreCase) == true);
            };

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

            await Task.Run(() =>
            {
                // Load all three trees
                base.LoadTrees();

                /// Can take a long time if disk is busy
                // KFreon: Populate game files info with tree info
                if (GameDirecs.Files?.Count <= 0)
                {
                    DebugOutput.PrintLn($"Game files not found for ME{GameDirecs.GameVersion} at {GameDirecs.PathBIOGame}");
                    Status = "Game Files not found!";
                    Busy = false;
                    return;
                }
            });

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
                if (TreePanelCloser != null)
                    TreePanelCloser();


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

            FTSReady = true;
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
            if (TreeScanProgressCloser != null)
                TreeScanProgressCloser();

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

            // Remove localisations - english only for now.
            IList<string> PCCsToScan = CurrentTree.ScannedPCCs.Where(file => !file.Contains("_LOC_")).ToList();  // Can't use ?? here as ScannedPCCs and pccs are different classes.
            if (pccs != null)
                PCCsToScan = pccs;

            


            //////// Basegame
            // Read in TFC's
            Dictionary<string, MemoryStream> TFCs = new Dictionary<string, MemoryStream>();
            var tfcFiles = GameDirecs.Files.Where(file => file.EndsWith("tfc"));// && !file.Contains("DLC\\DLC_"));

            await Task.Run(() =>
            {
                foreach (var tfc in tfcFiles)
                {
                    using (FileStream fs = new FileStream(tfc, FileMode.Open))
                    {
                        MemoryStream ms = RecyclableMemoryManager.GetStream((int)fs.Length);
                        ms.ReadFrom(fs, fs.Length);
                        TFCs.Add(tfc, ms);
                    }
                }
            });


            // Get PCC's
            IList<string> basegamePCCs = PCCsToScan;//.Where(pcc => !pcc.Contains("DLC\\DLC_")).ToList();

            // Perform scan - count goes in, gets updated internally and locally, thus returns the updated result for use out here.
            int count = await ScanPCCsInternal(basegamePCCs, 0, TFCs);

            Progress = MaxProgress;
            Status = $"Scan complete. Found {CurrentTree.Textures.Count} textures.";
        }

        async Task<int> ScanPCCsInternal(IList<string> PCCs, int count, Dictionary<string, MemoryStream> TFCs)
        {
            // Parallel scanning
            ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = NumThreads;

            await Task.Run(() => Parallel.For(0, PCCs.Count, po, (index, loopstate) =>
            {
                // TODO: Cancellation

                string file = PCCs[index];
                DebugOutput.PrintLn($"Scanning: {file}");

                string error = ScanPCCForTextures(file, TFCs);  // Ignore errors for now. Might display them later?
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

            return count;
        }

        string ScanPCCForTextures(string filename, Dictionary<string, MemoryStream> TFCs)
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

                        TreeTexInfo info = new TreeTexInfo(tex2D, ThumbnailWriter, export, TFCs);
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
            Busy = true;
            DebugOutput.PrintLn("Constructing Tree...");

            // Top all encompassing node
            TexplorerTextureFolder TopTextureFolder = new TexplorerTextureFolder("All Texture Files", null);

            // Normal nodes
            foreach (var tex in CurrentTree.Textures)
                RecursivelyCreateFolders(tex.FullPackage, "", TopTextureFolder, tex);

            // Alphabetical order
            TopTextureFolder.Folders = new MTRangedObservableCollection<TexplorerTextureFolder>(TopTextureFolder.Folders.OrderBy(p => p));

            TextureFolders.Add(TopTextureFolder);  // Only one item in this list. Chuckles.
            
            DebugOutput.PrintLn("Tree Constructed!");
            Busy = false;
        }

        void RecursivelyCreateFolders(string package, string oldFilter, TexplorerTextureFolder topFolder, TreeTexInfo texture)
        {
            int dotInd = package.IndexOf('.') + 1;
            string name = package;
            if (dotInd != 0)
                name = package.Substring(0, dotInd).Trim('.');

            string filter = oldFilter + '.' + name;
            filter = filter.Trim('.');

            TexplorerTextureFolder newFolder = new TexplorerTextureFolder(name, filter);

            // Add texture if part of this folder
            if (newFolder.Filter == texture.FullPackage)
                newFolder.Textures.Add(texture);

            TexplorerTextureFolder existingFolder = topFolder.Folders.FirstOrDefault(folder => newFolder.Name == folder.Name);
            if (existingFolder == null)  // newFolder not found in existing folders
            {
                topFolder.Folders.Add(newFolder);

                // No more folders in package
                if (dotInd == 0)
                    return;

                string newPackage = package.Substring(dotInd).Trim('.');
                RecursivelyCreateFolders(newPackage, filter, newFolder, texture);
            }
            else
            {  // No subfolders for newFolder yet, need to make them if there are any

                // Add texture if necessary
                if (existingFolder.Filter == texture.FullPackage)
                    existingFolder.Textures.Add(texture);

                // No more folders in package
                if (dotInd == 0)
                    return;

                string newPackage = package.Substring(dotInd).Trim('.');
                RecursivelyCreateFolders(newPackage, filter, existingFolder, texture);
            }
        }

        internal void LoadPreview(TreeTexInfo texInfo)
        {
            using (PCCObject pcc = new PCCObject(texInfo.PCCS[0].Name, GameVersion))
            {
                using (Texture2D tex2D = new Texture2D(pcc, texInfo.PCCS[0].ExpID, GameVersion))
                {
                    byte[] img = tex2D.ExtractMaxImage();
                    using (ImageEngineImage jpg = new ImageEngineImage(img))
                        PreviewImage = jpg.GetWPFBitmap();

                    img = null;
                }
            }
        }
    }
}
