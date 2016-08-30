using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer
{
    public static class ToolsetInfo
    {
        static Process currentProcess = null;
        static PerformanceCounter CPUCounter = null;
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

    }
}
