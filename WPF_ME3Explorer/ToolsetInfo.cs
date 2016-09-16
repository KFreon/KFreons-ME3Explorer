using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UsefulThings;
using WPF_ME3Explorer.UI;

namespace WPF_ME3Explorer
{
    public static class ToolsetInfo
    {
        public static bool Closing { get; internal set; }


        static TPFTools tpfToolsInstance = null;
        public static TPFTools TPFToolsInstance
        {
            get
            {
                if (!Closing && tpfToolsInstance?.IsClosed != false) // So if it's null or true, it resets the instance
                    tpfToolsInstance = new TPFTools();

                return tpfToolsInstance;
            }
        }

        static Texplorer texplorerInstance = null;
        public static Texplorer TexplorerInstance
        {
            get
            {
                if (!Closing && texplorerInstance?.IsClosed != false) // So if it's null or true, it resets the instance
                    texplorerInstance = new Texplorer();

                return texplorerInstance;
            }
        }

        static Modmaker modmakerInstance = null;
        public static Modmaker ModmakerInstance
        {
            get
            {
                if (!Closing && modmakerInstance?.IsClosed != false) // So if it's null or true, it resets the instance
                    modmakerInstance = new Modmaker();

                return modmakerInstance;
            }
        }



        static Process currentProcess = null;
        static PerformanceCounter CPUCounter = null;
        static PerformanceCounter DiskTransferCounter = null;
        static PerformanceCounter DiskActivityCounter = null;
        static ComputerInfo info = new ComputerInfo();

        public static ulong AvailableRam
        {
            get
            {
                return info.AvailablePhysicalMemory;
            }
        }

        static ToolsetInfo()
        {
            currentProcess = Process.GetCurrentProcess();
            CPUCounter = new PerformanceCounter("Process", "% Processor Time", currentProcess.ProcessName);
        }

        public static void SetupDiskCounters(string Disk)
        {
            // Don't need to setup if don't need to.
            if (DiskActivityCounter != null)
                return;

            // Get Disk Instance of interest
            var cat = new System.Diagnostics.PerformanceCounterCategory("LogicalDisk");
            var names = cat.GetInstanceNames();

            string instance = names.FirstOrDefault(name => name.Contains(Disk, StringComparison.OrdinalIgnoreCase));
            if (instance == null)
                return;

            DiskActivityCounter = new PerformanceCounter("LogicalDisk", "% Disk Time", instance);
            DiskTransferCounter = new PerformanceCounter("LogicalDisk", "Disk Bytes/sec", instance);
        }

        public static string MemoryUsage
        {
            get
            {
                currentProcess.Refresh();
                return UsefulThings.General.GetFileSizeAsString(currentProcess.PrivateMemorySize64);
            }
        }

        public static string CPUUsage
        {
            get
            {
                currentProcess.Refresh();
                return Math.Round(CPUCounter.NextValue() / Environment.ProcessorCount, 1).ToString() + "%";
            }
        }

        public static string Version
        {
            get
            {
                return UsefulThings.General.GetStartingVersion();
            }
        }

        public static string DiskActiveTime
        {
            get
            {
                if (DiskActivityCounter == null)
                    return null;

                return Math.Round(DiskActivityCounter.NextValue(), 1) + "%";
            }
        }

        public static string DiskTransferRate
        {
            get
            {
                if (DiskTransferCounter == null)
                    return null;

                return UsefulThings.General.GetFileSizeAsString(DiskTransferCounter.NextValue());
            }
        }

    }
}
