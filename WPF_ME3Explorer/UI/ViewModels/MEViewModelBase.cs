using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Threading;
using UsefulThings;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class MEViewModelBase<T> : ViewModelBase where T : AbstractTexInfo
    {
        protected CancellationTokenSource cts = new CancellationTokenSource();

        T selectedTexture = null;
        public T SelectedTexture
        {
            get
            {
                return selectedTexture;
            }
            set
            {
                SetProperty(ref selectedTexture, value);
                OnPropertyChanged(nameof(PCCsCheckAll));
            }
        }

        public bool? PCCsCheckAll
        {
            get
            {
                if (SelectedTexture?.PCCs == null)
                    return false;

                int num = SelectedTexture.PCCs.Where(pcc => pcc.IsChecked).Count();
                if (num == 0)
                    return false;
                else if (num == SelectedTexture.PCCs.Count)
                    return true;

                return null;
            }
            set
            {
                SelectedTexture.PCCs.AsParallel().ForAll(pcc => pcc.IsChecked = value == true);
            }
        }

        protected CommandHandler changeTree = null;
        public virtual CommandHandler ChangeTreeCommand
        {
            get
            {
                if (changeTree == null)
                    changeTree = new CommandHandler(new Action<object>(param =>
                    {
                        int version = ((TreeDB)param).GameVersion;
                        ChangeSelectedTree(version);
                        SetupCurrentTree();
                    }));

                return changeTree;
            }
        }

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

        CommandHandler cancelCommand = null;
        public CommandHandler CancelCommand
        {
            get
            {
                // Reset cts
                if (cts.IsCancellationRequested)
                    cts = new CancellationTokenSource();


                if (cancelCommand == null)
                    cancelCommand = new CommandHandler(() => cts.Cancel());

                return cancelCommand;
            }
        }

        bool busy = false;
        public bool Busy
        {
            get
            {
                return busy;
            }
            set
            {
                SetProperty(ref busy, value);
            }
        }

        string textureSearch = null;
        public virtual string TextureSearch
        {
            get
            {
                return textureSearch;
            }
            set
            {
                SetProperty(ref textureSearch, value);
                Search(value);
            }
        }

        public TreeDB[] Trees { get; private set; } = new TreeDB[3];
        public MTRangedObservableCollection<T> Textures { get; protected set; } = new MTRangedObservableCollection<T>();
        public MEDirectories.MEDirectories GameDirecs { get; set; } = new MEDirectories.MEDirectories();

        static readonly Object TreeLoadLocker = new object();

        string status = "Ready";
        public string Status
        {
            get
            {
                return status;
            }
            set
            {
                SetProperty(ref status, value);
            }
        }

        public double TaskBarProgress
        {
            get
            {
                return Progress / MaxProgress;
            }
        }

        int progress = 0;
        public int Progress
        {
            get
            {
                return progress;
            }
            set
            {
                Interlocked.Exchange(ref progress, value);
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(TaskBarProgress));
            }
        }

        int maxprogress = 1;
        public int MaxProgress
        {
            get
            {
                return maxprogress;
            }
            set
            {
                Interlocked.Exchange(ref maxprogress, value);
                OnPropertyChanged(nameof(MaxProgress));
                OnPropertyChanged(nameof(TaskBarProgress));
            }
        }

        bool progressIndeterminate = false;
        public bool ProgressIndeterminate
        {
            get
            {
                return progressIndeterminate;
            }
            set
            {
                SetProperty(ref progressIndeterminate, value);
            }
        }

        public TreeDB CurrentTree
        {
            get
            {
                return Trees[GameDirecs.GameVersion - 1];
            }
        }

        string memoryusage = null;
        public string MemoryUsage
        {
            get
            {
                return memoryusage;
            }
            set
            {
                SetProperty(ref memoryusage, value);
            }
        }

        string cpu = null;
        public string CPUUsage
        {
            get
            {
                return cpu;
            }
            set
            {
                SetProperty(ref cpu, value);
            }
        }

        string diskActivity = null;
        public string DiskActivity
        {
            get
            {
                return diskActivity;
            }
            set
            {
                SetProperty(ref diskActivity, value);
            }
        }

        string diskUsage = null;
        public string DiskUsage
        {
            get
            {
                return diskUsage;
            }
            set
            {
                SetProperty(ref diskUsage, value);
            }
        }

        public string ToolsetVersion
        {
            get
            {
                return ToolsetInfo.Version;
            }
        }

        public int GameVersion
        {
            get
            {
                return GameDirecs.GameVersion;
            }
            private set
            {
                GameDirecs.GameVersion = value;
                OnPropertyChanged(nameof(GameVersion));
            }
        }

        public int NumThreads
        {
            get
            {
                return Properties.Settings.Default.NumThreads;
            }
        }

        TimeSpan elapsedTime = TimeSpan.FromSeconds(0);
        public TimeSpan ElapsedTime
        {
            get
            {
                return elapsedTime;
            }
            set
            {
                SetProperty(ref elapsedTime, value);
            }
        }

        public int StartTime = 0;

        DispatcherTimer timer = new DispatcherTimer();

        static Task TreeLoader = null;

        static MEViewModelBase()
        {
            if (Properties.Settings.Default.UpgradeRequired)
                Properties.Settings.Default.Upgrade();

            // Set up NumThreads
            Properties.Settings.Default.NumThreads = Environment.ProcessorCount;
            Properties.Settings.Default.Save();
        }

        public MEViewModelBase()
        {
            Task.Run(async () => await Setup());

            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (sender, args) =>
            {
                MemoryUsage = ToolsetInfo.MemoryUsage;
                CPUUsage = ToolsetInfo.CPUUsage;
                DiskActivity = ToolsetInfo.DiskActiveTime;
                DiskUsage = ToolsetInfo.DiskTransferRate;


                if (StartTime != 0)
                    ElapsedTime = TimeSpan.FromMilliseconds(Environment.TickCount - StartTime);
            };
            timer.Start();
        }

        protected Task Setup()
        {
            lock (TreeLoadLocker)
                if (TreeLoader != null)
                    return TreeLoader.ContinueWith(t => Setup());   //  Effectively wait for the original loading to be complete, then come back in on this thread and set up it's trees.

            // Setup Files
            var gameFileLoaderTask = Task.Run(() =>
            {
                int start = Environment.TickCount;
                var temp = MEDirectories.MEDirectories.ME1Files;
                temp = MEDirectories.MEDirectories.ME2Files;
                temp = MEDirectories.MEDirectories.ME3Files;
                Console.WriteLine($"File Load: {TimeSpan.FromMilliseconds(Environment.TickCount - start)}");
            });
            

            // Setup Trees
            Trees[0] = new TreeDB(1);
            Trees[1] = new TreeDB(2);
            Trees[2] = new TreeDB(3);


            /// Can take a long time if disk is busy
            var TreeLoadTask = Task.Run(async () =>
            {
                int start = Environment.TickCount;
                BufferBlock<int> TreeBuffer = new BufferBlock<int>();
                TransformBlock<int, int> TreeReader = new TransformBlock<int, int>(ind => { Trees[ind].ReadFromFile(); return ind; }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1, MaxDegreeOfParallelism = 1 });
                TransformBlock<int, int> TreeConstructor = new TransformBlock<int, int>(ind => { Trees[ind].ConstructTree(); return ind; });
                ActionBlock<int> TreeCheckBoxLinker = new ActionBlock<int>(ind =>
                {
                    if (Trees[ind].Valid)
                        SetupPCCCheckBoxLinking(Trees[ind].Textures);
                });

                // Link pipeline
                TreeBuffer.LinkTo(TreeReader, new DataflowLinkOptions { PropagateCompletion = true });
                TreeReader.LinkTo(TreeConstructor, new DataflowLinkOptions { PropagateCompletion = true });
                TreeConstructor.LinkTo(TreeCheckBoxLinker, new DataflowLinkOptions { PropagateCompletion = true });

                // Producer
                TreeBuffer.Post(0);
                TreeBuffer.Post(1);
                TreeBuffer.Post(2);
                TreeBuffer.Complete();

                await TreeCheckBoxLinker.Completion;
                Console.WriteLine($"Tree load Load: {TimeSpan.FromMilliseconds(Environment.TickCount - start)}");
            });

            lock (TreeLoadLocker)
                TreeLoader = Task.WhenAll(TreeLoadTask, gameFileLoaderTask);

            return TreeLoader;
        }

        public virtual void Search(string searchText)
        {
            if (String.IsNullOrEmpty(searchText))
                return;

            Parallel.ForEach(Textures, texture =>
            {
                bool found = texture.Searchables.Any(searchable => searchable.Contains(searchText, StringComparison.OrdinalIgnoreCase));
                texture.IsHidden = !found;
            });
        }

        protected virtual void SetupCurrentTree()
        {
            Trees[GameVersion - 1].IsSelected = true;

            // KFreon: Populate game files info with tree info
            if (GameDirecs.Files?.Count <= 0)
            {
                DebugOutput.PrintLn($"Game files not found for ME{GameDirecs.GameVersion} at {GameDirecs.PathBIOGame}");
                Status = "Game Files not found!";
                Busy = false;
                return;
            }

            ToolsetInfo.SetupDiskCounters(Path.GetPathRoot(GameDirecs.BasePath).TrimEnd('\\'));
        }

        public virtual void ChangeSelectedTree(int game)
        {
            // If already selected, do nothing.
            if (GameVersion == game)
                return;

            GameVersion = game;

            // Clear tree selections
            Trees[0].IsSelected = false;
            Trees[1].IsSelected = false;
            Trees[2].IsSelected = false;

            // Select new tree
            Trees[GameVersion - 1].IsSelected = true;

            TextureSearch = null; // Clear search
            OnPropertyChanged(nameof(CurrentTree));
        }

        protected void SetupPCCCheckBoxLinking(IEnumerable<TreeTexInfo> texes)
        {
            // Add checkbox listener for linking the check action of individual pccs to top level Check All
            foreach (var tex in texes)
                foreach (var pccentry in tex.PCCs.Where(pcc => !pcc.CheckBoxListenerAttached))
                {
                    pccentry.PropertyChanged += (source, args) =>
                    {
                        if (args.PropertyName == nameof(pccentry.IsChecked))
                            OnPropertyChanged(nameof(PCCsCheckAll));
                    };
                    pccentry.CheckBoxListenerAttached = true;
                }
                    
        }

        protected string BuildTexDetailsForCSV(T tex)
        {
            StringBuilder texDetails = new StringBuilder();
            texDetails.AppendLine($"Texture details for:, {tex.TexName}, with hash, {ToolsetTextureEngine.FormatTexmodHashAsString(tex.Hash)}");
            texDetails.AppendLine();

            // Details
            texDetails.AppendLine($"Game Version, {tex.GameVersion}");
            texDetails.AppendLine($"Format, {ToolsetTextureEngine.StringifyFormat(tex.Format)}");
            texDetails.AppendLine($"Number of Mips, {tex.Mips}");
            texDetails.AppendLine($"Dimensions (Width x Height), {tex.Width}x{tex.Height}");

            var treetex = tex as TreeTexInfo;
            if (treetex != null)
            {
                texDetails.AppendLine($"Package, {treetex.FullPackage}");
                texDetails.AppendLine($"LODGroup (if available), {treetex.LODGroup}");
                texDetails.AppendLine($"Storage Type (if available), {treetex.StorageType}");
                texDetails.AppendLine($"Texture Cache (if available), {treetex.TextureCache}");
            }

            texDetails.AppendLine();
            texDetails.AppendLine();
            texDetails.AppendLine();

            //////// PCCS ////////
            // Column Headers
            texDetails.AppendLine("Is Checked, Name, Export ID");

            // PCC details
            foreach (PCCEntry pcc in tex.PCCs)
                texDetails.AppendLine($"{pcc.IsChecked}, {pcc.Name}, {pcc.ExpID}");

            return texDetails.ToString();
        }

        internal void ExportSelectedTexturePCCList(string fileName)
        {
            Busy = true;
            Status = $"Exporting PCC list for: {SelectedTexture.TexName}";
            Progress = 0;

            using (StreamWriter writer = new StreamWriter(fileName))
                writer.Write(BuildTexDetailsForCSV(SelectedTexture));

            Status = $"PCCs exported to: {fileName}";
            Progress = MaxProgress;
            Busy = false;
        }
    }
}
