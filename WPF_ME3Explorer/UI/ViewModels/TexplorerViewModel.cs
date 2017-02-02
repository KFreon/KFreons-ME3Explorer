#define ThreadedScan

using CSharpImageLibrary;
using CSharpImageLibrary.DDS;
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
using System.Windows.Threading;
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
                    ftsDLCsUnCheckAll = new CommandHandler(() =>
                    {
                        for (int i = 0; i < FTSDLCs.Count; i++)
                            FTSDLCs[i].IsChecked = true;
                    });

                return ftsDLCsUnCheckAll;
            }
        }

        CommandHandler removeNullPointersCommand = null;
        public CommandHandler RemoveNullPointersCommand
        {
            get
            {
                if (removeNullPointersCommand == null)
                    removeNullPointersCommand = new CommandHandler(new Action(async () => Task.Run(() => RemoveNullPointersFromTree())));

                return removeNullPointersCommand;
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
                        await Task.Run(() => CurrentTree.SaveToFile());

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

        #region Extraction Panel
        bool showExtractionPanel = false;
        public bool ShowExtractionPanel
        {
            get
            {
                return showExtractionPanel;
            }
            set
            {
                SetProperty(ref showExtractionPanel, value);
            }
        }

        string extract_SavePath = null;
        public string Extract_SavePath
        {
            get
            {
                return extract_SavePath;
            }
            set
            {
                SetProperty(ref extract_SavePath, value);
            }
        }

        bool extract_IsFolder = false;
        public bool Extract_IsFolder
        {
            get
            {
                return extract_IsFolder;
            }
            set
            {
                SetProperty(ref extract_IsFolder, value);
            }
        }

        ImageEngineFormat extract_SaveFormat = ImageEngineFormat.Unknown;
        public ImageEngineFormat Extract_SaveFormat
        {
            get
            {
                return extract_SaveFormat;
            }
            set
            {
                SetProperty(ref extract_SaveFormat, value);
            }
        }

        bool extract_BuildMips = true;
        public bool Extract_BuildMips
        {
            get
            {
                return extract_BuildMips;
            }
            set
            {
                SetProperty(ref extract_BuildMips, value);
            }
        }

        CommandHandler extract_ExtractCommand = null;
        public CommandHandler Extract_ExtractCommand
        {
            get
            {
                if (extract_ExtractCommand == null)
                    extract_ExtractCommand = new CommandHandler(() =>
                    {
                        if (Extract_IsFolder)
                            ExtractTextures(SelectedFolder.TexturesInclSubs, Extract_SavePath);
                        else
                            ExtractTexture(SelectedTexture, Extract_SavePath, Extract_BuildMips, Extract_SaveFormat);
                    });

                return extract_ExtractCommand;
            }
        }
        #endregion Extraction Panel

        #region Change Texture Panel
        double originalDXT1Alpha = 0;
        bool showChangePanel = false;
        public bool ShowChangePanel
        {
            get
            {
                return showChangePanel;
            }
            set
            {
                SetProperty(ref showChangePanel, value);
                if (value)
                    originalDXT1Alpha = DDSGeneral.DXT1AlphaThreshold;
            }
        }

        string change_SavePath = null;
        public string Change_SavePath
        {
            get
            {
                return change_SavePath;
            }
            set
            {
                SetProperty(ref change_SavePath, value);
            }
        }

        bool change_DisplayAlpha = false;
        public bool Change_DisplayAlpha
        {
            get
            {
                return change_DisplayAlpha;
            }
            set
            {
                SetProperty(ref change_DisplayAlpha, value);
                OnPropertyChanged(nameof(Change_OrigPreview));
                OnPropertyChanged(nameof(Change_ConvPreview));
            }
        }

        BitmapSource change_OrigAlphaPreview = null;
        BitmapSource change_OrigNOAlphaPreview = null;
        public BitmapSource Change_OrigPreview
        {
            get
            {
                return Change_DisplayAlpha ? change_OrigAlphaPreview : change_OrigNOAlphaPreview;
            }
        }

        BitmapSource change_ConvAlphaPreview = null;
        BitmapSource change_ConvNOAlphaPreview = null;
        public BitmapSource Change_ConvPreview
        {
            get
            {
                return Change_DisplayAlpha ? change_ConvAlphaPreview : change_ConvNOAlphaPreview;
            }
        }

        ImageEngineFormat change_TreeFormat = ImageEngineFormat.Unknown;
        public ImageEngineFormat Change_TreeFormat
        {
            get
            {
                return change_TreeFormat;
            }
            set
            {
                SetProperty(ref change_TreeFormat, value);
            }
        }

        ImageEngineFormat change_ReplacingFormat = ImageEngineFormat.Unknown;
        public ImageEngineFormat Change_ReplacingFormat
        {
            get
            {
                return change_ReplacingFormat;
            }
            set
            {
                SetProperty(ref change_ReplacingFormat, value);
            }
        }

        CommandHandler change_ChangeCommand = null;
        public CommandHandler Change_ChangeCommand
        {
            get
            {
                if (change_ChangeCommand == null)
                    change_ChangeCommand = new CommandHandler(() =>
                    {
                        ChangeTexture(SelectedTexture, change_convImage);

                        // Reset alpha threshold for further toolset operations.
                        DDSGeneral.DXT1AlphaThreshold = originalDXT1Alpha;
                    });

                return change_ChangeCommand;
            }
        }

        bool change_FlattenAlpha = false;
        public bool Change_FlattenAlpha
        {
            get
            {
                return change_FlattenAlpha;
            }
            set
            {
                SetProperty(ref change_FlattenAlpha, value);
                change_RemoveAlpha = !value;
                OnPropertyChanged(nameof(change_RemoveAlpha));
                DDSGeneral.DXT1AlphaThreshold = blendValue;

                change_PreviewTimer.Stop();
                change_PreviewTimer.Start();
            }
        }

        bool change_RemoveAlpha = false;
        public bool Change_RemoveAlpha
        {
            get
            {
                return change_RemoveAlpha;
            }
            set
            {
                SetProperty(ref change_RemoveAlpha, value);
                change_FlattenAlpha = !value;
                OnPropertyChanged(nameof(change_FlattenAlpha));
                DDSGeneral.DXT1AlphaThreshold = 0f;  // KFreon: Strips the alpha out 

                change_PreviewTimer.Stop();
                change_PreviewTimer.Start();
            }
        }

        double blendValue = DDSGeneral.DXT1AlphaThreshold;
        public double Change_AlphaThreshold
        {
            get
            {
                DDSGeneral.DXT1AlphaThreshold = blendValue;
                return DDSGeneral.DXT1AlphaThreshold * 100d;
            }
            set
            {
                DDSGeneral.DXT1AlphaThreshold = value / 100f;
                OnPropertyChanged(nameof(Change_AlphaThreshold));
                blendValue = value / 100f;

                change_PreviewTimer.Stop();
                change_PreviewTimer.Start();
            }
        }

        ImageEngineImage change_origImg = null;
        ImageEngineImage change_convImage = null;
        DispatcherTimer change_PreviewTimer = new DispatcherTimer();
        #endregion Change Texure Panel

        #region UI Actions
        public Action TreePanelCloser = null;
        public Action ProgressOpener = null;
        public Action TreePanelOpener = null;
        public Action ProgressCloser = null;
        #endregion UI Actions

        DispatcherTimer searchDelayer = new DispatcherTimer();

        #region Properties
        bool removeNullPointers = true;
        public bool RemoveNullPointers
        {
            get
            {
                return removeNullPointers;
            }
            set
            {
                SetProperty(ref removeNullPointers, value);
            }
        }

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
        MTRangedObservableCollection<DLCEntry> FTSDLCs { get; set; } = new MTRangedObservableCollection<DLCEntry>();
        MTRangedObservableCollection<GameFileEntry> FTSGameFiles { get; set; } = new MTRangedObservableCollection<GameFileEntry>();
        MTRangedObservableCollection<AbstractFileEntry> FTSExclusions { get; set; } = new MTRangedObservableCollection<AbstractFileEntry>();
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
                else
                {
                    searchDelayer.Stop();
                    searchDelayer.Start();
                }
            }
        }

        public bool HasSearchResults
        {
            get
            {
                return TextureSearchResults.Count > 0;
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

        bool DisableFTSUpdating = false;

        public TexplorerViewModel() : base()
        {
            DebugOutput.StartDebugger("Texplorer");
            change_PreviewTimer.Interval = TimeSpan.FromSeconds(1);
            change_PreviewTimer.Tick += (t, b) =>
            {
                Debugger.Break();
                // Build previews off thread
                Task.Run(() =>
                {
                    Change_GeneratePreviews();
                });
            };

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

            // Setup search
            searchDelayer.Interval = TimeSpan.FromSeconds(0.5);
            searchDelayer.Tick += (sender, args) =>
            {
                Search(TextureSearch);
                OnPropertyChanged(nameof(HasSearchResults));

                searchDelayer.Stop();  // Only want it to search once. Will get started when user first types something again.
            };

            AbstractFileEntry.Updater = new Action(() =>
            {
                if (!DisableFTSUpdating)
                    UpdateFTS();
            });

            #region Setup Texture UI Commands
            TreeTexInfo.ChangeCommand = new CommandHandler(new Action<object>(async param =>
            {
                // Try normal first - i.e. Replacing file is of correct format
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Select replacing image";
                ofd.Filter = ToolsetTextureEngine.GetFullDialogAcceptedImageFilters();
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog() != true)
                    return;

                TreeTexInfo tex = (TreeTexInfo)param;

                Change_SavePath = ofd.FileName;
                change_origImg = new ImageEngineImage(Change_SavePath);

                // Format is correct. Just replace and go.
                if (change_origImg.Format == tex.Format)
                {
                    ChangeTexture(tex, change_origImg);
                    change_origImg.Dispose();
                    return;
                }

                // Format not correct, requries conversion //
                ShowChangePanel = true;
                Change_ReplacingFormat = change_origImg.Format;
                Change_TreeFormat = tex.Format;

                // Default settings //
                // Default - alpha tends to inhibit the normal view of textures
                Change_DisplayAlpha = false;

                // Build previews off thread
                await Task.Run(() =>
                {
                    Change_GeneratePreviews();
                });
            }));

            TreeTexInfo.ExportTexAndInfoCommand = new CommandHandler(new Action<object>(obj =>
            {
                TreeTexInfo tex = (TreeTexInfo)obj;
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = Path.GetFileNameWithoutExtension(tex.DefaultSaveName) + ".zip";
                sfd.Filter = "Compressed Files|*.zip";
                sfd.Title = "Select destination for texture information";
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
                        string details = ToolsetTextureEngine.BuildTexDetailsForCSV(tex);

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

            TreeTexInfo.ExtractCommand = new CommandHandler(new Action<object>(tex =>
            {
                // Default settings
                Extract_SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), SelectedTexture?.DefaultSaveName);
                Extract_SaveFormat = SelectedTexture?.Format ?? ImageEngineFormat.Unknown;
                Extract_BuildMips = false;
                Extract_IsFolder = false;

                ShowExtractionPanel = true;
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
            TexplorerTextureFolder.ExtractFolderTexturesCommand = new CommandHandler(new Action<object>(obj =>
            {
                TexplorerTextureFolder folder = (TexplorerTextureFolder)obj;

                Extract_SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), SelectedFolder?.Path);
                Extract_IsFolder = true;
                

                ShowExtractionPanel = true;
            }));

            #endregion Setup UI Commands

            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GameVersion))
                    UpdateFTS();
            };

            GameDirecs.GameVersion = Properties.Settings.Default.TexplorerGameVersion;
            OnPropertyChanged(nameof(GameVersion));

            // Setup thumbnail writer - not used unless tree scanning.
            ThumbnailWriter = new ThumbnailWriter(GameDirecs);

            Setup();
        }

        void Change_GeneratePreviews()
        {
            var convData = change_origImg.Save(new ImageFormats.ImageEngineFormatDetails(SelectedTexture.Format), MipHandling.KeepTopOnly);
            ImageEngineImage conv = new ImageEngineImage(convData);

            change_ConvAlphaPreview = conv.GetWPFBitmap(ShowAlpha: true);
            change_ConvNOAlphaPreview = conv.GetWPFBitmap();

            change_OrigAlphaPreview = change_origImg.GetWPFBitmap(ShowAlpha: true);
            change_OrigNOAlphaPreview = change_origImg.GetWPFBitmap();

            OnPropertyChanged(nameof(Change_ConvPreview));
            OnPropertyChanged(nameof(Change_OrigPreview));
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
                    basegame = new DLCEntry("BaseGame", MEDirectories.MEDirectories.ME1Files?.Where(file => !file.Contains(@"DLC\DLC_")).ToList(), GameDirecs);
                    tempDLCs = ME1FTSDLCs;
                    tempGameFiles = ME1FTSGameFiles;
                    tempExclusions = ME1FTSExclusions;
                    break;
                case 2:
                    basegame = new DLCEntry("BaseGame", MEDirectories.MEDirectories.ME2Files?.Where(file => !file.Contains(@"DLC\DLC_") && !file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList(), GameDirecs);
                    tempDLCs = ME2FTSDLCs;
                    tempGameFiles = ME2FTSGameFiles;
                    tempExclusions = ME2FTSExclusions;
                    break;
                case 3:
                    basegame = new DLCEntry("BaseGame", MEDirectories.MEDirectories.ME3Files?.Where(file => !file.Contains(@"DLC\DLC_") && !file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList(), GameDirecs);
                    tempDLCs = ME3FTSDLCs;
                    tempGameFiles = ME3FTSGameFiles;
                    tempExclusions = ME3FTSExclusions;
                    break;

                default:
                    Debugger.Break();
                    break;
            }

            // Only add if none already there. 
            if (tempDLCs.Count != 0)
            {
                // Get DLC's
                tempDLCs.Add(basegame);
                GetDLCEntries(gameVersion);

                // Add all DLC files to global files list
                foreach (DLCEntry dlc in tempDLCs)
                    tempGameFiles.AddRange(dlc.Files);

                tempExclusions.AddRange(tempDLCs);
                tempExclusions.AddRange(tempGameFiles);
            }

            
            if (Trees[gameVersion - 1].Valid)
            {
                DisableFTSUpdating = true;

                /* Find any existing exclusions from when tree was created.*/
                // Set excluded DLC's checked first
                tempDLCs.ForEach(dlc => dlc.IsChecked = !dlc.Files.Any(file => !CurrentTree.ScannedPCCs.Contains(file.FilePath)));


                // Then set all remaining exlusions
                foreach (DLCEntry dlc in tempDLCs.Where(dlc => !dlc.IsChecked))
                    dlc.Files.ForEach(file => file.IsChecked = !CurrentTree.ScannedPCCs.Contains(file.FilePath));

                DisableFTSUpdating = false;
                UpdateFTS();
            }            
        }

        public void UpdateFTS()
        {
            // Update FTS collections
            FTSDLCs.Clear();
            FTSExclusions.Clear();
            FTSGameFiles.Clear();

            switch (GameVersion)
            {
                case 1:
                    FTSDLCs.AddRange(ME1FTSDLCs);
                    FTSGameFiles.AddRange(ME1FTSGameFiles);
                    FTSExclusions.AddRange(ME1FTSExclusions);
                    break;
                case 2:
                    FTSDLCs.AddRange(ME2FTSDLCs);
                    FTSGameFiles.AddRange(ME2FTSGameFiles);
                    FTSExclusions.AddRange(ME2FTSExclusions);
                    break;
                case 3:
                    FTSDLCs.AddRange(ME3FTSDLCs);
                    FTSGameFiles.AddRange(ME3FTSGameFiles);
                    FTSExclusions.AddRange(ME3FTSExclusions);
                    break;
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                DLCItemsView.Refresh();
                ExclusionsItemsView.Refresh();
                FileItemsView.Refresh();
            }));
            

            OnPropertyChanged(nameof(FTSDLCs));
            OnPropertyChanged(nameof(FTSExclusions));
            OnPropertyChanged(nameof(FTSGameFiles));

            OnPropertyChanged(nameof(DLCItemsView));
            OnPropertyChanged(nameof(ExclusionsItemsView));
            OnPropertyChanged(nameof(FileItemsView));
        }

        void GetDLCEntries(int tempGame)
        {
            List<string> DLCs = null;
            List<DLCEntry> tempFTSDLCs = null;
            List<string> tempGameFiles = null;

            switch (tempGame)
            {
                case 1:
                    if (!Directory.Exists(MEDirectories.MEDirectories.ME1DLCPath))
                        return;

                    DLCs = Directory.EnumerateDirectories(MEDirectories.MEDirectories.ME1DLCPath).Where(direc => !direc.Contains("_metadata")).ToList();
                    tempFTSDLCs = ME1FTSDLCs;
                    tempGameFiles = MEDirectories.MEDirectories.ME1Files;
                    break;
                case 2:
                    if (!Directory.Exists(MEDirectories.MEDirectories.ME2DLCPath))
                        return;

                    DLCs = Directory.EnumerateDirectories(MEDirectories.MEDirectories.ME2DLCPath).Where(direc => !direc.Contains("_metadata")).ToList();
                    tempGameFiles = MEDirectories.MEDirectories.ME2Files;
                    tempFTSDLCs = ME2FTSDLCs;
                    break;
                case 3:
                    if (!Directory.Exists(MEDirectories.MEDirectories.ME3DLCPath))
                        return;

                    DLCs = Directory.EnumerateDirectories(MEDirectories.MEDirectories.ME3DLCPath).Where(direc => !direc.Contains("_metadata")).ToList();
                    tempGameFiles = MEDirectories.MEDirectories.ME3Files;
                    tempFTSDLCs = ME3FTSDLCs;
                    break;
            }

            foreach (string dlc in DLCs)
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

            if (cts.IsCancellationRequested)
            {
                Busy = false;
                return;
            }
            
            CurrentTree.IsSelected = true; 

            DebugOutput.PrintLn("Saving tree to disk...");
            await Task.Run(() =>
            {
                CurrentTree.ConstructTree();
                SetupPCCCheckBoxLinking(CurrentTree.Textures);
            }).ConfigureAwait(false);
            await Task.Run(() => CurrentTree.SaveToFile()).ConfigureAwait(false);
            DebugOutput.PrintLn($"Treescan completed. Elapsed time: {ElapsedTime.ToString(@"%h\:mm\:ss\.ss")}. Num Textures: {CurrentTree.Textures.Count}.");
            
            ThumbnailWriter.FinishAdding();
            
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

            CurrentTree.Valid = true; // It was just scanned after all.
            SetupCurrentTree();

            Progress = MaxProgress;

            if (cts.IsCancellationRequested)
                Status = "Tree scan was cancelled!";
            else
                Status = $"Scan complete. Found {CurrentTree.Textures.Count} textures. Elapsed scan time: {ElapsedTime.ToString(@"%h\:mm\:ss\.ss")}.";
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
            int numThumbGenerators = NumThreads;
#if (!ThreadedScan)
            numThumbGenerators = 1;
            numScanners = 1;
#endif


            var thumbGenOptions = new ExecutionDataflowBlockOptions { BoundedCapacity = numThumbGenerators, MaxDegreeOfParallelism = numThumbGenerators };
            var pccScannerOptions = new ExecutionDataflowBlockOptions { BoundedCapacity = numScanners, MaxDegreeOfParallelism = numScanners };

            // Setup pipeline blocks
            var pccScanner = new TransformManyBlock<PCCObject, TreeTexInfo>(pcc => ScanSinglePCC(pcc, TFCs), pccScannerOptions);
            var texSorter = new TransformBlock<TreeTexInfo, TreeTexInfo>(tex =>
            {
                CurrentTree.AddTexture(tex);
                return tex;
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1, BoundedCapacity = 5 });  // Limited to single thread since it's all locked anyway.

            var thumbGenerator = new ActionBlock<TreeTexInfo>(tex =>
            {
                if (tex.GenerateThumbnail != null)
                    tex.GenerateThumbnail();

                tex.GenerateThumbnail = null; // Frees up the generation code for GC.
            }, thumbGenOptions);   // In another block so as to allow Generating Thumbnails to decouple from identifying textures

            // Link together
            pccScanBuffer.LinkTo(pccScanner, new DataflowLinkOptions { PropagateCompletion = true });
            pccScanner.LinkTo(texSorter, new DataflowLinkOptions { PropagateCompletion = true });
            texSorter.LinkTo(thumbGenerator, new DataflowLinkOptions { PropagateCompletion = true });

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


        // Much thanks to Aquadran for his idea of removing the null pointers to prevent weird texture displays when upscaling.
        bool RemoveInvalidTexEntries(PCCObject pcc, Texture2D tex2D)
        {
            // Remove null pointers
            if (tex2D.ImageList.Any(t => t.storageType == Texture2D.storage.empty || t.Offset == -1 || t.CompressedSize == -1))
            {
                // imgdata/header/footer?,
                tex2D.ImageList.RemoveAll(t => t.storageType == Texture2D.storage.empty || t.Offset == -1 || t.CompressedSize == -1);
                tex2D.UpdateSizeProperties(tex2D.ImageList[0].ImageSize.Width, tex2D.ImageList[0].ImageSize.Height, true);
                tex2D.UpdateMipCountProperty();
                
                // Save tex2D's to PCC
                ToolsetTextureEngine.SaveTex2DToPCC(pcc, tex2D, GameDirecs, tex2D.pccExpIdx);
                
                return true;
            }

            return false;
        }

        public void RemoveNullPointersFromTree()
        {
            // Massage textures into more efficient loop format (loop over pccs, then textures)
            var pccTexGroups =
                from tex in CurrentTree.Textures
                from pcc in tex.PCCs
                group pcc by pcc.Name;


            int count = 0;
            int length = pccTexGroups.Count();
            Progress = 0;
            MaxProgress = length;
            foreach (var group in pccTexGroups)
            {
                Status = $"Removing nulls from pccs: {count++}/{length}";

                PCCObject pcc = new PCCObject(group.Key, GameVersion);
                bool requiresSave = false;
                foreach (var tex in group.Distinct())
                {
                    var tex2D = new Texture2D(pcc, tex.ExpID, GameDirecs, treescanning: true);
                    if (RemoveInvalidTexEntries(pcc, tex2D))
                        requiresSave = true;
                }

                if (requiresSave)
                    pcc.SaveToFile(pcc.pccFileName);

                Progress++;
            }

            Progress = MaxProgress;
            Status = "Nulls removed!";
        }

        List<TreeTexInfo> ScanSinglePCC(PCCObject pcc, Dictionary<string, MemoryStream> TFCs)
        {
            List<TreeTexInfo> texes = new List<TreeTexInfo>();
            DebugOutput.PrintLn($"Scanning: {pcc.pccFileName}");

            bool pccRequiresSave = false;

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
                        tex2D = new Texture2D(pcc, i, GameDirecs, treescanning: true);

                        if (RemoveNullPointers && RemoveInvalidTexEntries(pcc, tex2D))
                            pccRequiresSave = true;
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
                        TreeTexInfo info = new TreeTexInfo(tex2D, ThumbnailWriter, export, TFCs, Errors, GameDirecs, CurrentTree.ScannedPCCs);
                        texes.Add(info);
                    }
                    catch(Exception e)
                    {
                        Errors.Add(e.ToString());
                    }
                }
                
                // Save PCC if nulls removed
                if (RemoveNullPointers && pccRequiresSave)
                    pcc.SaveToFile(pcc.pccFileName);
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
                if (texInfo.HasChanged)
                    SetPreview(texInfo.ChangedAssociatedTexture);
                else
                    using (Texture2D tex2D = new Texture2D(pcc, texInfo.PCCs[0].ExpID, GameDirecs))
                        SetPreview(tex2D);
            }
        }

        void SetPreview(Texture2D tex2D)
        {
            byte[] img = tex2D.ExtractMaxImage(true);
            using (ImageEngineImage jpg = new ImageEngineImage(img))
                PreviewImage = jpg.GetWPFBitmap();

            img = null;
        }

        // This is going to change to pipeline TPL stuff when TPFTools comes along
        public void ChangeTexture(TreeTexInfo tex, string filename)
        {
            Busy = true;
            Status = $"Changing Texture: {tex.TexName}...";
            ProgressIndeterminate = true;

            bool success = ToolsetTextureEngine.ChangeTexture(tex, filename);
            ChangeTexture(tex, success);
        }

        // This is going to change to pipeline TPL stuff when TPFTools comes along
        public void ChangeTexture(TreeTexInfo tex, ImageEngineImage img)
        {
            Busy = true;
            Status = $"Changing Texture: {tex.TexName}...";
            ProgressIndeterminate = true;

            bool success = ToolsetTextureEngine.ChangeTexture(tex, img);
            ChangeTexture(tex, success);
        }


        void ChangeTexture(TreeTexInfo tex, bool success)
        {
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

        internal void ExtractTextures(List<TreeTexInfo> texes, string destFolder)
        {
            Busy = true;
            Progress = 0;
            MaxProgress = texes.Count;
            Status = $"Extracting Textures: {Progress} / {MaxProgress}";

            Progress<int> progressIndicator = new Progress<int>(new Action<int>(prog =>
            {
                Progress++;
                Status = $"Extracting Textures: {Progress} / {MaxProgress}";
            }));

            string error = null;
            try
            {
                ToolsetTextureEngine.ExtractTextures(texes, destFolder, progressIndicator);
            }
            catch (Exception e)
            {
                error = e.Message;
                DebugOutput.PrintLn($"Extracting textures failed. Reason: {e.ToString()}");
            }

            Progress = MaxProgress;
            Busy = false;
            Status = error == null ? $"Textures extracted to {destFolder}!" : $"Failed to extract textures. Reason: {error}.";
            showExtractionPanel = false;
        }

        internal void ExtractTexture(TreeTexInfo tex, string filename, bool buildMips = true, ImageEngineFormat format = ImageEngineFormat.Unknown)
        {
            Busy = true;
            Status = $"Extracting Texture: {tex.TexName}...";
            ProgressIndeterminate = true;

            string error = null;
            try
            {
                ToolsetTextureEngine.ExtractTexture(tex, filename, buildMips, format);
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
            showExtractionPanel = false;
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

            DisableFTSUpdating = true;

            // Clear exclusions
            for (int i = 0; i < FTSDLCs.Count; i++)
                FTSDLCs[i].IsChecked = false;

            for (int i = 0; i < FTSGameFiles.Count; i++)
                FTSGameFiles[i].IsChecked = false;

            DisableFTSUpdating = false;
            LoadFTSandTree(true);
        }


        public override void Search(string searchText)
        {
            // Was going to do a Stack-based previous search thing, and have future searches work off this, but it's simpler to just have a slight delay when getting results.
            // So now it waits for a search string to be more complete instead of searching when single characters are entered.

            TextureSearchResults.Clear();

            if (String.IsNullOrEmpty(searchText))
                return;

            ConcurrentBag<TreeTexInfo> names = new ConcurrentBag<TreeTexInfo>();
            ConcurrentBag<TreeTexInfo> pccs = new ConcurrentBag<TreeTexInfo>();
            ConcurrentBag<TreeTexInfo> expIDs = new ConcurrentBag<TreeTexInfo>();
            ConcurrentBag<TreeTexInfo> hashes = new ConcurrentBag<TreeTexInfo>();
            ConcurrentBag<TreeTexInfo> formats = new ConcurrentBag<TreeTexInfo>();

            string[] parts = searchText.Trim(' ').Split(' ');

            var keyWords = parts.Where(t => !t[0].isDigit()).ToList();
            var keyNumbers = parts.Where(t => t[0].isDigit()).ToList();

            bool requiresMoreThanOne = keyWords?.Count > 0 && keyNumbers?.Count > 0;

            Parallel.ForEach(Textures, texture =>
            {
                List<int> foundSearchables = new List<int>();
                for (int i = 0; i < texture.Searchables.Count; i++)
                {
                    //texture.Searchables[i].Contains(searchText, StringComparison.OrdinalIgnoreCase);
                    bool found = SearchSearchable(texture.Searchables[i], i, keyWords, keyNumbers, out int searchableType);                  

                    if (found)
                    {
                        if (!requiresMoreThanOne)
                        {
                            switch (searchableType)
                            {
                                case 0:
                                    names.Add(texture);
                                    break;
                                case 1:
                                    pccs.Add(texture);
                                    break;
                                case 2:
                                    expIDs.Add(texture);
                                    break;
                                case 3:
                                    hashes.Add(texture);
                                    break;
                                case 4:
                                    formats.Add(texture);
                                    break;
                            }
                            break;
                        }
                        else
                            foundSearchables.Add(searchableType);
                    }
                }

                if (requiresMoreThanOne)
                {
                    int highestPrioritySearchable = foundSearchables.Min();

                    switch (highestPrioritySearchable)
                    {
                        case 0:
                            names.Add(texture);
                            break;
                        case 1:
                            pccs.Add(texture);
                            break;
                        case 2:
                            expIDs.Add(texture);
                            break;
                        case 3:
                            hashes.Add(texture);
                            break;
                        case 4:
                            formats.Add(texture);
                            break;
                    }
                }
            });

            TextureSearchResults.AddRange(names);
            TextureSearchResults.AddRange(pccs);
            TextureSearchResults.AddRange(expIDs);
            TextureSearchResults.AddRange(hashes);
            TextureSearchResults.AddRange(formats);
        }

        public void Refresh()
        {
            UpdateFTS();
        }
    }
}
