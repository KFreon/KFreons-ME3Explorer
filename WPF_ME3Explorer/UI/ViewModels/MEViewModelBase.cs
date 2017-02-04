using CSharpImageLibrary;
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
        #region Properties
        protected CancellationTokenSource cts = new CancellationTokenSource();

        public bool CancellationRequested
        {
            get
            {
                return cts.IsCancellationRequested;
            }
        }

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
                        info.Closed += (unused1, unused2) =>
                        {
                            GameDirecs.RefreshListeners();  // Refresh all game directory related info once window is closed. 

                            // If selected game was altered. Setup current tree again.
                            if (GameVersion == version)
                                SetupCurrentTree();
                        };
                        info.Show();
                        info.Activate();
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
                    cancelCommand = new CommandHandler(() =>
                    {
                        cts.Cancel();
                        OnPropertyChanged(nameof(CancellationRequested));
                    });

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
            }
        }

        public MTRangedObservableCollection<T> Textures { get; protected set; } = new MTRangedObservableCollection<T>();
        public MEDirectories.MEDirectories GameDirecs { get; set; } = new MEDirectories.MEDirectories();

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
                // This stops taskbar element from staying green/red forever.
                int prog = Progress / MaxProgress;
                if (prog == 1)
                    return 0;
                else
                    return prog;
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

        public TreeDB[] Trees
        {
            get
            {
                return ToolsetTextureEngine.Trees;
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
            set
            {
                Properties.Settings.Default.NumThreads = value;
                Properties.Settings.Default.Save();
                OnPropertyChanged(nameof(NumThreads));
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
        #endregion Properties


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

        /// <summary>
        /// Needed here instead of just incorporating it into constructor as GameVersion is required and can be different between tools.
        /// </summary>
        protected void Setup()
        {
            Busy = true;

            // Setup Trees
            for (int i = 0; i < 3; i++)
            {
                if (Trees[i].Valid)
                    SetupPCCCheckBoxLinking(Trees[i].Textures);
            }

            SetupCurrentTree();

            Busy = false;
        }

        public virtual void Search(string searchText)
        {
            if (String.IsNullOrEmpty(searchText))
                return;


            string[] parts = searchText.Trim(' ').Split(' ');

            var keyWords = parts.Where(t => !t[0].isDigit()).ToList();
            var keyNumbers = parts.Where(t => t[0].isDigit()).ToList();

            bool requiresMoreThanOne = keyWords?.Count > 0 && keyNumbers?.Count > 0;

            Parallel.ForEach(Textures, texture =>
            {
                for (int i = 0; i < texture.Searchables.Count; i++)
                {
                    bool found = SearchSearchable(texture.Searchables[i], i, keyWords, keyNumbers, out int searchableType);
                    texture.IsHidden = !found;
                }
            });
        }

        protected bool SearchSearchable(string searchable, int searchableInd, IEnumerable<string> keyWords, IEnumerable<string> keyNumbers, out int searchableType)
        {
            searchableType = 0;

            // Name is always first and there's only 1 of them.
            if (searchableInd != 0)
            {
                if (searchable.StartsWith("0x"))
                    searchableType = 3;
                else if (searchable[0].isDigit())
                    searchableType = 2;
                else if (searchable.Contains('\\'))
                    searchableType = 1;
                else if (searchable.Length < 16)  // Formats. Shouldn't be any paths shorter than 16, and all things that are shorter should be caught in previous 'ifs'.
                    searchableType = 4;
            }

            bool found = true;

            // SEARCH
            if (searchableType == 2 || searchableType == 3)
            {
                foreach (string keynumber in keyNumbers)
                {
                    if (!searchable.Contains(keynumber, StringComparison.CurrentCultureIgnoreCase))
                    {
                        found = false;
                        break;
                    }
                }
            }
            else
            {
                // Keyword search
                foreach (string keyword in keyWords)
                {
                    if (!searchable.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
                    {
                        found = false;
                        break;
                    }
                }
            }

            return found;
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

            if (GameDirecs.BasePath != null)
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
        

        internal void ExportSelectedTexturePCCList(string fileName)
        {
            Busy = true;
            Status = $"Exporting PCC list for: {SelectedTexture.TexName}";
            Progress = 0;

            using (StreamWriter writer = new StreamWriter(fileName))
                writer.Write(ToolsetTextureEngine.BuildTexDetailsForCSV(SelectedTexture));

            Status = $"PCCs exported to: {fileName}";
            Progress = MaxProgress;
            Busy = false;
        }
    }
}
