#define ThreadedScan
#undef ThreadedScan

using CSharpImageLibrary;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        CommandHandler ftsDLCsUnCheckAll = null;
        public CommandHandler FTSDLCsUnCheckAll
        {
            get
            {
                if (ftsDLCsUnCheckAll == null)
                    ftsDLCsUnCheckAll = new CommandHandler(() => FTSDLCs?.ForEach(dlc => dlc.IsChecked = false));

                return ftsDLCsUnCheckAll;
            }
        }

        CommandHandler saveChangesCommand = null;
        public CommandHandler SaveChangesCommand
        {
            get
            {
                if (saveChangesCommand == null)
                    saveChangesCommand = new CommandHandler(new Action<object>(async param =>
                    {
                        Busy = true;

                        var texes = ChangedTextures.ToArray();

                        // Show progress panel
                        if (texes.Length > 5)
                            ProgressOpener();
                        else
                            ProgressIndeterminate = true;

                        // Install changed textures
                        await Task.Run(async () => await ToolsetTextureEngine.InstallTextures(NumThreads, this, GameDirecs, cts, texes));

                        MaxProgress = Progress;
                        Status = $"Saved all files!";


                        ThumbnailWriter.BeginAdding();

                        // Update thumbnails
                        foreach (var tex in ChangedTextures)
                            tex.Thumb = ThumbnailWriter.ReplaceOrAdd(tex.Thumb.StreamThumb, tex.Thumb);

                        ThumbnailWriter.FinishAdding();

                        // Refresh thumbnail display
                        foreach (var tex in ChangedTextures)
                            tex.HasChanged = false;


                        ChangedTextures.Clear();


                        // Update tree - thumb change only
                        CurrentTree.SaveToFile();

                        // Close progress
                        if (texes.Length > 5)
                            ProgressCloser();
                        else
                            ProgressIndeterminate = false;

                        Busy = false;
                    }));

                return saveChangesCommand;
            }
        }

        public override CommandHandler ChangeTreeCommand
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

        CommandHandler exportTexAndInfoCommand = null;
        public CommandHandler ExportTexAndInfoCommand
        {
            get
            {
                if (exportTexAndInfoCommand ==null)
                    exportTexAndInfoCommand = new CommandHandler(new Action<object>(param =>
                    {
                        var tex = (TreeTexInfo)param;

                        SaveFileDialog sfd = new SaveFileDialog();
                        sfd.FileName = Path.GetFileNameWithoutExtension(tex.DefaultSaveName) + ".zip";
                        sfd.Filter = "Compressed Files|*.zip";
                        if (sfd.ShowDialog() != true)
                            return;

                        using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create))
                        {
                            using (ZipArchive zipper = new ZipArchive(fs, ZipArchiveMode.Update)) // Just a container
                            {
                                // Extract texture itself.
                                var imgData = ToolsetTextureEngine.ExtractTexture(tex);

                                // Put texture into zip archive
                                var img = zipper.CreateEntry(tex.DefaultSaveName);
                                using (Stream sw = img.Open())
                                    sw.Write(imgData, 0, imgData.Length);

                                // Create details csv
                                string details = BuildTexDetailsForCSV(tex);

                                // Put details into zip archive
                                var csv = zipper.CreateEntry($"{Path.GetFileNameWithoutExtension(tex.DefaultSaveName)}_details.csv");
                                using (Stream sw = csv.Open())
                                {
                                    var arr = Encoding.Default.GetBytes(details);
                                    sw.Write(arr, 0, arr.Length);
                                }
                            }
                        }
                    }));

                return exportTexAndInfoCommand;
            }
        }

        CommandHandler startTPFToolsModeCommand = null;
        public CommandHandler StartTPFToolsModeCommand
        {
            get
            {
                if (startTPFToolsModeCommand == null)
                {
                    startTPFToolsModeCommand = new CommandHandler(() =>
                    {
                        // Start TPFTools
                        var tpftools = ToolsetInfo.TPFToolsInstance;

                        // Open window if necessary, minimising it either way.
                        tpftools.WindowState = WindowState.Minimized;
                        if (tpftools.Visibility != Visibility.Visible)
                            tpftools.Show();

                        // Change mode indicator
                        TPFToolsModeEnabled = true;
                    });
                }

                return startTPFToolsModeCommand;
            }
        }

        CommandHandler stopTPFToolsModeCommand = null;
        public CommandHandler StopTPFToolsModeCommand
        {
            get
            {
                if (stopTPFToolsModeCommand == null)
                {
                    stopTPFToolsModeCommand = new CommandHandler(() =>
                    {
                        // Start TPFTools
                        var tpftools = ToolsetInfo.TPFToolsInstance;

                        // Show TPFTools window
                        tpftools.WindowState = WindowState.Normal;
                        tpftools.Activate();


                        // Change mode indicator
                        TPFToolsModeEnabled = false;
                    });
                }

                return stopTPFToolsModeCommand;
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
        #region Caches
        List<DLCEntry> ME1FTSDLCs { get; set; } = new List<DLCEntry>();
        List<GameFileEntry> ME1FTSGameFiles { get; set; } = new List<GameFileEntry>();
        List<AbstractFileEntry> ME1FTSExclusions { get; set; } = new List<AbstractFileEntry>();

        List<DLCEntry> ME2FTSDLCs { get; set; } = new List<DLCEntry>();
        List<GameFileEntry> ME2FTSGameFiles { get; set; } = new List<GameFileEntry>();
        List<AbstractFileEntry> ME2FTSExclusions { get; set; } = new List<AbstractFileEntry>();

        List<DLCEntry> ME3FTSDLCs { get; set; } = new List<DLCEntry>();
        List<GameFileEntry> ME3FTSGameFiles { get; set; } = new List<GameFileEntry>();
        List<AbstractFileEntry> ME3FTSExclusions { get; set; } = new List<AbstractFileEntry>();

        // Mains
        List<DLCEntry> tempFTSDLCs = new List<DLCEntry>();
        List<DLCEntry> FTSDLCs
        {
            get
            {
                tempFTSDLCs.Clear();
                switch (GameVersion)
                {
                    case 1:
                        tempFTSDLCs.AddRange(ME1FTSDLCs);
                        break;
                    case 2:
                        tempFTSDLCs.AddRange(ME2FTSDLCs);
                        break;
                    case 3:
                        tempFTSDLCs.AddRange(ME3FTSDLCs);
                        break;
                }

                return tempFTSDLCs;
            }
        }

        List<GameFileEntry> tempFTSGameFiles = new List<GameFileEntry>();
        List<GameFileEntry> FTSGameFiles
        {
            get
            {
                tempFTSGameFiles.Clear();
                switch (GameVersion)
                {
                    case 1:
                        tempFTSGameFiles.AddRange(ME1FTSGameFiles);
                        break;
                    case 2:
                        tempFTSGameFiles.AddRange(ME2FTSGameFiles);
                        break;
                    case 3:
                        tempFTSGameFiles.AddRange(ME3FTSGameFiles);
                        break;
                }

                return tempFTSGameFiles;
            }
        }

        List<AbstractFileEntry> tempFTSExclusions = new List<AbstractFileEntry>();
        List<AbstractFileEntry> FTSExclusions
        {
            get
            {
                tempFTSExclusions.Clear();
                switch (GameVersion)
                {
                    case 1:
                        tempFTSExclusions.AddRange(ME1FTSExclusions);
                        break;
                    case 2:
                        tempFTSExclusions.AddRange(ME2FTSExclusions);
                        break;
                    case 3:
                        tempFTSExclusions.AddRange(ME3FTSExclusions);
                        break;
                }

                return tempFTSExclusions;
            }
        }
        #endregion Caches

        public MTRangedObservableCollection<TreeTexInfo> TextureSearchResults { get; set; } = new MTRangedObservableCollection<TreeTexInfo>();
        public ICollectionView DLCItemsView { get; set; }
        public ICollectionView ExclusionsItemsView { get; set; }
        public ICollectionView FileItemsView { get; set; }
        ThumbnailWriter ThumbnailWriter = null;
        public MTRangedObservableCollection<string> Errors { get; set; } = new MTRangedObservableCollection<string>();
        public MTRangedObservableCollection<TreeTexInfo> ChangedTextures { get; set; } = new MTRangedObservableCollection<TreeTexInfo>();

        public override string TextureSearch
        {
            get
            {
                return base.TextureSearch;
            }

            set
            {
                base.TextureSearch = value;

                // Clear results if no search performed.
                if (String.IsNullOrEmpty(value))
                    TextureSearchResults.Clear();
            }
        }

        public bool TPFToolsModeEnabled
        {
            get
            {
                return ToolsetTextureEngine.TPFToolsModeEnabled;
            }
            set
            {
                SetProperty(ref ToolsetTextureEngine.TPFToolsModeEnabled, value);
            }
        }

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

            #region FTS Filtering
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
            #endregion FTS Filtering

            AbstractFileEntry.Updater = new Action(() => UpdateFTS());

            #region Setup Texture UI Commands
            TreeTexInfo.ChangeCommand = new CommandHandler(new Action<object>(tex =>
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "DirectX Images|*.dds";  // TODO Expand to allow any ImageEngine supported format for on the fly conversion. Need to have some kind of preview first though to maybe change the conversion parameters.
                if (ofd.ShowDialog() != true)
                    return;

                Task.Run(() => ChangeTexture((TreeTexInfo)tex, ofd.FileName));
            }));

            TreeTexInfo.ExtractCommand = new CommandHandler(new Action<object>(tex =>
            {
                SaveFileDialog sfd = new SaveFileDialog();
                var texture = tex as TreeTexInfo;
                sfd.FileName = texture.DefaultSaveName;
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
                await Task.Run(async () => await RegenerateThumbs((TreeTexInfo)tex)).ConfigureAwait(false);
            }));

            TexplorerTextureFolder.RegenerateThumbsDelegate = RegenerateThumbs;

            #endregion Setup UI Commands

            GameDirecs.GameVersion = Properties.Settings.Default.TexplorerGameVersion;
            OnPropertyChanged(nameof(GameVersion));

            // Setup thumbnail writer - not used unless tree scanning.
            ThumbnailWriter = new ThumbnailWriter(GameDirecs);

            Setup();
        }

        internal async Task RegenerateThumbs(params TreeTexInfo[] textures)
        {
            Busy = true;
            StartTime = Environment.TickCount;
            List<TreeTexInfo> texes = new List<TreeTexInfo>();

            // No args = regen everything
            if (textures?.Length < 1)
                texes.AddRange(Textures);
            else
                texes.AddRange(textures);

            // Open Progress Panel if worth it.
            if (texes.Count > 10)
                ProgressOpener();
            else
                ProgressIndeterminate = true;

            DebugOutput.PrintLn($"Regenerating {texes.Count} thumbnails...");

            MaxProgress = texes.Count;
            Progress = 0;

            ThumbnailWriter.BeginAdding();
            int errors = 0;

            #region Setup Regen Pipeline
            int halfThreads = NumThreads / 2;
            if (halfThreads < 1)
                halfThreads = 1;

            // Get all PCCs - maybe same as saving? - only need first pcc of each texture
            var pccBuffer = new BufferBlock<Tuple<PCCObject, IGrouping<string, TreeTexInfo>>>(new DataflowBlockOptions { BoundedCapacity = 1 });
            
            // loop over textures getting a tex2D from each tex located in pcc
            var tex2DMaker = new TransformBlock<Tuple<PCCObject, IGrouping<string, TreeTexInfo>>, Tuple<Thumbnail, Texture2D>>(obj =>
            {
                TreeTexInfo tex = obj.Item2.First();
                Texture2D tex2D = new Texture2D(obj.Item1, tex.PCCs[0].ExpID, GameDirecs);
                return new Tuple<Thumbnail, Texture2D>(tex.Thumb, tex2D);
            }, new ExecutionDataflowBlockOptions { BoundedCapacity = halfThreads });

            // Get thumb from each tex2D
            var ThumbMaker = new TransformBlock<Tuple<Thumbnail, Texture2D>, Tuple<Thumbnail, MemoryStream>>(bits => 
            {
                return new Tuple<Thumbnail, MemoryStream>(bits.Item1, ToolsetTextureEngine.GetThumbFromTex2D(bits.Item2));
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = halfThreads, BoundedCapacity = halfThreads });

            // Write thumb to disk
            var WriteThumbs = new ActionBlock<Tuple<Thumbnail, MemoryStream>>(bits => ThumbnailWriter.ReplaceOrAdd(bits.Item2, bits.Item1), new ExecutionDataflowBlockOptions { BoundedCapacity = 2, MaxDegreeOfParallelism = 1 });  // 2 to make sure a queue is there so the thumb maker can keep working.

            // Create pipeline
            pccBuffer.LinkTo(tex2DMaker, new DataflowLinkOptions { PropagateCompletion = true });
            tex2DMaker.LinkTo(ThumbMaker, new DataflowLinkOptions { PropagateCompletion = true });
            ThumbMaker.LinkTo(WriteThumbs, new DataflowLinkOptions { PropagateCompletion = true });

            // Start producer
            PCCRegenProducer(pccBuffer, texes);
            #endregion Setup Regen Pipeline

            await WriteThumbs.Completion;  // Wait for pipeline to complete

            if (cts.IsCancellationRequested)
            {
                ThumbnailWriter.FinishAdding(); // Close thumbnail writer gracefully
                Status = "Thumbnail Regeneration was cancelled!";
            }
            else
                Status = $"Regenerated {texes.Count - errors} thumbnails" + (errors == 0 ? "." : $" with {errors} errors.");

            Progress = MaxProgress;

            ThumbnailWriter.FinishAdding();

            // Close Progress Panel if previously opened.
            if (texes.Count > 10)
                ProgressCloser();
            else
                ProgressIndeterminate = false;

            StartTime = 0;
            Busy = false;
        }

        async Task PCCRegenProducer(BufferBlock<Tuple<PCCObject, IGrouping<string, TreeTexInfo>>> pccBuffer, List<TreeTexInfo> texes)
        {
            // Gets all distinct pcc's being altered.
            var pccTexGroups =
                from tex in texes
                group tex by tex.PCCs[0].Name;

            // Send each unique PCC to get textures saved to it.
            foreach (var texGroup in pccTexGroups)
            {
                if (cts.IsCancellationRequested)
                    break;

                string pcc = texGroup.Key;
                PCCObject pccobj = new PCCObject(pcc, GameVersion);
                await pccBuffer.SendAsync(new Tuple<PCCObject, IGrouping<string, TreeTexInfo>>(pccobj, texGroup));
            }

            pccBuffer.Complete();
        }

        /// <summary>
        /// For tree setup during startup.
        /// </summary>
        /// <returns></returns>
        protected override void SetupCurrentTree()
        {
            base.SetupCurrentTree();

            InitialiseFTS(1);
            InitialiseFTS(2);
            InitialiseFTS(3);

            LoadFTSandTree();
        }

        void LoadFTSandTree(bool panelAlreadyOpen = false)
        {
            FTSReady = false;

            // Tree isn't valid. Open the panel immediately.
            if (!panelAlreadyOpen && !CurrentTree.Valid && TreePanelOpener != null)
                TreePanelOpener();


            if (CurrentTree.Valid)
            {
                // Put away TreeScan Panel since it isn't required if tree is valid.
                if (TreePanelCloser != null)
                    TreePanelCloser();

                Textures.Clear();
                Textures.AddRange(CurrentTree.Textures);
            }
            else
                Status = "Tree invalid/non-existent. Begin Tree Scan by clicking 'Settings'";

            FTSReady = true;
            UpdateFTS();
        }

        void InitialiseFTS(int gameVersion)
        {
            // Setup temps
            List<DLCEntry> tempDLCs = null;
            List<GameFileEntry> tempGameFiles = null;
            List<AbstractFileEntry> tempExclusions = null;
            DLCEntry basegame = null;
            switch (gameVersion)
            {
                case 1:
                    basegame = new DLCEntry("BaseGame", MEDirectories.MEDirectories.ME1Files.Where(file => !file.Contains(@"DLC\DLC_")).ToList(), GameDirecs);
                    tempDLCs = ME1FTSDLCs;
                    tempGameFiles = ME1FTSGameFiles;
                    tempExclusions = ME1FTSExclusions;
                    break;
                case 2:
                    basegame = new DLCEntry("BaseGame", MEDirectories.MEDirectories.ME2Files.Where(file => !file.Contains(@"DLC\DLC_") && !file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList(), GameDirecs);
                    tempDLCs = ME2FTSDLCs;
                    tempGameFiles = ME2FTSGameFiles;
                    tempExclusions = ME2FTSExclusions;
                    break;
                case 3:
                    basegame = new DLCEntry("BaseGame", MEDirectories.MEDirectories.ME3Files.Where(file => !file.Contains(@"DLC\DLC_") && !file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList(), GameDirecs);
                    tempDLCs = ME3FTSDLCs;
                    tempGameFiles = ME3FTSGameFiles;
                    tempExclusions = ME3FTSExclusions;
                    break;

                default:
                    Debugger.Break();
                    break;
            }


            // Get DLC's
            tempDLCs.Add(basegame);
            GetDLCEntries(gameVersion);

            // Add all DLC files to global files list
            foreach (DLCEntry dlc in tempDLCs)
                tempGameFiles.AddRange(dlc.Files);

            if (Trees[gameVersion - 1].Valid)
            {
                /* Find any existing exclusions from when tree was created.*/
                // Set excluded DLC's checked first
                tempDLCs.ForEach(dlc => dlc.IsChecked = !dlc.Files.Any(file => CurrentTree.ScannedPCCs.Contains(file.FilePath)));

                // Then set all remaining exlusions
                foreach (DLCEntry dlc in tempDLCs.Where(dlc => !dlc.IsChecked))
                    dlc.Files.ForEach(file => file.IsChecked = !CurrentTree.ScannedPCCs.Contains(file.FilePath));
            }
            

            tempExclusions.AddRange(tempDLCs);
            tempExclusions.AddRange(tempGameFiles);
        }

        public void UpdateFTS()
        {
            OnPropertyChanged(nameof(FTSDLCs));
            OnPropertyChanged(nameof(FTSExclusions));
            OnPropertyChanged(nameof(FTSGameFiles));

            var temp = FTSDLCs;
            var ttemp = FTSExclusions;
            var itemp = FTSGameFiles;

            
            DLCItemsView.Refresh();
            ExclusionsItemsView.Refresh();
            FileItemsView.Refresh();
        }

        void GetDLCEntries(int tempGame)
        {
            List<string> DLCs = null;
            List<DLCEntry> tempFTSDLCs = null;
            List<string> tempGameFiles = null;
            switch (tempGame)
            {
                case 1:
                    DLCs = Directory.EnumerateDirectories(MEDirectories.MEDirectories.ME1DLCPath).Where(direc => !direc.Contains("_metadata")).ToList();
                    tempFTSDLCs = ME1FTSDLCs;
                    tempGameFiles = MEDirectories.MEDirectories.ME1Files;
                    break;
                case 2:
                    DLCs = Directory.EnumerateDirectories(MEDirectories.MEDirectories.ME2DLCPath).Where(direc => !direc.Contains("_metadata")).ToList();
                    tempGameFiles = MEDirectories.MEDirectories.ME2Files;
                    tempFTSDLCs = ME2FTSDLCs;
                    break;
                case 3:
                    DLCs = Directory.EnumerateDirectories(MEDirectories.MEDirectories.ME3DLCPath).Where(direc => !direc.Contains("_metadata")).ToList();
                    tempGameFiles = MEDirectories.MEDirectories.ME3Files;
                    tempFTSDLCs = ME3FTSDLCs;
                    break;
            }

            foreach(string dlc in DLCs)
            {
                string[] parts = dlc.Split('\\');
                string DLCName = parts.First(part => part.Contains("DLC_"));

                string name = MEDirectories.MEDirectories.GetCommonDLCName(DLCName);
                DLCEntry entry = new DLCEntry(name, tempGameFiles.Where(file => file.Contains(DLCName) && !file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList(), GameDirecs);

                tempFTSDLCs.Add(entry);
            }
        }

        internal async Task BeginTreeScan()
        {
            Busy = true;

            TextureSearch = null;

            DebugOutput.PrintLn($"Beginning Tree scan for ME{GameVersion}.");

            // Populate Tree PCCs in light of exclusions
            foreach (GameFileEntry item in FTSGameFiles.Where(file => !file.IsChecked && !file.FilterOut))
                CurrentTree.ScannedPCCs.Add(item.FilePath);

            DebugOutput.PrintLn("Attempting to delete old thumbnails.");

            // Remove any existing thumbnails
            if (File.Exists(GameDirecs.ThumbnailCachePath))
                File.Delete(GameDirecs.ThumbnailCachePath);

            // Wait until file properly deleted
            while (File.Exists(GameDirecs.ThumbnailCachePath))
                await Task.Delay(100); 

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

            if (cts.IsCancellationRequested)
            {
                Busy = false;
                return;
            }

            CurrentTree.IsSelected = true;  // TODO: Need to call LoadFTSandTree? 

            DebugOutput.PrintLn("Saving tree to disk...");
            await Task.Run(() =>
            {
                CurrentTree.ConstructTree();
                SetupPCCCheckBoxLinking(CurrentTree.Textures);
            }).ConfigureAwait(false);
            await Task.Run(() => CurrentTree.SaveToFile()).ConfigureAwait(false);
            DebugOutput.PrintLn($"Treescan completed. Elapsed time: {ElapsedTime}. Num Textures: {CurrentTree.Textures.Count}.");
            
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

            // DEBUGGING
            /*CurrentTree.ScannedPCCs.Clear();
            CurrentTree.ScannedPCCs.Add(@"R:\Games\Mass Effect\BioGame\CookedPC\Packages\GameObjects\Characters\Humanoids\Salarian\BIOG_SAL_HED_PROMorph_R.upk");*/

            IList<string> PCCsToScan = CurrentTree.ScannedPCCs;  // Can't use ?? here as ScannedPCCs and pccs are different classes.
            if (pccs != null)
                PCCsToScan = pccs;

            MaxProgress = PCCsToScan.Count;

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

            Status = "Beginning Tree Scan...";

            

            // Perform scan
            await ScanAllPCCs(PCCsToScan, TFCs).ConfigureAwait(false);   // Start entire thing on another thread which awaits when collection is full, then wait for pipeline completion.

            // Re-arrange files
            if (GameVersion == 1)
                ToolsetTextureEngine.ME1_SortTexturesPCCs(CurrentTree.Textures);

            // Dispose all TFCs
            if (TFCs != null)
            {
                foreach (var tfc in TFCs)
                    tfc.Value.Dispose();

                TFCs.Clear();
                TFCs = null;
            }
                
            Debug.WriteLine($"Max ram during scan: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024d / 1024d / 1024d}");
            DebugOutput.PrintLn($"Max ram during scan: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024d / 1024d / 1024d}");

            Progress = MaxProgress;

            if (cts.IsCancellationRequested)
                Status = "Tree scan was cancelled!";
            else
                Status = $"Scan complete. Found {CurrentTree.Textures.Count} textures. Elapsed scan time: {ElapsedTime}.";
        }

        Task ScanAllPCCs(IList<string> PCCs, Dictionary<string, MemoryStream> TFCs)
        {
            // Parallel scanning
            int bound = 10;
            double RAMinGB = ToolsetInfo.AvailableRam / 1024d / 1024d / 1024d;
            if (RAMinGB < 10)
                bound = 5;

            // Create buffer to store PCCObjects from disk
            BufferBlock<PCCObject> pccScanBuffer = new BufferBlock<PCCObject>(new DataflowBlockOptions { BoundedCapacity = bound });   // Collection can't grow past this. Good for low RAM situations.

            // Decide degrees of parallelism for each block of the pipeline
            int numScanners = NumThreads;
            int maxParallelForSorter = NumThreads;
#if (ThreadedScan)
            maxParallelForSorter = 1;
            numScanners = 1;
#endif


            var texSorterOptions = new ExecutionDataflowBlockOptions { BoundedCapacity = maxParallelForSorter, MaxDegreeOfParallelism = maxParallelForSorter };
            var pccScannerOptions = new ExecutionDataflowBlockOptions { BoundedCapacity = numScanners, MaxDegreeOfParallelism = numScanners };

            // Setup pipeline blocks
            var pccScanner = new TransformManyBlock<PCCObject, TreeTexInfo>(pcc => ScanSinglePCC(pcc, TFCs), pccScannerOptions);
            var texSorter = new ActionBlock<TreeTexInfo>(tex => CurrentTree.AddTexture(tex), texSorterOptions);  // In another block so as to allow Generating Thumbnails to decouple from identifying textures

            // Link together
            pccScanBuffer.LinkTo(pccScanner, new DataflowLinkOptions { PropagateCompletion = true });
            pccScanner.LinkTo(texSorter, new DataflowLinkOptions { PropagateCompletion = true });

            // Begin producer
            PCCProducer(PCCs, pccScanBuffer);

            // Return task to await for pipeline completion - only need to wait for last block as PropagateCompletion is set.
            return texSorter.Completion;
        }

        async Task PCCProducer(IList<string> PCCs, BufferBlock<PCCObject> pccs)
        {
            for (int i = 0; i < PCCs.Count; i++)
            {
                if (cts.IsCancellationRequested)
                    break;

                string file = PCCs[i];
                PCCObject pcc = await PCCObject.CreateAsync(file, GameVersion);

                await pccs.SendAsync(pcc);
            }

            pccs.Complete();
        }

        List<TreeTexInfo> ScanSinglePCC(PCCObject pcc, Dictionary<string, MemoryStream> TFCs)
        {
            List<TreeTexInfo> texes = new List<TreeTexInfo>();
            DebugOutput.PrintLn($"Scanning: {pcc.pccFileName}");

            try
            {
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    ExportEntry export = pcc.Exports[i];
                    if (!export.ValidTextureClass())
                        continue;

                    Texture2D tex2D = null;
                    try
                    {
                        tex2D = new Texture2D(pcc, i, GameDirecs);
                    }
                    catch (Exception e)
                    {
                        Errors.Add(e.ToString());
                        continue;
                    }

                    // Skip if no images
                    if (tex2D.ImageList.Count == 0)
                    {
                        tex2D.Dispose();
                        continue;
                    }

                    try
                    {
                        TreeTexInfo info = new TreeTexInfo(tex2D, ThumbnailWriter, export, TFCs, Errors, GameDirecs);
                        texes.Add(info);
                    }
                    catch(Exception e)
                    {
                        Errors.Add(e.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn($"Scanning failed on {pcc.pccFileName}. Reason: {e.ToString()}.");
            }
            finally
            {
                pcc.Dispose();
            }

            Progress++;
            Status = $"Scanning PCC's to build ME{GameVersion} tree: {Progress} / {MaxProgress}";

            return texes;       
        }

        internal void LoadPreview(TreeTexInfo texInfo)
        {
            using (PCCObject pcc = new PCCObject(texInfo.PCCs[0].Name, GameVersion))
            {
                using (Texture2D tex2D = new Texture2D(pcc, texInfo.PCCs[0].ExpID, GameDirecs))
                {
                    byte[] img = tex2D.ExtractMaxImage(true);
                    using (ImageEngineImage jpg = new ImageEngineImage(img))
                        PreviewImage = jpg.GetWPFBitmap();

                    img = null;
                }
            }
        }

        // This is going to change to pipeline TPL stuff when TPFTools comes along
        public void ChangeTexture(TreeTexInfo tex, string filename)
        {
            Busy = true;
            Status = $"Changing Texture: {tex.TexName}...";
            ProgressIndeterminate = true;

            bool success = ToolsetTextureEngine.ChangeTexture(tex, filename);
            if (success)
            {
                // Add only if not already added.
                if (!ChangedTextures.Contains(tex))
                    ChangedTextures.Add(tex);

                // Re-populate details
                tex.PopulateDetails();

                // Re-generate Thumbnail
                MemoryStream stream = null;
                if (tex.HasChanged)
                    stream = ToolsetTextureEngine.GetThumbFromTex2D(tex.ChangedAssociatedTexture);

                using (PCCObject pcc = new PCCObject(tex.PCCs[0].Name, GameVersion))
                    using (Texture2D tex2D = new Texture2D(pcc, tex.PCCs[0].ExpID, GameDirecs))
                        stream = ToolsetTextureEngine.GetThumbFromTex2D(tex2D);

                if (tex.Thumb == null) // Could happen
                    tex.Thumb = new Thumbnail();

                tex.SetChangedThumb(stream);
            }

            ProgressIndeterminate = false;
            Progress = MaxProgress;
            Status = $"Texture: {tex.TexName} changed!";
            Busy = false;
        }

        internal void ExtractTexture(TreeTexInfo tex, string filename)
        {
            Busy = true;
            Status = $"Extracting Texture: {tex.TexName}...";
            ProgressIndeterminate = true;

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

            ProgressIndeterminate = false;
            Progress = MaxProgress;
            Busy = false;
            Status = $"Texture: {tex.TexName} " + (error == null ? $"extracted to {filename}!" : $"failed to extract. Reason: {error}.");
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

        public override void Search(string searchText)
        {
            TextureSearchResults.Clear();

            if (String.IsNullOrEmpty(searchText))
                return;

            ConcurrentBag<TreeTexInfo> tempResults = new ConcurrentBag<TreeTexInfo>();

            Parallel.ForEach(Textures, texture =>
            {
                bool found = texture.Searchables.Any(searchable => searchable.Contains(searchText, StringComparison.OrdinalIgnoreCase));
                if (found)
                    tempResults.Add(texture);
            });

            TextureSearchResults.AddRange(tempResults);
        }

        public void Refresh()
        {
            UpdateFTS();
        }
    }
}
