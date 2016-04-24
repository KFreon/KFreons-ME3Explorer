using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using SaltTPF;
using UsefulThings;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class TPFToolsViewModel : MEViewModelBase<TPFTexInfo>
    {
        List<ZipReader> Zippys = new List<ZipReader>();

        public TPFToolsViewModel() : base(Properties.Settings.Default.TPFToolsGameVersion)
        {
            Trees.Add(new TreeDB(MEExDirecs, 1, MEExDirecs.GameVersion == 1, null, ToolsetRevision, DontLoad: true));
            Trees.Add(new TreeDB(MEExDirecs, 2, MEExDirecs.GameVersion == 2, null, ToolsetRevision, DontLoad: true));
            Trees.Add(new TreeDB(MEExDirecs, 3, MEExDirecs.GameVersion == 3, null, ToolsetRevision, DontLoad: true));

            BeginTreeLoading();
        }

        async void BeginTreeLoading()
        {
            Busy = true;
            PrimaryStatus = "Loading Trees...";

            await Task.Run(() => base.LoadTrees(Trees, CurrentTree, false));

            PrimaryStatus = "Ready!";
            Busy = false;
        }

        internal void ClearAll()
        {
            Textures.Clear();
            PrimaryProgress = 0;
            MaxPrimaryProgress = 1;
            PrimaryIndeterminate = false;
            Zippys.Clear();
            PrimaryStatus = "Ready!";
            IsLoaded = false;
            Busy = false;
            cts = new System.Threading.CancellationTokenSource();
        }

        public async Task LoadFiles(IEnumerable<string> Files)
        {
            int numFiles = Files.Count();
            Busy = true;
            PrimaryProgress = 0;
            MaxPrimaryProgress = numFiles;


            List<TPFTexInfo> ProcessedFiles = null;

            try
            {
                ProcessedFiles = await Task<List<TPFTexInfo>>.Run(() => ProcessLoadingFiles(Files, numFiles));
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to load files: ", "TPFTools LoadFiles", e);
            }

            // KFreon: Add files to list
            Textures.AddRange(ProcessedFiles);

            PrimaryIndeterminate = true;
            PrimaryStatus = "Getting details and generating thumbnails...";

            // KFreon: Generate Thumbnails with multiple threads
            ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = NumThreads - 1;
            await Task.Run(() => Parallel.ForEach(Textures, po, t => t.EnumerateDetails()));

            OnPropertyChanged("RequiresAutoFix");

            PrimaryIndeterminate = false;
            PrimaryStatus = "Loaded!";

            IsLoaded = true;
            Busy = false;
        }

        /// <summary>
        /// Load textures from mixed format files (TPF, jpg, bmp, etc). Returns valid textures found.
        /// </summary>
        /// <param name="Files>Files to load textures from.</param>
        /// <param name="numFiles">Number of files to load textures from.</param>
        protected List<TPFTexInfo> ProcessLoadingFiles(IEnumerable<string> Files, int numFiles)
        {
            List<TPFTexInfo> entries = new List<TPFTexInfo>();
            foreach (string file in Files)
            {
                cts.Token.ThrowIfCancellationRequested();

                PrimaryStatus = String.Format("Loading {0} textures from {1}.", numFiles, Path.GetFileName(file));

                string ext = Path.GetExtension(file).ToLowerInvariant();
                switch (ext)
                {
                    case ".tpf":
                    case ".metpf":
                        entries.AddRange(LoadTPF(file));
                        break;
                    case ".dds":
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".bmp":
                    case ".log":
                    case ".txt":
                    case ".def":
                        entries.Add(LoadExternal(file));
                        break;
                    case ".mod":
                        entries.AddRange(LoadMOD(file));
                        break;
                    default:
                        DebugOutput.PrintLn("File: " + file + " is unsupported.");
                        break;
                }

                PrimaryProgress++;
            }

            return entries;
        }

        /// <summary>
        /// Loads textures from a .MOD file. 
        /// Returns valid textures.
        /// </summary>
        /// <param name="file">Path to .MOD file.</param>
        private List<TPFTexInfo> LoadMOD(string file)
        {
            PrimaryProgress = 0;
            List<TPFTexInfo> entries = new List<TPFTexInfo>();
            /*List<ModJob> tempJobs = new List<ModJob>();
            KFreonLibME.Scripting.ModMakerHelper.LoadDotMod(file, null, null, null, null, MEExDirecs.ExecFolder, MEExDirecs.BIOGames, tempJobs);

            MaxPrimaryProgress = tempJobs.Count;

            int count = 1;
            foreach (ModJob job in tempJobs)
            {
                cts.Token.ThrowIfCancellationRequested();

                PrimaryStatus = String.Format("Processing Job: {0}  {1}/{2} from {3}", job.ObjectName, count++, tempJobs.Count, Path.GetFileName(file));
                if (job.JobType == Scripting.ModJobType.Texture)
                {
                    Debug.WriteLine("");
                }
                else
                    DebugOutput.PrintLn(String.Format("Job: {0} isn't a texture. Ignoring...", job.Name));

            }*/

            return entries;
        }

        /// <summary>
        /// Loads non TPF based image, i.e. any normal image such as dds, jpg, bmp, etc.
        /// Returns texture entry or null if failed.
        /// </summary>
        /// <param name="file">Path to texture to load.</param>
        /// <param name="isDef">True = file is a .log file, false = texture image.</param>
        private TPFTexInfo LoadExternal(string file)
        {
            PrimaryStatus = "Loading external file: " + file;

            TPFTexInfo tex = LoadTex(file);

            if (!tex.IsDef && tex.Hash == 0)
                DebugOutput.PrintLn("Failed to get hash from {0}", file);

            return tex;
        }

        /// <summary>
        /// Loads textures from TPF. Returns textures found.
        /// </summary>
        /// <param name="TPF">Path to TPF file to load.</param>
        private List<TPFTexInfo> LoadTPF(string TPF)
        {
            // KFreon: Open TPF and set some properties
            SaltTPF.ZipReader zippy = new SaltTPF.ZipReader(TPF);
            zippy.Description = "Filename:  \n" + zippy._filename + "\n\nComment:  \n" + zippy.EOFStrct.Comment + "\nNumber of stored files:  " + zippy.Entries.Count;
            zippy.Scanned = false;
            Zippys.Add(zippy);

            int numEntries = zippy.Entries.Count;
            List<TPFTexInfo> entries = new List<TPFTexInfo>(numEntries);

            // KFreon: Load texture details from internal .log
            List<string> Lines = new List<string>(50);
            try
            {
                byte[] data = zippy.Entries[numEntries - 1].Extract(true);
                StringBuilder temp = new StringBuilder(100);
                for (int i = 0; i < data.Length; i++)
                {
                    cts.Token.ThrowIfCancellationRequested();

                    // KFreon: Ignore some chars and split on newlines
                    char c = (char)data[i];
                    if (c == '\n')
                    {
                        Lines.Add(temp.ToString());
                        temp.Clear();
                        continue;
                    }

                    if (c != '\0' && c != '\r')
                        temp.Append(c);
                }
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to read TPF details: ", "TPFTools LoadTPF", e);
                return entries;
            }

            // KFreon: Remove duplicates
            Lines = Lines.Distinct().ToList(Lines.Count);
            zippy.DefLines = Lines;

            for (int i = 0; i < numEntries; i++)
            {
                // KFreon: Add TPF entries to TotalTexes list
                TPFTexInfo tmpTex = LoadTex(zippy.Entries[i].Filename, null, i, zippy, Lines);


                // KFreon: If hash gen failed, notify
                if (!tmpTex.IsDef && tmpTex.Hash == 0)
                    DebugOutput.PrintLn("Failure to get hash for entry " + i + " in " + TPF);

                entries.Add(tmpTex);
            }

            return entries;
        }

        private TPFTexInfo LoadTex(string file)
        {
            return LoadTex(file, file.GetDirParent(), -1, null, null);
        }

        private TPFTexInfo LoadTex(string file, string path, int tpfind, ZipReader zippy, List<string> Lines)
        {
            TPFTexInfo tempTex = new TPFTexInfo(file, path, tpfind, zippy, GameVersion)
            {
                PathBIOGame = MEExDirecs.PathBIOGame,
                /*ExtractConvertDelegate = this.ExtractConvertDelegate,
                ReplaceDelegate = this.ReplaceDelegate,
                InstallDelegate = this.InstallDelegate,
                AutoFixDelegate = async t =>
                {
                    await this.AutoFix(t);
                    PrimaryStatus = t.ValidTexture ? "Successfully fixed!" : "Failed to fix :(";
                }*/
            };

            if (tempTex.IsDef && zippy != null)
                tempTex.LogContents.AddRange(Lines);
            else
            {
                tempTex.Hash = GetHash(file, zippy == null, Lines);
                tempTex.OriginalHash = tempTex.Hash;

                if (!tempTex.IsDef && tempTex.Hash == 0)
                    DebugOutput.PrintLn("Failure to get hash for entry {0} in {1}.", file, path ?? zippy._filename);
            }

            return tempTex;
        }

        private uint GetHash(string filename, bool external, List<string> Lines)
        {
            uint hash = 0;
            if (external)
            {
                int hashInd = filename.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
                if (hashInd > 0)
                    hash = WPF_ME3Explorer.General.FormatTexmodHashAsUint(filename.Substring(hashInd, 10));
                else
                {
                    foreach (var tex in Textures)
                    {
                        if (tex.IsDef)
                        {
                            hash = FindHashInDef(tex.LogContents, filename);
                            if (hash != 0)
                                break;
                        }
                    }
                }
            }
            else
                hash = FindHashInDef(Lines, filename);

            return hash;
        }

        private uint FindHashInDef(List<string> Lines, string filename)
        {
            uint hash = 0;
            foreach (var line in Lines)
            {
                hash = GetHashFromLine(line, filename);
                if (hash != 0)
                    break;
            }


            return hash;
        }

        /// <summary>
        /// Gets Texmod hash out of TPF internal .log line entry. Returns 0 if none found.
        /// </summary>
        /// <param name="line">Line in TPF .log</param>
        /// <param name="filename">filename as seen in .log line</param>
        private uint GetHashFromLine(string line, string filename)
        {
            int index = -1;
            index = line.IndexOf(filename, StringComparison.OrdinalIgnoreCase);
            if ((index) > 0)
                return WPF_ME3Explorer.General.FormatTexmodHashAsUint(line);

            return 0;
        }
    }
}
