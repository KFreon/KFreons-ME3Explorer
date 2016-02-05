using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using UsefulThings.WPF;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public abstract class MEViewModelBase<T> : ViewModelBase where T : AbstractTexInfo
    {
        public ICollectionView ItemsView { get; set; }
        public MEDirectories.MEDirectories MEExDirecs { get; private set; }
        public MTRangedObservableCollection<TreeDB> Trees { get; set; }
        public MTRangedObservableCollection<T> Textures { get; set; }

        bool searching = false;
        public bool Searching
        {
            get
            {
                return searching;
            }
            set
            {
                SetProperty(ref searching, value);
            }
        }

        string searchText = null;
        public string SearchText
        {
            get
            {
                return searchText;
            }
            set
            {
                SetProperty(ref searchText, value);
                Searching = !String.IsNullOrEmpty(value);
                Search(value);
            }
        }

        #region Status Bar
        string elapsedTime = null;
        public string ElapsedTime
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

        string toolsetversionstring = null;
        public string ToolsetVersionString
        {
            get
            {
                return toolsetversionstring;
            }
            set
            {
                SetProperty(ref toolsetversionstring, value);
            }
        }

        int toolsetrevision = -1;
        public int ToolsetRevision
        {
            get
            {
                return toolsetrevision;
            }
            set
            {
                SetProperty(ref toolsetrevision, value);
            }
        }

        bool primaryIndeterminate = false;
        public bool PrimaryIndeterminate
        {
            get
            {
                return primaryIndeterminate;
            }
            set
            {
                SetProperty(ref primaryIndeterminate, value);
            }
        }

        int primaryProgress = 0;
        public int PrimaryProgress
        {
            get
            {
                return primaryProgress;
            }
            set
            {
                SetProperty(ref primaryProgress, value);
            }
        }

        int maxPrimaryProgress = 1;
        public int MaxPrimaryProgress
        {
            get
            {
                return maxPrimaryProgress;
            }
            set
            {
                SetProperty(ref maxPrimaryProgress, value);
            }
        }

        string primaryStatus = null;
        public string PrimaryStatus
        {
            get
            {
                return primaryStatus;
            }
            set
            {
                SetProperty(ref primaryStatus, value);
            }
        }
        #endregion


        public int GameVersion
        {
            get
            {
                return MEExDirecs.GameVersion;
            }
        }

        string memoryUsage = null;
        public string MemoryUsage
        {
            get
            {
                return memoryUsage; 
            }
            set
            {
                SetProperty(ref memoryUsage, value);
            }
        }

        PerformanceCounter cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
        public float CPUUsage
        {
            get
            {
                return cpu.NextValue();
            }
        }

        protected DispatcherTimer timer = null;
        protected Stopwatch MainStopWatch = null;

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

        bool doesGame1Exist = false;
        public bool DoesGame1Exist
        {
            get
            {
                return doesGame1Exist;
            }
            set
            {
                SetProperty(ref doesGame1Exist, value);
            }
        }


        bool doesGame2Exist = false;
        public bool DoesGame2Exist
        {
            get
            {
                return doesGame2Exist;
            }
            set
            {
                SetProperty(ref doesGame2Exist, value);
            }
        }


        bool doesGame3Exist = false;
        public bool DoesGame3Exist
        {
            get
            {
                return doesGame3Exist;
            }
            set
            {
                SetProperty(ref doesGame3Exist, value);
            }
        }

        int numThreads = 4;
        public int NumThreads
        {
            get
            {
                return numThreads;
            }
            set
            {
                SetProperty(ref numThreads, value);
            }
        }

        bool isLoaded = false;
        public bool IsLoaded
        {
            get
            {
                return isLoaded;
            }
            set
            {
                SetProperty(ref isLoaded, value);
            }
        }

        public ICommand ShowGameInfo { get; set; }
        public ICommand ChangeTreeCommand { get; set; }

        protected CancellationTokenSource cts { get; set; }

        public virtual void ChangeTree(TreeDB newlySelectedTree)
        {
            foreach (var tree in Trees)
                tree.IsSelected = tree == newlySelectedTree;

            MEExDirecs.GameVersion = newlySelectedTree.GameVersion;
            OnPropertyChanged("GameVersion");

        }

        public MEViewModelBase(int game, string execFolder = null)
        {
            WPF_ME3Explorer.General.UpgradeProperties();

            MEExDirecs = new MEDirectories.MEDirectories(game, execFolder);
            timer = new DispatcherTimer();
            MainStopWatch = new Stopwatch();
            timer.Interval = TimeSpan.FromSeconds(1);

            timer.Tick += (sender, e) =>
            {
                if (MainStopWatch.IsRunning)
                    ElapsedTime = MainStopWatch.Elapsed.ToString("hh':'mm':'ss");

                MemoryUsage = UsefulThings.General.GetFileSizeAsString(Environment.WorkingSet);
            };
            timer.Start();


            NumThreads = Properties.Settings.Default.NumThreads;

            DoesGame1Exist = MEExDirecs.DoesGame1Exist;
            DoesGame2Exist = MEExDirecs.DoesGame2Exist;
            DoesGame3Exist = MEExDirecs.DoesGame3Exist;

            ToolsetVersionString = "Version: " + UsefulThings.General.GetCallingVersion();

            string[] parts = ToolsetVersionString.Split('.');
            ToolsetRevision = int.Parse(parts[parts.Length - 2]);

            cts = new CancellationTokenSource();

            ShowGameInfo = new CommandHandler(t => 
            {
                int passedGame = int.Parse((string)t);
                var info = new GameInfoViewer($"Mass Effect {passedGame}", passedGame);
                if (info.ShowDialog() == true)
                    MEExDirecs.SetupPathing();
            });

            ChangeTreeCommand = new CommandHandler(tree =>
            {
                ChangeTree((TreeDB)tree);
            });

            Textures = new MTRangedObservableCollection<T>();
            ItemsView = CollectionViewSource.GetDefaultView(Textures);
            ItemsView.Filter = obj => ((AbstractTexInfo)obj).IsSearchVisible;
        }

        protected bool LoadTrees(IList<TreeDB> Trees, TreeDB CurrentTree, bool isChanging)
        {
            if (isChanging)
            {
                if (!CurrentTree.Valid)
                    CurrentTree.LoadTree();
            }
            else
            {
                Trees[0].LoadTree();
                Trees[1].LoadTree();
                Trees[2].LoadTree();
            }

            if (CurrentTree.Valid)
            {
                CurrentTree.ConstructTree();
                PrimaryStatus = "Ready.";
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Shutdown()
        {
            ItemsView = null;
            timer.Stop();
            MainStopWatch.Stop();
        }

        public void Search(string text)
        {
            foreach (var item in Textures)
                item.Search(text);

            ItemsView.Refresh();
        }
    }
}
