using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UsefulThings;
using Microsoft.Win32;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer.MEDirectories
{
    /// <summary>
    /// Provides Object Oriented front end access to all three ME Directory classes under one roof.
    /// </summary>
    public class MEDirectories : ViewModelBase
    {
        #region Static Methods
        /// <summary>
        /// Returns name of DLC if path is part of a DLC.
        /// </summary>
        /// <param name="path">Path to search for DLC names in.</param>
        /// <returns>Name of DLC or empty string if not found.</returns>
        public static string GetDLCNameFromPath(string path)
        {
            if (path.Contains("DLC_"))
            {
                List<string> parts = path.Split('\\').ToList();
                string retval = new List<string>(parts.Where(part => part.Contains("DLC_")))[0];
                if (retval.Contains("metadata"))
                    return null;
                else
                    return retval;
            }
            else
                return null;
        }


        /// <summary>
        /// Gets list of DLC from specified path
        /// </summary>
        /// <param name="DLCBasePath">Path to search.</param>
        /// <returns>List of DLC's found at DLCBasePath.</returns>
        public static List<string> GetInstalledDLC(string DLCBasePath)
        {
            if (!Directory.Exists(DLCBasePath))
                return null;

            return Directory.EnumerateDirectories(DLCBasePath).Where(d => !d.Contains("metadata")).ToList(16);
        }

        /// <summary>
        /// Gets BIOGame path from Exe path.
        /// </summary>
        /// <param name="exePath">Path to Exe.</param>
        /// <param name="game">Game to set. Valid: 1-3</param>
        /// <returns>BIOGame path.</returns>
        public static string GetBIOGameFromExe(string exePath, int game)
        {
            string subpath = exePath.GetDirParent();
            string retval = null;
            switch (game)
            {
                case 1:
                case 2:
                    retval = Path.Combine(subpath, "BIOGame\\");
                    break;
                case 3:
                    retval = Path.Combine(subpath.GetDirParent(), "BIOGame\\");
                    break;
            }
            return retval;
        }


        /// <summary>
        /// Gets Cooked path from Exe path.
        /// </summary>
        /// <param name="exePath">Path to Exe.</param>
        /// <param name="game">Game to set. Valid: 1-3</param>
        /// <returns>Cooked path.</returns>
        public static string GetCookedFromBIOGame(string BIOGame, int game)
        {
            string retval = null;
            switch (game)
            {
                case 1:
                case 2:
                    retval = Path.Combine(BIOGame, "CookedPC\\");
                    break;
                case 3:
                    retval = Path.Combine(BIOGame, "CookedPCConsole\\");
                    break;
            }
            return retval;
        }


        /// <summary>
        /// Gets DLC path from Exe path.
        /// </summary>
        /// <param name="exePath">Path to Exe.</param>
        /// <param name="game">Game to set. Valid: 1-3</param>
        /// <returns>DLC path.</returns>
        public static string GetDLCFromBIOGame(string BIOGame, int game)
        {
            string retval = null;
            switch (game)
            {
                case 1:
                    retval = Path.Combine(BIOGame.GetDirParent(), "DLC\\");
                    break;
                case 2:
                case 3:
                    retval = Path.Combine(BIOGame, "DLC\\");
                    break;
            }
            return retval;
        }

        /// <summary>
        /// Gets a list of game files from specified folder.
        /// </summary>
        /// <param name="GameVersion">Version of game being searched.</param>
        /// <param name="searchPath">Path to search.</param>
        /// <param name="predicate">Filtering predicate.</param>
        /// <param name="recurse">True = recurse into subfolders.</param>
        /// <returns>List of gamefiles in specified folders, optionally matching predicate.</returns>
        public static List<string> EnumerateGameFiles(int GameVersion, string searchPath, bool recurse = true, Predicate<string> predicate = null)
        {
            if (String.IsNullOrEmpty(searchPath) || GameVersion < 1 || GameVersion > 3)
                return new List<string>();

            List<string> files = new List<string>();

            files = Directory.EnumerateFiles(searchPath, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList(5000);
            files = EnumerateGameFiles(GameVersion, files, predicate);
            return files;
        }


        /// <summary>
        /// Gets a list of game files from given list of files.
        /// </summary>
        /// <param name="GameVersion">Target game version.</param>
        /// <param name="files">List of files to search.</param>
        /// <param name="predicate">Filtering predicate.</param>
        /// <returns>List of Gamefiles found in files.</returns>
        public static List<string> EnumerateGameFiles(int GameVersion, List<string> files, Predicate<string> predicate = null)
        {
            if (GameVersion < 1 || GameVersion > 3 || files == null)
                return new List<string>();

            if (predicate == null)
            {
                // KFreon: Set default search predicate.
                switch (GameVersion)
                {
                    case 1:
                        predicate = s => s.EndsWith(".upk", StringComparison.CurrentCultureIgnoreCase) || s.EndsWith(".u", StringComparison.CurrentCultureIgnoreCase) || s.EndsWith(".sfm", StringComparison.CurrentCultureIgnoreCase);
                        break;
                    case 2:
                    case 3:
                        predicate = s => s.EndsWith(".pcc", StringComparison.CurrentCultureIgnoreCase) || s.EndsWith(".tfc", StringComparison.CurrentCultureIgnoreCase);
                        break;
                }
            }

            return files.Where(t => predicate(t)).ToList(5000);
        }


        /// <summary>
        /// Sets the BIOGame path in the MEDirectory classes.
        /// </summary>
        /// <param name="GameVers">Which game to set path of.</param>
        /// <param name="path">New BIOGame path.</param>
        public static void SetGamePath(int GameVers, string path)
        {
            switch (GameVers)
            {
                case 1:
                    ME1Directory.GamePath = path;
                    break;
                case 2:
                    ME2Directory.GamePath = path;
                    break;
                case 3:
                    ME3Directory.GamePath = path;
                    break;
            }
        }


        /// <summary>
        /// Gets user input to select a game exe.
        /// </summary>
        /// <param name="GameVers">Game to select.</param>
        /// <returns>Path to game exe.</returns>
        public static string SelectGameLoc(int GameVers)
        {
            string retval = null;
            string gameExe = "MassEffect" + GameVers + ".exe";
            OpenFileDialog selectDir = new OpenFileDialog();
            {
                selectDir.FileName = gameExe;
                selectDir.Filter = "ME" + GameVers + " exe file|" + gameExe;
                selectDir.Title = "Select the Mass Effect " + GameVers + " executable file";
                if (selectDir.ShowDialog() == true)
                    retval = GetBIOGameFromExe(selectDir.FileName, GameVers);
            }

            return retval;
        }
        #endregion Static Methods


        #region Properties
        #region Game states
        // KFreon: True = path found in properties, null = path found in registry, false = path not found
        // NOTE: This does not mean the path actually exists, only that it is/isn't stored in settings.

        public enum GameState
        {
            FoundInProperties, FoundInRegistry, NotFound
        }

        GameState game1PathState = GameState.NotFound;
        public GameState Game1PathState
        {
            get
            {
                return game1PathState;
            }
            set
            {
                game1PathState = value;
                OnPropertyChanged();
            }
        }

        GameState game2PathState = GameState.NotFound;
        public GameState Game2PathState
        {
            get
            {
                return game2PathState;
            }
            set
            {
                game2PathState = value;
                OnPropertyChanged();
            }
        }

        GameState game3PathState = GameState.NotFound;
        public GameState Game3PathState
        {
            get
            {
                return game3PathState;
            }
            set
            {
                game3PathState = value;
                OnPropertyChanged();
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
                doesGame1Exist = value;
                OnPropertyChanged();
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
                doesGame2Exist = value;
                OnPropertyChanged();
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
                doesGame3Exist = value;
                OnPropertyChanged();
            }
        }
        #endregion


        #region Pathing
        public string BasePath
        {
            get
            {
                return PathBIOGame.GetDirParent();
            }
        }

        public string pathCooked
        {
            get
            {
                if (GameVersion < 1 || GameVersion > 3)
                    return null;
                return GetDifferentPathCooked(GameVersion);
            }
            set
            {
                if (GameVersion >= 1 && GameVersion <= 3)
                {
                    Cookeds[GameVersion - 1] = value;
                    SaveInstanceSettings();
                }
            }
        }

        public string PathBIOGame
        {
            get
            {
                if (GameVersion < 1 || GameVersion > 3)
                    return null;
                return GetDifferentPathBIOGame(GameVersion);
            }
            set
            {
                if (GameVersion >= 1 && GameVersion <= 3)
                {
                    BIOGames[GameVersion - 1] = value;
                    SaveInstanceSettings();
                }
            }
        }

        public string DLCPath
        {
            get
            {
                if (GameVersion < 1 || GameVersion > 3)
                    return null;

                return GetDifferentDLCPath(GameVersion);
            }
            set
            {
                if (GameVersion >= 1 && GameVersion <= 3)
                {
                    DLCPaths[GameVersion - 1] = value;
                    SaveInstanceSettings();
                }
            }
        }

        public string ExePath
        {
            get
            {
                if (GameVersion < 1 || GameVersion > 3)
                    return null;

                return GetDifferentExePath(GameVersion);
            }
            set
            {
                if (GameVersion >= 1 && GameVersion <= 3)
                {
                    ExePaths[GameVersion - 1] = value;
                    SaveInstanceSettings();
                }
            }
        }

        string execf = null;
        public string ExecFolder
        {
            get
            {
                if (execf == null)
                    execf = Path.Combine(UsefulThings.General.GetExecutingLoc(), "Exec\\");

                return execf;
            }
        }

        public List<string> DLCFiles
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1Directory.DLCFiles;
                    case 2:
                        return ME2Directory.DLCFiles;
                    case 3:
                        return ME3Directory.DLCFiles;
                    default:
                        return null;
                }
            }
        }

        public List<string> BasegameFiles
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1Directory.BaseGameFiles;
                    case 2:
                        return ME2Directory.BaseGameFiles;
                    case 3:
                        return ME3Directory.BaseGameFiles;
                    default:
                        return null;
                }
            }
        }
        #endregion


        #region Misc
        public int GameVersion { get; set; }
        #endregion Misc
        #endregion


        // KFreon: Lists of each game's relevant pathing information
        public List<string> BIOGames = new List<string>() { "", "", "" };
        public List<string> Cookeds = new List<string>() { "", "", "" };
        public List<string> DLCPaths = new List<string>() { "", "", "" };
        public List<string> ExePaths = new List<string>() { "", "", "" };


        #region Constructors
        /// <summary>
        /// Creates instance that serves as a front end to all 3 ME Directory classes.
        /// </summary>
        /// <param name="game">Game to set as default on this instance. Valid: 1-3.</param>
        public MEDirectories(int game, string execPath = null) : this(execPath)
        {
            GameVersion = game;
        }


        /// <summary>
        /// Creates instance based on another.
        /// </summary>
        /// <param name="orig">Original instance.</param>
        /// <param name="game">Game version to use for new instance.</param>
        public MEDirectories(MEDirectories orig, int game)
        {
            // KFreon: COPY all properties
            BIOGames = new List<string>(orig.BIOGames);
            Cookeds = new List<string>(orig.Cookeds);
            DLCPaths = new List<string>(orig.DLCPaths);
            ExePaths = new List<string>(orig.ExePaths);

            Game1PathState = orig.Game1PathState;
            Game2PathState = orig.Game2PathState;
            Game3PathState = orig.Game3PathState;

            DoesGame1Exist = orig.DoesGame1Exist;
            DoesGame2Exist = orig.DoesGame2Exist;
            DoesGame3Exist = orig.DoesGame3Exist;

            execf = orig.ExecFolder;

            GameVersion = game;
        }

        /// <summary>
        /// Construct new instance based on another.
        /// </summary>
        /// <param name="orig">Base instance.</param>
        public MEDirectories(MEDirectories orig)
            : this(orig, orig.GameVersion)
        {

        }


        /// <summary>
        /// Creates instance that serves as a front end to all 3 ME Directory classes. 
        /// </summary>
        /// <param name="ask">true = Gets user input if pathing not found.</param>
        public MEDirectories(string execPath, bool ask = false)
        {
            if (execPath != null)
                execf = execPath;
            WPF_ME3Explorer.General.UpgradeProperties();
            SetupPathing();
        }
        #endregion


        #region Misc
        /// <summary>
        /// Gets List of basegame files for specified game.
        /// </summary>
        /// <param name="game">Game to use. Valid 1-3.</param>
        public List<string> GetBaseGameFiles(int game = -1)
        {
            List<string> basefiles = null;
            switch (game == -1 ? GameVersion : game)
            {
                case 1:
                    basefiles = ME1Directory.BaseGameFiles;
                    break;
                case 2:
                    basefiles = ME2Directory.BaseGameFiles;
                    break;
                case 3:
                    basefiles = ME3Directory.BaseGameFiles;
                    break;
            }
            return basefiles;
        }


        /// <summary>
        /// Gets List of DLC files for specified game.
        /// </summary>
        /// <param name="game">Game to use. Valid 1-3.</param>
        public List<string> GetDLCFiles(int game = -1)
        {
            List<string> dlcfiles = null;
            switch (game == -1 ? GameVersion : game)
            {
                case 1:
                    dlcfiles = ME1Directory.DLCFiles;
                    break;
                case 2:
                    dlcfiles = ME2Directory.DLCFiles;
                    break;
                case 3:
                    dlcfiles = ME3Directory.DLCFiles;
                    break;
            }
            return dlcfiles;
        }

        /// <summary>
        /// Checks whether BIOGame path for given game actually exists on disk.
        /// </summary>
        /// <param name="gameInd">Game to check. Valid: 1-3.</param>
        /// <returns>True if game path exists, false otherwise.</returns>
        public bool DoesGameExist(int gameInd)
        {
            return String.IsNullOrEmpty(BIOGames[gameInd - 1]) ? false : Directory.Exists(BIOGames[gameInd - 1]);
        }

        /// <summary>
        /// Sets specified game state. True = from settings, null = from registry, false = not found.
        /// </summary>
        private void SetGamePathState(int game, GameState state)
        {
            switch (game)
            {
                case 1:
                    Game1PathState = state;
                    break;
                case 2:
                    Game2PathState = state;
                    break;
                case 3:
                    Game3PathState = state;
                    break;
            }
        }

        /// <summary>
        /// Sets up pathing for all games.
        /// </summary>
        /// <param name="AskIfNotFound">True = asks for location if none found.</param>
        public void SetupPathing()
        {
            DebugOutput.PrintLn("-----Pathing-----");
            for (int i = 1; i <= 3; i++)
            {
                // KFreon: Try to get pathing from MEDirectories
                GameState status = GetPaths(i);
                if (status == GameState.FoundInProperties)
                    DebugOutput.PrintLn("Found game path in settings. Using cooked directory: " + GetDifferentPathCooked(i));
                else if (status == GameState.FoundInRegistry)
                    DebugOutput.PrintLn("Found installation path from registry. Using cooked directory: " + GetDifferentPathCooked(i));

                SetGamePathState(i, status);
            }
            DebugOutput.PrintLn("-----------------");
            DebugOutput.PrintLn();
            SaveInstanceSettings();

            // KFreon: Populate Existance properties
            DoesGame1Exist = DoesGameExist(1);
            DoesGame2Exist = DoesGameExist(2);
            DoesGame3Exist = DoesGameExist(3);
        }


        /// <summary>
        /// Saves currently set properties to Project Settings
        /// </summary>
        public void SaveInstanceSettings()
        {
            try
            {
                SetBIOGame(1, BIOGames[0], false);
                SetBIOGame(2, BIOGames[1], false);
                SetBIOGame(3, BIOGames[2], false);

                SetDLCPath(1, DLCPaths[0], false);
                SetDLCPath(2, DLCPaths[1], false);
                SetDLCPath(3, DLCPaths[2], false);

                SetCooked(1, Cookeds[0], false);
                SetCooked(2, Cookeds[1], false);
                SetCooked(3, Cookeds[2], false);

                SetExePath(1, ExePaths[0], false);
                SetExePath(2, ExePaths[1], false);
                SetExePath(3, ExePaths[2], false);

                WPF_ME3Explorer.General.SaveSettings();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Changing directory settings or saving settings failed:  ", "MEDirectories", e);
            }
        }
        #endregion


        #region Getting different paths than set
        // KFreon: As MEExDirectories is an instance class (i.e. not static), it can have a stored game index for easy access to main variables.
        // However, it is sometimes required to get paths other than the one being targetted. Still, non static as each tool may want to set different paths (don't know why...)

        /// <summary>
        /// Gets Cooked path other than the current instance setting.
        /// </summary>
        /// <param name="game">Game to fetch. Valid: 1-3</param>
        /// <returns>Cooked path for game.</returns>
        public string GetDifferentPathCooked(int game)
        {
            return Cookeds[game - 1];   // KFreon: There will always be 3 elements in these arrays - it's initialised that way.
        }


        /// <summary>
        /// Gets BIOGame path other than the current instance setting.
        /// </summary>
        /// <param name="game">Game to fetch. Valid: 1-3</param>
        /// <returns>BIOGame path for game.</returns>
        public string GetDifferentPathBIOGame(int game)
        {
            return BIOGames[game - 1];
        }


        /// <summary>
        /// Gets DLC path other than the current instance setting.
        /// </summary>
        /// <param name="game">Game to fetch. Valid: 1-3</param>
        /// <returns>DLC path for game.</returns>
        public string GetDifferentDLCPath(int game)
        {
            return DLCPaths[game - 1];
        }


        /// <summary>
        /// Gets Exe path other than the current instance setting.
        /// </summary>
        /// <param name="game">Game to fetch. Valid: 1-3</param>
        /// <returns>Exe path for game.</returns>
        public string GetDifferentExePath(int game)
        {
            return ExePaths[game - 1];
        }
        #endregion


        #region Setting Paths
        /// <summary>
        /// Sets BIOGame path property in Project Properties and optionally saves.
        /// </summary>
        /// <param name="game">Game to set BIOGame path for.</param>
        /// <param name="path">Path of BIOGame.</param>
        /// <param name="Autosave">Saves WPF_ME3Explorer.Properties.</param>
        public static void SetBIOGame(int game, string path, bool Autosave = true)
        {
            if (String.IsNullOrEmpty(path))
                return;

            try
            {
                switch (game)
                {
                    case 1:
                        WPF_ME3Explorer.Properties.Settings.Default.ME1BIOGame = path;
                        break;
                    case 2:
                        WPF_ME3Explorer.Properties.Settings.Default.ME2BIOGame = path;
                        break;
                    case 3:
                        WPF_ME3Explorer.Properties.Settings.Default.ME3BIOGame = path;
                        break;
                }


                // KFreon: Safely save Settings if asked.
                if (Autosave)
                    WPF_ME3Explorer.General.SaveSettings();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to set BIOGame property: ", "MEDirectories", e);
            }
        }


        /// <summary>
        /// Sets Cooked path property in Project Properties and optionally saves.
        /// </summary>
        /// <param name="game">Game to set Cooked path for.</param>
        /// <param name="path">Path of Cooked.</param>
        /// <param name="Autosave">Saves WPF_ME3Explorer.Properties.</param>
        public static void SetCooked(int game, string path, bool Autosave = true)
        {
            if (String.IsNullOrEmpty(path))
                return;

            try
            {
                switch (game)
                {
                    case 1:
                        WPF_ME3Explorer.Properties.Settings.Default.ME1CookedPath = path;
                        break;
                    case 2:
                        WPF_ME3Explorer.Properties.Settings.Default.ME2CookedPath = path;
                        break;
                    case 3:
                        WPF_ME3Explorer.Properties.Settings.Default.ME3CookedPath = path;
                        break;
                }

                // KFreon: Safely save Settings if asked.
                if (Autosave)
                    WPF_ME3Explorer.Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to set Cooked property: ", "MEDirectories", e);
            }
        }


        /// <summary>
        /// Sets DLC path property in Project Properties and optionally saves.
        /// </summary>
        /// <param name="game">Game to set DLC path for.</param>
        /// <param name="path">Path of DLC.</param>
        /// <param name="Autosave">Saves WPF_ME3Explorer.Properties.</param>
        public static void SetDLCPath(int game, string path, bool Autosave = true)
        {
            if (String.IsNullOrEmpty(path))
                return;

            try
            {
                switch (game)
                {
                    case 1:
                        WPF_ME3Explorer.Properties.Settings.Default.ME1DLCPath = path;
                        break;
                    case 2:
                        WPF_ME3Explorer.Properties.Settings.Default.ME2DLCPath = path;
                        break;
                    case 3:
                        WPF_ME3Explorer.Properties.Settings.Default.ME3DLCPath = path;
                        break;
                }

                // KFreon: Safely save Settings if asked.
                if (Autosave)
                    WPF_ME3Explorer.Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to set DLCPath property: ", "MEDirectories", e);
            }
        }


        /// <summary>
        /// Sets Exe path property in Project Properties and optionally saves.
        /// </summary>
        /// <param name="game">Game to set Exe path for.</param>
        /// <param name="path">Path of Exe.</param>
        /// <param name="Autosave">Saves WPF_ME3Explorer.Properties.</param>
        public static void SetExePath(int game, string path, bool Autosave = true)
        {
            if (String.IsNullOrEmpty(path))
                return;

            try
            {
                switch (game)
                {
                    case 1:
                        WPF_ME3Explorer.Properties.Settings.Default.ME1ExePath = path;
                        break;
                    case 2:
                        WPF_ME3Explorer.Properties.Settings.Default.ME2ExePath = path;
                        break;
                    case 3:
                        WPF_ME3Explorer.Properties.Settings.Default.ME3ExePath = path;
                        break;
                }

                // KFreon: Safely save Settings if asked.
                if (Autosave)
                    WPF_ME3Explorer.Properties.Settings.Default.Save();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to set ExePath property: ", "MEDirectories", e);
            }
        }
        #endregion


        #region Getting Paths
        /// <summary>
        /// Gets paths for specified game from properties/registry and loads into current instance.
        /// </summary>
        /// <param name="whichgame">Game to get paths for.</param>
        public GameState GetPaths(int whichgame)
        {
            string tempgamepath = "";
            string tempdlcpath = "";
            string tempexepath = "";
            string tempcookedpath = "";

            string PropertiesBIOGamePath = "";
            string PropertiesDLCPath = "";
            string PropertiesExePath = "";
            string PropertiesCookedPath = "";

            GameState gamePathState = GameState.NotFound;

            // KFreon: Get paths from various sources
            switch (whichgame)
            {
                case 1:
                    tempgamepath = ME1Directory.GamePath;
                    tempdlcpath = ME1Directory.DLCPath;
                    tempexepath = ME1Directory.ExePath;
                    tempcookedpath = ME1Directory.cookedPath;
                    break;
                case 2:
                    tempgamepath = ME2Directory.GamePath;
                    tempdlcpath = ME2Directory.DLCPath;
                    tempexepath = ME2Directory.ExePath;
                    tempcookedpath = ME2Directory.cookedPath;
                    break;
                case 3:
                    tempgamepath = ME3Directory.GamePath;
                    tempdlcpath = ME3Directory.DLCPath;
                    tempexepath = ME3Directory.ExePath;
                    tempcookedpath = ME3Directory.cookedPath;
                    break;
            }

            PropertiesBIOGamePath = GetBIOGameProperty(whichgame);
            PropertiesDLCPath = GetDLCPathProperty(whichgame);
            PropertiesExePath = GetExePathProperty(whichgame);
            PropertiesCookedPath = GetCookedProperty(whichgame);


            // KFreon: Set paths initially
            Cookeds[whichgame - 1] = tempcookedpath;
            ExePaths[whichgame - 1] = tempexepath;
            DLCPaths[whichgame - 1] = tempdlcpath;


            if (!String.IsNullOrEmpty(PropertiesBIOGamePath))
            {
                BIOGames[whichgame - 1] = PropertiesBIOGamePath;

                // KFreon: Since its a new property, if it doesn't exist, neither will the others
                if (!String.IsNullOrEmpty(PropertiesCookedPath))
                {
                    Cookeds[whichgame - 1] = PropertiesCookedPath;
                    ExePaths[whichgame - 1] = PropertiesExePath;
                    DLCPaths[whichgame - 1] = PropertiesDLCPath;
                }

                gamePathState = GameState.FoundInProperties;
            }
            else if (tempgamepath != null)
            {
                gamePathState = GameState.FoundInRegistry;
                BIOGames[whichgame - 1] = Path.Combine(tempgamepath, "BIOGame");
            }
            else
                DebugOutput.PrintLn("ME" + whichgame + " game files not found.");

            return gamePathState;
        }


        /// <summary>
        /// Gets BIOGamePath from previously saved WPF_ME3Explorer.Properties.
        /// </summary>
        /// <param name="game">Game to get property for</param>
        public static string GetBIOGameProperty(int game)
        {
            string retval = null;
            switch (game)
            {
                case 1:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME1BIOGame;
                    break;
                case 2:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME2BIOGame;
                    break;
                case 3:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME3BIOGame;
                    break;
            }
            return retval;
        }

        /// <summary>
        /// Gets DLCPath from previously saved WPF_ME3Explorer.Properties.
        /// </summary>
        /// <param name="game">Game to get property for</param>
        public static string GetDLCPathProperty(int game)
        {
            string retval = null;
            switch (game)
            {
                case 1:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME1DLCPath;
                    break;
                case 2:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME2DLCPath;
                    break;
                case 3:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME3DLCPath;
                    break;
            }
            return retval;
        }


        /// <summary>
        /// Gets ExePath from previously saved WPF_ME3Explorer.Properties.
        /// </summary>
        /// <param name="game">Game to get property for</param>
        public static string GetExePathProperty(int game)
        {
            string retval = null;
            switch (game)
            {
                case 1:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME1ExePath;
                    break;
                case 2:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME2ExePath;
                    break;
                case 3:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME3ExePath;
                    break;
            }
            return retval;
        }


        /// <summary>
        /// Gets CookedPath from previously saved WPF_ME3Explorer.Properties.
        /// </summary>
        /// <param name="game">Game to get property for</param>
        public static string GetCookedProperty(int game)
        {
            string retval = null;
            switch (game)
            {
                case 1:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME1CookedPath;
                    break;
                case 2:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME2CookedPath;
                    break;
                case 3:
                    retval = WPF_ME3Explorer.Properties.Settings.Default.ME3CookedPath;
                    break;
            }
            return retval;
        }
        #endregion

        internal static string GetDefaultBIOGame(int tempGameVersion)
        {
            string tempSearchPath = null;
            switch (tempGameVersion)
            {
                case 1:
                    tempSearchPath = ME1Directory.GamePath;
                    break;
                case 2:
                    tempSearchPath = ME2Directory.GamePath;
                    break;
                case 3:
                    tempSearchPath = ME3Directory.GamePath;
                    break;
            }

            return tempSearchPath;
        }
    }
}
