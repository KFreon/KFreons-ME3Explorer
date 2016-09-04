#define ThreadedScan

using CSharpImageLibrary;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
        #region Commands
        CommandHandler showGameInfo = null;
        public CommandHandler ShowGameInfoCommand
        {
            get
            {
                if (showGameInfo == null)
                    showGameInfo = new CommandHandler(new Action<object>(param =>
                        {
                            int version = int.Parse((string)param);
                            GameInformation info = new GameInformation(version);
                            info.Closed += (unused1, unused2) => GameDirecs.RefreshListeners();  // Refresh all game directory related info once window is closed. 
                            info.Show();
                        }));

                return showGameInfo;
            }
        }

        CommandHandler changeTree = null;
        public CommandHandler ChangeTreeCommand
        {
            get
            {
                if (changeTree == null)
                    changeTree = new CommandHandler(new Action<object>(param =>
                    {
                        if (ChangedTextures.Count > 0)
                        {
                            var result = MessageBox.Show("There are unsaved changes. Do you want to continue to change trees? Continuing will NOT save changes made.", "You sure about this, Shepard?", MessageBoxButton.YesNo);
                            if (result == MessageBoxResult.No)
                                return;
                        }
                        int version = ((TreeDB)param).GameVersion;
                        ChangeSelectedTree(version);
                        LoadFTSandTree();
                    }));

                return changeTree;
            }
        }
        #endregion Commands

        #region UI Actions
        public Action TreePanelCloser = null;
        public Action ProgressOpener = null;
        public Action TreePanelOpener = null;
        public Action ProgressCloser = null;
        #endregion UI Actions

        #region Properties
        List<DLCEntry> FTSDLCs { get; set; } = new List<DLCEntry>();
        public MTRangedObservableCollection<GameFileEntry> FTSGameFiles { get; set; } = new MTRangedObservableCollection<GameFileEntry>();
        List<AbstractFileEntry> FTSExclusions { get; set; } = new List<AbstractFileEntry>();
        public ICollectionView DLCItemsView { get; set; }
        public ICollectionView ExclusionsItemsView { get; set; }
        public ICollectionView FileItemsView { get; set; }
        ThumbnailWriter ThumbnailWriter = null;
        public MTRangedObservableCollection<string> Errors { get; set; } = new MTRangedObservableCollection<string>();
        public MTRangedObservableCollection<TexplorerTextureFolder> TextureFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();
        public MTRangedObservableCollection<TexplorerTextureFolder> AllFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();
        public MTRangedObservableCollection<TreeTexInfo> ChangedTextures { get; set; } = new MTRangedObservableCollection<TreeTexInfo>();

        TexplorerTextureFolder mySelected = null;
        public TexplorerTextureFolder SelectedFolder
        {
            get
            {
                return mySelected;
            }
            set
            {
                SetProperty(ref mySelected, value);
            }
        }

        TreeTexInfo selectedTexture = null;
        public TreeTexInfo SelectedTexture
        {
            get
            {
                return selectedTexture;
            }
            set
            {
                SetProperty(ref selectedTexture, value);
            }
        }

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

        #endregion Properties

        public override void ChangeSelectedTree(int game)
        {
            Status = $"Selected Tree changed from {GameVersion} to {game}.";

            base.ChangeSelectedTree(game);

            RefreshTreeRelatedProperties();
        }

        public TexplorerViewModel() : base()
        {
            DebugOutput.StartDebugger("Texplorer");

            // Setup filtering on FTS views
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

            #region Setup Texture UI Commands
            TreeTexInfo.ChangeCommand = new CommandHandler(new Action<object>(tex =>
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "DirectX Images|*.dds";  // TODO Expand to allow any ImageEngine supported format for on the fly conversion. Need to have some kind of preview first though to maybe change the conversion parameters.
                if (ofd.ShowDialog() != true)
                    return;

                ChangeTexture((TreeTexInfo)tex, ofd.FileName);
            }));

            TreeTexInfo.ExtractCommand = new CommandHandler(new Action<object>(tex =>
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = null;
                sfd.Filter = "DirectX Images|*.dds";  // TODO Expand to allow any ImageEngine supported format.
                if (sfd.ShowDialog() != true)
                    return;

                ExtractTexture((TreeTexInfo)tex, sfd.FileName);
            }));

            TreeTexInfo.LowResFixCommand = new CommandHandler(new Action<object>(tex =>
            {
                ME1_LowResFix((TreeTexInfo)tex);
            }));

            TreeTexInfo.RegenerateThumbCommand = new CommandHandler(new Action<object>(async tex =>
            {
                await Task.Run(() =>RegenerateThumbs((TreeTexInfo)tex)).ConfigureAwait(false);
            }));

            TexplorerTextureFolder.RegenerateThumbsDelegate = RegenerateThumbs;

            #endregion Setup UI Commands


            // Setup thumbnail writer - not used unless tree scanning.
            ThumbnailWriter = new ThumbnailWriter(GameDirecs);
            BeginTreeLoading();
        }

        internal void RegenerateThumbs(params TreeTexInfo[] textures)
        {
            Busy = true;
            List<TreeTexInfo> texes = new List<TreeTexInfo>();

            // No args = regen everything
            if (textures?.Length < 1)
                texes.AddRange(Textures);

            // Open Progress Panel if worth it.
            if (texes.Count > 10)
                ProgressOpener();

            DebugOutput.PrintLn($"Regenerating {texes.Count} thumbnails...");

            ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = NumThreads;
            MaxProgress = texes.Count;
            Progress = 0;

            ThumbnailWriter.BeginAdding();
            int errors = 0;
            Parallel.ForEach(texes, po, tex =>
            {
                try
                {
                    Status = $"Regenerating Thumbnails: {Progress} of {MaxProgress}";
                    tex.Thumb = ThumbnailWriter.ReplaceOrAdd(tex);
                    Progress++;
                }
                catch (Exception e)
                {
                    errors++;
                    DebugOutput.PrintLn($"Unable to regenerate thumbnail for {tex.TexName}. Reason: {e.ToString()}.");
                }
            });

            Status = $"Regenerated {texes.Count - errors} thumbnails" + (errors == 0 ? "." : $" with {errors} errors.");
            Progress = MaxProgress;

            ThumbnailWriter.FinishAdding();

            // Close Progress Panel if previously opened.
            if (texes.Count > 10)
                ProgressCloser();

            Busy = false;
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

            await LoadFTSandTree();

            ToolsetInfo.SetupDiskCounters(Path.GetPathRoot(GameDirecs.BasePath).TrimEnd('\\'));

            Status = CurrentTree.Valid ? "Ready!" : Status; 
            Busy = false;
        }

        async Task LoadFTSandTree(bool panelAlreadyOpen = false)
        {
            FTSReady = false;

            // Clear everything to start again.
            FTSExclusions.Clear();
            FTSGameFiles.Clear();
            FTSDLCs.Clear();

            // Tree isn't valid. Open the panel immediately.
            if (!panelAlreadyOpen && !CurrentTree.Valid)
                TreePanelOpener();

            await Task.Run(() =>
            {
                // Get DLC's
                DLCEntry basegame = new DLCEntry("BaseGame", GameDirecs.Files.Where(file => !file.Contains(@"DLC\DLC_") && !file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList());
                FTSDLCs.Add(basegame);
                GetDLCEntries();
            });

            // Add all DLC files to global files list
            foreach (DLCEntry dlc in FTSDLCs)
                FTSGameFiles.AddRange(dlc.Files);

            if (CurrentTree.Valid)
            {
                await LoadValidTree();

                /* Find any existing exclusions from when tree was created.*/
                // Set excluded DLC's checked first
                FTSDLCs.ForEach(dlc => dlc.IsChecked = !dlc.Files.Any(file => CurrentTree.ScannedPCCs.Contains(file.FilePath)));

                // Then set all remaining exlusions
                foreach (DLCEntry dlc in FTSDLCs.Where(dlc => !dlc.IsChecked))
                    dlc.Files.ForEach(file => file.IsChecked = !CurrentTree.ScannedPCCs.Contains(file.FilePath));
            }
            else
                Status = "Tree invalid/non-existent. Begin Tree Scan by clicking 'Settings'";

            FTSExclusions.AddRange(FTSDLCs);
            FTSExclusions.AddRange(FTSGameFiles);

            FTSReady = true;
            UpdateFTS();
        }

        async Task LoadValidTree()
        {
            // Put away TreeScan Panel since it isn't required if tree is valid.
            if (TreePanelCloser != null)
                TreePanelCloser();

            CurrentTree.IsSelected = true;

            await Task.Run(() => ConstructTree());
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

            await BeginScanningPCCs();

            // Reorder ME2 Game files - DISABLED FOR NOW - Think it should be in the loader for Texture2D. Think I was hoping I could intialise things with this.
            if (GameVersion == 2)
            {
                DebugOutput.PrintLn("Reordering ME2 textures...");
                //await Task.Run(() => Parallel.ForEach(CurrentTree.Textures, tex => tex.ReorderME2Files())).ConfigureAwait(false);  // This should be fairly quick so let the runtime deal with threading.
            }

            StartTime = 0; // Stop Elapsed Time from counting
            ThumbnailWriter.FinishAdding();

            DebugOutput.PrintLn("Saving tree to disk...");
            await Task.Run(() => CurrentTree.SaveToFile()).ConfigureAwait(false);

            DebugOutput.PrintLn($"Treescan completed. Elapsed time: {ElapsedTime}. Num Textures: {CurrentTree.Textures.Count}.");

            await Task.Run(() => ConstructTree()).ConfigureAwait(false);

            CurrentTree.Valid = true; // It was just scanned after all.

            // Put away TreeScanProgress Window
            ProgressCloser();

            GC.Collect();  // On a high RAM x64 system, things sit around at like 6gb. Might as well clear it.

            Busy = false;
        }

        async Task<KeyValuePair<string, MemoryStream>> LoadTFC(string tfc)
        {
            using (FileStream fs = new FileStream(tfc, FileMode.Open, FileAccess.Read, FileShare.None, 4096, true))
            {
                //MemoryStream ms = RecyclableMemoryManager.GetStream((int)fs.Length);
                MemoryStream ms = new MemoryStream((int)fs.Length);
                await fs.CopyToAsync(ms).ConfigureAwait(false);
                return new KeyValuePair<string, MemoryStream>(tfc, ms);
            }
        }

        /// <summary>
        /// Scans PCCs in Tree or given pccs e.g from adding textures to existing tree.
        /// </summary>
        /// <param name="pccs">PCCs to scan (to add to existing tree)</param>
        async Task BeginScanningPCCs(List<string> pccs = null)
        {
            Progress = 0;
            MaxProgress = pccs?.Count ?? CurrentTree.ScannedPCCs.Count;

            IList<string> PCCsToScan = CurrentTree.ScannedPCCs;  // Can't use ?? here as ScannedPCCs and pccs are different classes.
            if (pccs != null)
                PCCsToScan = pccs;

            //ME3 only
            Dictionary<string, MemoryStream> TFCs = null;
            if (GameVersion != 1)
            {
                Status = "Reading TFC's into memory...";

                // Read TFCs into RAM if available/requested.
                double RAMinGB = ToolsetInfo.AvailableRam / 1024d / 1024d / 1024d;
                if (Environment.Is64BitProcess && RAMinGB > 10)
                {
                    // Enough RAM to load TFCs
                    TFCs = new Dictionary<string, MemoryStream>();

                    var tfcfiles = GameDirecs.Files.Where(tfc => tfc.EndsWith("tfc"));
                    foreach (var tfc in tfcfiles)
                    {
                        var item = await LoadTFC(tfc);
                        TFCs.Add(item.Key, item.Value);
                    }
                }
            }


            // DEBUGGING
            /*PCCObject pcc = new PCCObject(@"R:\Games\Mass Effect\BIOGame\CookedPC\Maps\ICE\DSG\BIOA_ICE20_01leave_DSG.SFM", 1);
            await PCCConsumer(pcc, null);*/

            Status = "Beginning Scan...";

            // Perform scan
            var tasks = ScanAllPCCs(PCCsToScan, TFCs);   // Start entire thing on another thread which awaits when collection is full, but task itself should complete when producers complete, then have to wait for consumers.
            await Task.WhenAll(tasks).ConfigureAwait(false);

            // Dispose all TFCs
            if (TFCs != null)
            {
                foreach (var tfc in TFCs)
                    tfc.Value.Dispose();

                TFCs.Clear();
                TFCs = null;
            }
                
            Console.WriteLine($"Max ram during scan: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024d / 1024d / 1024d}");
            DebugOutput.PrintLn($"Max ram during scan: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024d / 1024d / 1024d}");

            Progress = MaxProgress;
            Status = $"Scan complete. Found {CurrentTree.Textures.Count} textures. Elapsed scan time: {ElapsedTime}.";
        }

        Task[] ScanAllPCCs(IList<string> PCCs, Dictionary<string, MemoryStream> TFCs)
        {
            // Parallel scanning
            int bound = 10;
            double RAMinGB = ToolsetInfo.AvailableRam / 1024d / 1024d / 1024d;
            if (RAMinGB < 10)
                bound = 5;

            BufferBlock<PCCObject> pccs = new BufferBlock<PCCObject>(new DataflowBlockOptions { BoundedCapacity = bound });   // Collection can't grow past this. Good for low RAM situations.


            // Begin consumer
            int numConsumers = NumThreads / 2;
            if (numConsumers < 1)
                numConsumers = 1;

#if (!ThreadedScan)
            numConsumers = 1;
#endif

            var consumerOptions = new ExecutionDataflowBlockOptions { BoundedCapacity = numConsumers, MaxDegreeOfParallelism = numConsumers };
            var consumer = new ActionBlock<PCCObject>(async pcc => await PCCConsumer(pcc, TFCs), consumerOptions);
            pccs.LinkTo(consumer, new DataflowLinkOptions { PropagateCompletion = true });

            // Begin producer
            var producer = PCCProducer(PCCs, pccs);

            // Wait for consumers to finish
            return new Task[] { consumer.Completion, producer};
        }

        async Task PCCProducer(IList<string> PCCs, BufferBlock<PCCObject> pccs)
        {
            for (int i = 0; i < PCCs.Count; i++)
            {
                string file = PCCs[i];
                DebugOutput.PrintLn($"Scanning: {file}");
                PCCObject pcc = await PCCObject.CreateAsync(file, GameVersion);

                await pccs.SendAsync(pcc);
            }

            pccs.Complete();
        }

        async Task PCCConsumer(PCCObject pcc, Dictionary<string, MemoryStream> TFCs)
        {
            try
            {
                ParallelOptions po = new ParallelOptions();
                po.MaxDegreeOfParallelism = NumThreads;


#if (ThreadedScan)
                await Task.Run(() => Parallel.For(0, pcc.Exports.Count, po, i =>
#else
                for (int i = 0; i < pcc.Exports.Count; i++)
#endif
                {
                    ExportEntry export = pcc.Exports[i];
                    if (!export.ValidTextureClass())
                    {
#if (ThreadedScan)
                        return;
#else
                        continue;
#endif
                    }

                    Texture2D tex2D = null;
                    try
                    {
                        tex2D = new Texture2D(pcc, i, GameVersion);
                    }
                    catch(Exception e)
                    {
                        Errors.Add(e.ToString());
#if (ThreadedScan)
                        return;
#else
                        continue;
#endif
                    }

                    // Skip if no images
                    if (tex2D.ImageList.Count == 0)
                    {
                        tex2D.Dispose();
#if (ThreadedScan)
                        return;
#else
                        continue;
#endif
                    }

                    try
                    {
                        TreeTexInfo info = new TreeTexInfo(tex2D, ThumbnailWriter, export, TFCs, Errors);
                        CurrentTree.AddTexture(info);
                    }
                    catch(Exception e)
                    {
                        Errors.Add(e.ToString());
                    }
#if (ThreadedScan)
                })).ConfigureAwait(false);
#else
                }
#endif
                    
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn($"Scanning failed on {pcc.pccFileName}. Reason: {e.ToString()}.");
            }
            finally
            {
                pcc.Dispose();
            }

            /*Interlocked.Increment(ref count);  // Threadsafely increment count

            // Only change progress and status every 10 or so. Improves UI performance, otherwise it'll be updating the display 40 times a second on top of all the scanning.
            if (count % 10 == 0)
            {
                Progress += 10;
                Status = $"Scanning: {count} / {MaxProgress}";
            }*/

            Progress++;
            Status = $"Scanning: {Progress} / {MaxProgress}";         
        }

        void ConstructTree()
        {
            Busy = true;
            DebugOutput.PrintLn("Constructing Tree...");

            // Top all encompassing node
            TexplorerTextureFolder TopTextureFolder = new TexplorerTextureFolder("All Texture Files", null, null);

            // Normal nodes
            foreach (var tex in CurrentTree.Textures)
                RecursivelyCreateFolders(tex.FullPackage, "", TopTextureFolder, tex);

            Console.WriteLine($"Total number of folders: {AllFolders.Count}");
            // Alphabetical order
            TopTextureFolder.Folders = new MTRangedObservableCollection<TexplorerTextureFolder>(TopTextureFolder.Folders.OrderBy(p => p));

            TextureFolders.Add(TopTextureFolder);  // Only one item in this list. Chuckles.

            // Add textures to base class list - mostly just for searching.
            Textures.Clear();
            Textures.AddRange(CurrentTree.Textures);

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

            TexplorerTextureFolder newFolder = new TexplorerTextureFolder(name, filter, topFolder);

            // Add texture if part of this folder
            if (newFolder.Filter == texture.FullPackage)
                newFolder.Textures.Add(texture);

            TexplorerTextureFolder existingFolder = topFolder.Folders.FirstOrDefault(folder => newFolder.Name == folder.Name);
            if (existingFolder == null)  // newFolder not found in existing folders
            {
                topFolder.Folders.Add(newFolder);
                AllFolders.Add(newFolder);

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

        void ChangeTexture(TreeTexInfo tex, string filename)
        {
            Busy = true;
            Status = $"Changing Texture: {tex.TexName}...";


            bool success = ToolsetTextureEngine.ChangeTexture(tex, filename);
            if (success)
            {
                // Add only if not already added.
                if (!ChangedTextures.Contains(tex))
                    ChangedTextures.Add(tex);
            }

            Status = $"Texture: {tex.TexName} changed!";
            Busy = false;
        }

        void ExtractTexture(TreeTexInfo tex, string filename)
        {
            Busy = true;
            Status = $"Extracting Texture: {tex.TexName}...";

            string error = null;
            try
            {
                ToolsetTextureEngine.ExtractTexture(tex, filename);
            }
            catch (Exception e)
            {
                error = e.Message;
                DebugOutput.PrintLn($"Extracting image {tex.TexName} failed. Reason: {e.ToString()}");
            }


            Busy = false;
            Status = $"Texture: {tex.TexName} " + (error != null ? $"extracted to {filename}!" : $"failed to extract. Reason: {error}.");
        }

        internal void ME1_LowResFix(TreeTexInfo tex)
        {
            Busy = true;
            Status = $"Applying Low Res Fix to {tex.TexName}.";

            string error = null;
            try
            {
                ToolsetTextureEngine.ME1_LowResFix(tex);
            }
            catch(Exception e)
            {
                error = e.Message;
                DebugOutput.PrintLn($"Low Res Fix failed for {tex.TexName}. Reason: {e.ToString()}.");
            }

            Status = error != null ? $"Applied Low Res Fix to {tex.TexName}." : $"Failed to apply Low Res Fix to {tex.TexName}. Reason: {error}.";
            Busy = false;
        }

        void RefreshTreeRelatedProperties()
        {
            // Clear texture folders
            TextureFolders.Clear();
            AllFolders.Clear();
            ChangedTextures.Clear();
            Errors.Clear();
            PreviewImage = null;
            SelectedFolder = null;
            SelectedTexture = null;
            ShowingPreview = false;
            Textures.Clear();  // Just in case

            Properties.Settings.Default.TexplorerGameVersion = GameVersion;
            Properties.Settings.Default.Save();
        }

        internal void DeleteCurrentTree()
        {
            CurrentTree.Delete();
            CurrentTree.Clear(true);

            RefreshTreeRelatedProperties();

            LoadFTSandTree(true);
        }
    }
}
