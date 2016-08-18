using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using UsefulThings.WPF;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class MEViewModelBase<T> : ViewModelBase
    {
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

        public MTRangedObservableCollection<T> Textures { get; protected set; } = new MTRangedObservableCollection<T>();
        public TreeDB[] Trees { get; private set; } = new TreeDB[3];
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

        int progress = 0;
        public int Progress
        {
            get
            {
                return progress;
            }
            set
            {
                SetProperty(ref progress, value);
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
                SetProperty(ref maxprogress, value);
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
        }

        int numthreads = 4;
        public int NumThreads
        {
            get
            {
                return numthreads;
            }
            set
            {
                SetProperty(ref numthreads, value);
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
        

        public MEViewModelBase()
        {
            if (Properties.Settings.Default.UpgradeRequired)
                Properties.Settings.Default.Upgrade();

            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (sender, args) =>
            {
                MemoryUsage = ToolsetInfo.MemoryUsage;
                CPUUsage = ToolsetInfo.CPUUsage;

                if (StartTime != 0)
                    ElapsedTime = TimeSpan.FromMilliseconds(Environment.TickCount - StartTime);
            };
            timer.Start();
            Trees[0] = new TreeDB(1);
            Trees[1] = new TreeDB(2);
            Trees[2] = new TreeDB(3);

            NumThreads = Properties.Settings.Default.NumThreads;
        }

        public void LoadTrees()
        {
            Trees[0].ReadFromFile();
            Trees[1].ReadFromFile();
            Trees[2].ReadFromFile();
        }

        
    }
}
