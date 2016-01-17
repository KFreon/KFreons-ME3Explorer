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
    public static class ME1Directory
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
                    basegameFiles = MEDirectories.EnumerateGameFiles(1, cookedPath);

                return basegameFiles;
            }
        }

        static List<string> dlcfiles = null;
        public static List<string> DLCFiles
        {
            get
            {
                if (dlcfiles == null && !String.IsNullOrEmpty(DLCPath) && Directory.Exists(DLCPath))
                    dlcfiles = MEDirectories.EnumerateGameFiles(1, DLCPath);

                return dlcfiles;
            }
        }

        public static IEnumerable<string> GameFiles
        {
            get
            {
                DebugOutput.PrintLn("ME1 COOKED: " + cookedPath);
                DebugOutput.PrintLn("ME1 DLC: " + DLCPath);

                IEnumerable<string> files = null;
                if (BaseGameFiles != null)
                    files = new List<string>(BaseGameFiles);

                if (DLCFiles != null)
                    files.Concat(DLCFiles);

                return files;
            }
        }

        static string gamePath = null;
        public static string GamePath
        {
            get
            {
                return gamePath;
            }
            set
            {
                if (!String.IsNullOrEmpty(value))
                {
                    string temp = value;
                    if (temp.Contains("BioGame", StringComparison.OrdinalIgnoreCase))
                        temp = temp.Substring(0, temp.ToLower().LastIndexOf("biogame"));
                    gamePath = temp;
                }
            }
        }

        static string exepath = null;
        public static string ExePath
        {
            get
            {
                if (exepath == null)
                {
                    if (!String.IsNullOrEmpty(GamePath) && Directory.Exists(GamePath))
                        exepath = Directory.EnumerateFiles(GamePath, "*", SearchOption.AllDirectories).First(t => t.ToUpperInvariant().Contains("MASSEFFECT.EXE"));
                }

                return exepath;
            }
        }

        public static string cookedPath { get { return (gamePath != null) ? Path.Combine(gamePath, gamePath.Contains("biogame", StringComparison.OrdinalIgnoreCase) ? @"CookedPC\" : @"BioGame\CookedPC\") : null; } }
        public static string DLCPath { get { return (gamePath != null) ? gamePath.Contains("biogame", StringComparison.OrdinalIgnoreCase) ? Path.Combine(Path.GetDirectoryName(gamePath), @"DLC\") : Path.Combine(gamePath, @"DLC\") : null; } }

        public static string BioWareDocPath { get { return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\"; } }
        public static string GamerSettingsIniFile { get { return BioWareDocPath + @"BIOGame\Config\GamerSettings.ini"; } }

        public static string DLCFilePath(string DLCName)
        {
            string fullPath = DLCPath + DLCName + @"\CookedPC\";
            if (File.Exists(fullPath))
                return fullPath;
            else
                throw new FileNotFoundException("Invalid DLC path " + fullPath);
        }

        static ME1Directory()
        {
            string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
            string subkey = @"BioWare\Mass Effect";
            string keyName;

            keyName = hkey32 + subkey;
            string test = (string)Microsoft.Win32.Registry.GetValue(keyName, "Path", null);
            if (test != null)
            {
                gamePath = test;
                return;
            }

            keyName = hkey64 + subkey;
            gamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Path", null);
            if (gamePath != null)
            {
                gamePath = gamePath + "\\";
                return;
            }
        }
    }
}
