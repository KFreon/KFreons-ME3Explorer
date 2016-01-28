using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using UsefulThings;
using WPF_ME3Explorer.Debugging;

namespace MEDirectories
{
    public static class ME3Directory
    {
        public static bool DoesGameExist
        {
            get
            {
                return GamePath == null ? false : Directory.Exists(GamePath);
            }
        }

        private static List<String> basegameFiles = null;
        public static List<string> BaseGameFiles
        {
            get
            {
                if (basegameFiles == null && !String.IsNullOrEmpty(cookedPath) && Directory.Exists(cookedPath))
                    basegameFiles = MEDirectories.EnumerateGameFiles(3, cookedPath);

                return basegameFiles;
            }
        }

        static List<string> dlcfiles = null;
        public static List<string> DLCFiles
        {
            get
            {
                if (dlcfiles == null && !String.IsNullOrEmpty(DLCPath) && Directory.Exists(DLCPath))
                    dlcfiles = MEDirectories.EnumerateGameFiles(3, DLCPath);

                return dlcfiles;
            }
        }

        public static IEnumerable<string> GameFiles
        {
            get
            {
                DebugOutput.PrintLn("ME3 COOKED: " + cookedPath);
                DebugOutput.PrintLn("ME3 DLC: " + DLCPath);

                IEnumerable<string> files = null;
                if (BaseGameFiles != null)
                    files = new List<string>(BaseGameFiles);

                if (DLCFiles != null)
                    files.Concat(DLCFiles);

                return files;
            }
        }

        static List<string> archives = null;
        public static List<string> Archives
        {
            get
            {
                if (archives == null)
                {
                    archives = MEDirectories.EnumerateGameFiles(3, BaseGameFiles, f => f.EndsWith(".tfc"));
                    if (archives.Count == 0)
                    {
                        archives = null;
                        return null;
                    }
                    archives.AddRange(MEDirectories.EnumerateGameFiles(3, DLCFiles, f => f.EndsWith(".tfc")));
                }

                return archives;
            }
        }

        public static string GamePath { get; set; }

        static string exepath = null;
        public static string ExePath
        {
            get
            {
                if (exepath == null)
                {
                    if (GamePath != null && Directory.Exists(GamePath))
                        exepath = Directory.EnumerateFiles(GamePath, "*", SearchOption.AllDirectories).First(t => t.Contains("MASSEFFECT3.EXE", StringComparison.CurrentCultureIgnoreCase));
                }

                return exepath;
            }
        }

        public static string tocFile { get { return (GamePath != null) ? Path.Combine(GamePath, @"BIOGame\PCConsoleTOC.bin") : null; } }
        public static string cookedPath { get { return (GamePath != null) ? Path.Combine(GamePath, @"BIOGame\CookedPCConsole\") : null; } }
        public static string DLCPath { get { return (GamePath != null) ? Path.Combine(GamePath, @"BIOGame\DLC\") : null; } }

        public static string BioWareDocPath { get { return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect 3\"; } }
        public static string GamerSettingsIniFile { get { return BioWareDocPath + @"BIOGame\Config\GamerSettings.ini"; } }

        public static string DLCFilePath(string DLCName)
        {
            string fullPath = DLCPath + DLCName + @"\CookedPCConsole\Default.sfar";
            if (File.Exists(fullPath))
                return fullPath;
            else
                throw new FileNotFoundException("Invalid DLC path " + fullPath);
        }

        static ME3Directory()
        {
            string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
            string subkey = @"BioWare\Mass Effect 3";
            string keyName;

            keyName = hkey32 + subkey;
            string test = (string)Microsoft.Win32.Registry.GetValue(keyName, "Install Dir", null);
            if (test != null)
            {
                GamePath = test;
                return;
            }

            keyName = hkey64 + subkey;
            GamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Install Dir", null);
            if (GamePath != null)
            {
                GamePath = GamePath + "\\";
                return;
            }
        }
    }
}
