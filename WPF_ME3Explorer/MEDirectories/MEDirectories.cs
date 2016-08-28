using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer.MEDirectories
{
    public class MEDirectories : ViewModelBase
    {
        static Dictionary<string, string> CommonDLCNames = new Dictionary<string, string>()
        {
            // ME1
            { "DLC_UNC", "Bring Down the Sky" },
            { "DLC_Vegas", "Pinnacle Station" },

            // ME2
            { "DLC_CER_02", "Aegis Pack" },
            { "DLC_CON_Pack01", "Alternate Appearance Pack 1" },
            { "DLC_CON_Pack02", "Alternate Appearance Pack 2" },
            { "DLC_CER_Arc", "Arc Projector" },
            { "DLC_EXP_Part02", "Arrival" },
            { "DLC_PRE_DA", "Blood Dragon Armor" },
            { "DLC_PRE_Cerberus", "Cerberus Weapon and Armor" },
            { "DLC_PRE_Collectors", "Collectors' Armor and Assault Rifle" },
            { "DLC_MCR_03", "Equalizer Pack" },
            { "DLC_MCR_01", "Firepower Pack" },
            { "DLC_UNC_Hammer01", "Firewalker" },
            { "DLC_PRE_General", "Inferno Armor" },
            { "DLC_EXP_Part01", "Lair of the Shadow Broker" },
            { "DLC_HEM_MT", "Kasumi - Stolen Memory" },
            { "DLC_PRE_Incisor", "M-29 Incisor" },
            { "DLC_DHME1", "Mass Effect: Genesis" },
            { "DLC_UNC_Monument01", "Normandy Crash Site" },
            { "DLC_UNC_Pack01", "Overlord" },
            { "DLC_PRO_Pepper02", "Recon Hood" },
            { "DLC_PRO_Gulp01", "Sentery Interface" },
            { "DLC_PRE_Gamestop", "Terminus Weapon and Armor" },
            { "DLC_PRO_Pepper01", "Umbra Visor" },
            { "DLC_HEN_VT", "Zaeed - The Price of Revenge" },           

            // ME3
            { "DLC_CON_APP01", "Alternate Appearance Pack 1" },
            { "DLC_CON_END", "Extended Cut" },
            { "DLC_CON_GUN01", "FireFight Pack" },
            { "DLC_CON_GUN02", "Groundside Resistance Pack" },
            { "DLC_CON_MP1" , "Resurgence Multiplayer Expansion" },
            { "DLC_CON_MP2", "Rebellion Multiplayer Expansion" },
            { "DLC_CON_MP3", "Earth Multiplayer Expansion" },
            { "DLC_CON_MP4", "Retaliation Multiplayer Expansion" },
            { "DLC_CON_MP5", "Reckoning Multiplayer Expansion" },
            { "DLC_EXP_Pack001", "Leviathan" },
            { "DLC_EXP_Pack002", "Omega" },
            { "DLC_EXP_Pack003", "Citadel" },
            { "DLC_EXP_Pack003_Base", "Citadel" },
            { "DLC_HEN_PR", "From Ashes" },
            { "DLC_OnlinePassHidCE", "Collector's Edition Bonus Content" },
            { "DLC_UPD_Patch01", "Patch" },
            { "DLC_UPD_Patch02", "Patch" }
        };

        public static string CachePath { get; set; }

        static List<string> BIOGames = new List<string>() { "", "", "" };

        int gameVersion = 0;
        public int GameVersion
        {
            get
            {
                return gameVersion;
            }
            set
            {
                SetProperty(ref gameVersion, value);
                BasePathLength = BasePath == null ? -1 : BasePath.Length + 1;
            }
        }

        #region Individuals based on BIOGames
        public static string ME1BIOGame
        {
            get
            {
                return BIOGames[0];
            }
            set
            {
                // Clear stored files if path is different. Files will be repopulated when next requested.
                if (BIOGames[0] != value)
                    me1Files = null;

                BIOGames[0] = value;
            }
        }

        public static string ME2BIOGame
        {
            get
            {
                return BIOGames[1];
            }
            set
            {
                // Clear stored files if path is different. Files will be repopulated when next requested.
                if (BIOGames[1] != value)
                    me2Files = null;
                BIOGames[1] = value;
            }
        }

        public static string ME3BIOGame
        {
            get
            {
                return BIOGames[2];
            }
            set
            {
                // Clear stored files if path is different. Files will be repopulated when next requested.
                if (BIOGames[2] != value)
                    me3Files = null;

                BIOGames[2] = value;
            }
        }

        public static string ME1Cooked
        {
            get
            {
                if (String.IsNullOrEmpty(ME1BIOGame))
                    return null;

                return Path.Combine(ME1BIOGame, "CookedPC");
            }
        }

        public static string ME2Cooked
        {
            get
            {
                if (String.IsNullOrEmpty(ME2BIOGame))
                    return null;

                return Path.Combine(ME2BIOGame, "CookedPC");
            }
        }

        public static string ME3Cooked
        {
            get
            {
                if (String.IsNullOrEmpty(ME3BIOGame))
                    return null;

                return Path.Combine(ME3BIOGame, "CookedPCConsole");
            }
        }

        public static string ME1DLCPath
        {
            get
            {
                if (String.IsNullOrEmpty(ME1BIOGame))
                    return null;

                return Path.Combine(Path.GetDirectoryName(ME1BIOGame), "DLC");
            }
        }

        public static string ME2DLCPath
        {
            get
            {
                if (String.IsNullOrEmpty(ME2BIOGame))
                    return null;

                return Path.Combine(ME2BIOGame, "DLC");
            }
        }

        public static string ME3DLCPath
        {
            get
            {
                if (String.IsNullOrEmpty(ME3BIOGame))
                    return null;

                return Path.Combine(ME3BIOGame, "DLC");
            }
        }

        static List<string> me1Files = null;
        public static List<string> ME1Files
        {
            get
            {
                if (me1Files == null && ME1BIOGame != null && Directory.Exists(ME1BIOGame))
                    me1Files = EnumerateGameFiles(1, ME1BIOGame);

                return me1Files;
            }
        }

        static List<string> me2Files = null;
        public static List<string> ME2Files
        {
            get
            {
                if (me2Files == null && ME2BIOGame != null && Directory.Exists(ME2BIOGame))
                    me2Files = EnumerateGameFiles(2, ME2BIOGame);

                return me2Files;
            }
        }

        static List<string> me3Files = null;
        public static List<string> ME3Files
        {
            get
            {
                if (me3Files == null && ME3BIOGame != null && Directory.Exists(ME3BIOGame))
                    me3Files = EnumerateGameFiles(3, ME3BIOGame);  // Includes DLC

                return me3Files;
            }
        }
        #endregion Individuals based on BIOGames

        public static int BasePathLength { get; set; }

        public string PathCooked
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1Cooked;
                    case 2:
                        return ME2Cooked;
                    case 3:
                        return ME3Cooked;
                }

                return null;
            }
        }

        public string PathBIOGame
        {
            get
            {
                if (GameVersion == 0)
                    return null;
                return BIOGames[GameVersion - 1];
            }
        }

        public string DLCPath
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1DLCPath;
                    case 2:
                        return ME2DLCPath;
                    case 3:
                        return ME3DLCPath;
                }

                return null;
            }
        }

        public List<string> Files
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1Files;
                    case 2:
                        return ME2Files;
                    case 3:
                        return ME3Files;
                }

                return null;
            }
        }

        public string BasePath
        {
            get
            {
                if (String.IsNullOrEmpty(PathBIOGame))
                    return null;

                return Path.GetDirectoryName(PathBIOGame);
            }
        }

        public static string StorageFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ME3Explorer");
            }
        }

        public string ThumbnailCachePath
        {
            get
            {
                if (GameVersion == 0)
                    return null;

                return Path.Combine(StorageFolder, "ThumbnailCaches", "ME" + GameVersion + "ThumbnailCache.cache");
            }
        }

        public bool DoesGame1Exist
        {
            get
            {
                return Directory.Exists(ME1BIOGame);
            }
        }

        public bool DoesGame2Exist
        {
            get
            {
                return Directory.Exists(ME2BIOGame);
            }
        }

        public bool DoesGame3Exist
        {
            get
            {
                return Directory.Exists(ME3BIOGame);
            }
        }

        public MEDirectories(int game) : this()
        {
            GameVersion = game;
        }

        public MEDirectories()
        {
            CachePath = "CustTextures";
            SetupPaths();
        }

        public static string GetCommonDLCName(string dLCName)
        {
            string commonName = dLCName;
            try
            {
                commonName = CommonDLCNames[dLCName] + $" ({dLCName})";
            }
            catch { } // Name not in list

            return commonName;
        }

        public void SetupPaths(bool force = false)
        {
            if (!force && BIOGames.Any(bio => !String.IsNullOrEmpty(bio)))
                return;

            for (int i = 1; i < 4; i++)
            {
                string tempBIO = null;
                switch (i)
                {
                    case 1:
                        tempBIO = Properties.Settings.Default.ME1BIOGame;
                        break;
                    case 2:
                        tempBIO = Properties.Settings.Default.ME2BIOGame;
                        break;
                    case 3:
                        tempBIO = Properties.Settings.Default.ME3BIOGame;
                        break;
                }

                if (!string.IsNullOrEmpty(tempBIO))
                {
                    BIOGames[i - 1] = tempBIO.TrimEnd(Path.DirectorySeparatorChar);
                    DebugOutput.PrintLn($"Found ME{i} BIOGame path in ME3Explorer Settings: {tempBIO}.");
                    continue;
                }

                // KFreon: Get path from Registry
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string subkey = @"BioWare\Mass Effect" + (i == 1 ? "" : (" " + i));
                string keyName;
                string valueName = i == 3 ? "Install Dir" : "Path";

                // KFreon: x86 Check first
                keyName = hkey32 + subkey;
                string test = (string)Microsoft.Win32.Registry.GetValue(keyName, valueName, null);
                if (test != null)
                {
                    tempBIO = test;
                    BIOGames[i - 1] = Path.Combine(tempBIO.TrimEnd(Path.DirectorySeparatorChar), "BIOGame");
                    DebugOutput.PrintLn($"Found ME{i} BIOGame path in x86 Registry: {tempBIO}.");
                    continue;
                }

                // KFreon: Nope, try x64
                keyName = hkey64 + subkey;
                tempBIO = (string)Microsoft.Win32.Registry.GetValue(keyName, valueName, null);

                if (!string.IsNullOrEmpty(tempBIO))
                {
                    BIOGames[i - 1] = Path.Combine(tempBIO.TrimEnd(Path.DirectorySeparatorChar), "BIOGame");
                    DebugOutput.PrintLn($"Found ME{i} BIOGame path in x64 Regsitry: {tempBIO}.");
                }
                else
                    DebugOutput.PrintLn($"ME{i} BIOGame path not found.");
            }
        }

        /// <summary>
        /// Saves BIOGames set into Properties.
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                if (!String.IsNullOrEmpty(BIOGames[0]))
                    Properties.Settings.Default.ME1BIOGame = BIOGames[0];

                if (!String.IsNullOrEmpty(BIOGames[1]))
                    Properties.Settings.Default.ME2BIOGame = BIOGames[1];

                if (!String.IsNullOrEmpty(BIOGames[2]))
                    Properties.Settings.Default.ME3BIOGame = BIOGames[2];

                Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Error saving pathing: " + e.Message);
            }
        }

        /// <summary>
        /// Enumerates useful game files for specified game version in the specified path.
        /// </summary>
        /// <param name="GameVersion">Game version to search in.</param>
        /// <param name="searchPath">Path to search in.</param>
        /// <param name="recurse">True: searches in subfolders.</param>
        /// <param name="predicate">Filter to use to find game files. Default returns normal game files like .pccs.</param>
        /// <returns>List of filtered game files in specified directory.</returns>
        public static List<string> EnumerateGameFiles(int GameVersion, string searchPath, bool recurse = true, Predicate<string> predicate = null)
        {
            List<string> files = new List<string>();

            files = Directory.EnumerateFiles(searchPath, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
            DebugOutput.PrintLn($"Enumerated files for ME{GameVersion} in {searchPath}");
            files = EnumerateGameFiles(GameVersion, files, predicate);
            DebugOutput.PrintLn($"Filtered gamefiles for ME{GameVersion} in {searchPath}");
            return files;
        }


        /// <summary>
        /// Enumerates game files in the given list.
        /// </summary>
        /// <param name="GameVersion">Game version to search in.</param>
        /// <param name="files">Files to search.</param>
        /// <param name="predicate">Filter to use to find game files. Default returns normal game files like .pccs.</param>
        /// <returns>List of filtered game files in given files.</returns>
        public static List<string> EnumerateGameFiles(int GameVersion, List<string> files, Predicate<string> predicate = null)
        {
            if (predicate == null)
            {
                // KFreon: Set default search predicate.
                switch (GameVersion)
                {
                    case 1:
                        predicate = s => s.ToLowerInvariant().EndsWith(".upk", true, null) || s.ToLowerInvariant().EndsWith(".u", true, null) || s.ToLowerInvariant().EndsWith(".sfm", true, null);
                        break;
                    case 2:
                    case 3:
                        predicate = s => s.ToLowerInvariant().EndsWith(".pcc", true, null) || s.ToLowerInvariant().EndsWith(".tfc", true, null);
                        break;
                }
            }

            return files.Where(t => predicate(t)).ToList();
        }

        public void RefreshListeners()
        {
            OnPropertyChanged(nameof(DoesGame1Exist));
            OnPropertyChanged(nameof(DoesGame2Exist));
            OnPropertyChanged(nameof(DoesGame3Exist));
            OnPropertyChanged(nameof(PathCooked));
            OnPropertyChanged(nameof(PathBIOGame));
            OnPropertyChanged(nameof(DLCPath));
            OnPropertyChanged(nameof(Files));
        }
    }
}
