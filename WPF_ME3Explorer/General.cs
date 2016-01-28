using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmaroK86.ImageFormat;
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer
{
    public static class General
    {
        /// <summary>
        /// Returns hash as a string in the 0xhash format.
        /// </summary>
        /// <param name="hash">Hash as a uint.</param>
        /// <returns>Hash as a string.</returns>
        public static string FormatTexmodHashAsString(uint hash)
        {
            return "0x" + System.Convert.ToString(hash, 16).PadLeft(8, '0').ToUpper();
        }

        /// <summary>
        /// Returns a uint of a hash in string format. 
        /// </summary>
        /// <param name="line">String containing hash in texmod log format of name|0xhash.</param>
        /// <returns>Hash as a uint.</returns>
        public static uint FormatTexmodHashAsUint(string line)
        {
            return uint.Parse(line.Split('|')[0].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
        }


        /// <summary>
        /// Load an image into one of AK86's classes.
        /// </summary>
        /// <param name="im">AK86 image already, just return it unless null. Then load from fileToLoad.</param>
        /// <param name="fileToLoad">Path to file to be loaded. Irrelevent if im is provided.</param>
        /// <returns>AK86 Image file.</returns>
        public static ImageFile LoadAKImageFile(byte[] imgData)
        {
            ImageFile imgFile = null;

            try
            {
                imgFile = new DDS(null, imgData);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to load image as AK", "AKImage", e);
            }

            return imgFile;
        }

        public static void UpgradeProperties()
        {
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
        }

        public static int SetupThreadCount()
        {
            int currentThreads = Properties.Settings.Default.NumThreads;
            if (currentThreads == 0)
                currentThreads = SetNumThreads(false);

            return currentThreads;
        }

        /// <summary>
        /// Sets number of threads in Project Settings.
        /// </summary>
        /// <param name="User">True = asks user for threads.</param>
        /// <param name="AutoSave">True = saves Settings automatically.</param>
        /// <returns>Number of threads set to.</returns>
        public static int SetNumThreads(bool User, bool AutoSave = true)
        {
            int threads = -1;
            try
            {
                if (User)
                    // KFreon: Get user input
                    while (true)
                        if (int.TryParse(Microsoft.VisualBasic.Interaction.InputBox("Set number of threads to use in multi-threaded programs: ", "Threads", "" + Environment.ProcessorCount), out threads))
                            break;
                        else
                            threads = Environment.ProcessorCount;

                // KFreon: Checks
                if (threads <= 0)
                    threads = Environment.ProcessorCount;


                Properties.Settings.Default.NumThreads = threads;

                // KFreon: Save Properties if requested.
                if (AutoSave)
                    SaveSettings();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to set numThreads: ", "Misc", e);
            }
            return threads;
        }

        /// <summary>
        /// Saves Project settings safely.
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                WPF_ME3Explorer.Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to save settings: ", "MEDirectories", e);
            }
        }
    }
}
