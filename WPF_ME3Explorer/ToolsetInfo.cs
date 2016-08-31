using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UsefulThings;

namespace WPF_ME3Explorer
{
    public static class ToolsetInfo
    {
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
                return Math.Round(DiskActivityCounter.NextValue(), 1) + "%";
            }
        }

        public static string DiskTransferRate
        {
            get
            {
                return UsefulThings.General.GetFileSizeAsString(DiskTransferCounter.NextValue());
            }
        }
    }
}
