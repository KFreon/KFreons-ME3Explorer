using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using UsefulThings.WPF;
using UsefulThings;
using System.Windows.Threading;
using System.Windows.Input;
using System.IO.Compression;
using System.Diagnostics;
using WPF_ME3Explorer.Textures;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.PCCObjects;

namespace WPF_ME3Explorer
{
    public class TreeDB : ViewModelBase
    {
        public class TreePCC
        {
            public string Name { get; set; }

            public TreePCC()
            {

            }

            public TreePCC(string name, DateTime scannedtime)
            {
                Name = name;
            }

            public bool Exists { get; set; }
        }

        ThumbnailManager thumbs = null;

        FileSystemWatcher treeWatcher = null;
        MEDirectories.MEDirectories MEExDirecs;

        #region Properties
        int numtexes = 0;
        public int NumTreeTexes
        {
            get
            {
                return numtexes;
            }
            set
            {
                SetProperty(ref numtexes, value);
            }
        }


        public int TexCount
        {
            get
            {
                return Textures.Count;
            }
        }

        bool isSelected = false;
        public bool IsSelected
        {
            get
            {
                return isSelected;
            }
            set
            {
                SetProperty(ref isSelected, value);
            }
        }
        public string TreeLocation
        {
            get
            {
                return Path.Combine(MEExDirecs.ExecFolder, "me" + GameVersion + "tree.bin");
            }
        }
        public List<TreePCC> PCCs { get; set; }

        bool exists = false;
        public bool Exists
        {
            get
            {
                return exists;
            }
            set
            {
                exists = value;
                OnPropertyChanged();
            }
        }

        bool valid = false;
        public bool Valid
        {
            get
            {
                return valid;
            }
            set
            {
                valid = value;
                OnPropertyChanged();
            }
        }




        readonly object TreeLocker = new object();
        readonly object RunningLocker = new object();

        public List<TreeTexInfo> Textures { get; set; }

        public RangedObservableCollection<TreeTexFolders> TreeTexes { get; set; }

        public bool? AdvancedFeatures { get; set; }
        public int GameVersion { get; set; }
        #endregion Properties


        int treeVersion = 0;
        public int TreeVersion
        {
            get
            {
                return treeVersion;
            }
            set
            {
                SetProperty(ref treeVersion, value);
            }
        }


        ICommand selectCommand = null;
        public ICommand SelectCommand
        {
            get
            {
                return selectCommand;
            }
            set
            {
                SetProperty(ref selectCommand, value);
            }
        }

        public TreeDB(List<string> pccs, MEDirectories.MEDirectories direcs, int game, bool Selected, ICommand selectcommand, int treeVersion, ThumbnailManager thumbsmanager = null, bool DontLoad = false)
            : this(direcs, game, Selected, selectcommand, treeVersion, thumbsmanager, DontLoad)
        {
            List<TreePCC> temppccs = new List<TreePCC>();
            DateTime now = DateTime.Now;
            pccs.ForEach(pcc => temppccs.Add(new TreePCC(pcc, now)));
            PCCs = temppccs;
        }

        public TreeDB(string filename, int treeVersion, ThumbnailManager thumbsmanager = null)
            : this(treeVersion, thumbsmanager)
        {
            GameVersion = int.Parse(Path.GetFileName(filename)[2] + "");
            MEExDirecs = new MEDirectories.MEDirectories(GameVersion);
            LoadTree(filename);
        }

        private TreeDB(int treeVersion, ThumbnailManager thumbsmanager = null)
        {
            thumbs = thumbsmanager;
            PCCs = new List<TreePCC>();
            Textures = new List<TreeTexInfo>();
            TreeTexes = new RangedObservableCollection<TreeTexFolders>();
            TreeVersion = treeVersion;
        }

        public TreeDB(MEDirectories.MEDirectories direcs, int game, bool Selected, ICommand selectcommand, int treeVersion, ThumbnailManager thumbsmanager = null, bool DontLoad = false) : this(treeVersion, thumbsmanager)
        {
            MEExDirecs = new MEDirectories.MEDirectories(direcs, game);
            GameVersion = game;

            SelectCommand = selectcommand;

            BindingOperations.EnableCollectionSynchronization(TreeTexes, TreeLocker);

            Directory.CreateDirectory(MEExDirecs.ExecFolder);

            treeWatcher = new FileSystemWatcher(MEExDirecs.ExecFolder, Path.GetFileName(TreeLocation));
            treeWatcher.Changed += treeWatcher_Changed;
            treeWatcher.Created += treeWatcher_Changed;
            treeWatcher.Deleted += treeWatcher_Changed;
            treeWatcher.Renamed += treeWatcher_Changed;

            treeWatcher.EnableRaisingEvents = true;

            IsSelected = Selected;

            if (!DontLoad)
                LoadTree();
        }

        void treeWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            Debug.WriteLine(GameVersion + " Tree renamed!");
        }

        void treeWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            Debug.WriteLine(GameVersion + " Tree changed: " + e.ChangeType);
        }

        public void SaveTree(string destinationFileName)
        {
            DebugOutput.PrintLn(String.Format("Saving ME{0} tree to file at: {1}", GameVersion, destinationFileName));
            using (FileStream fs = new FileStream(destinationFileName, FileMode.Create, FileAccess.Write))
            {
                using (GZipStream Compressor = new GZipStream(fs, CompressionLevel.Optimal))
                    WriteTreeToStream(Compressor);
            }
        }

        private void WriteTreeToStream(Stream output)
        {
            using (BinaryWriter bin = new BinaryWriter(output))
            {
                bin.Write(631991); // KFreon: Marker for advanced features
                bin.Write(TreeVersion);

                // KFreon: Write pccs scanned 
                bin.Write(PCCs.Count);
                foreach (TreePCC pcc in PCCs)
                    bin.Write(pcc.Name.Remove(0, MEExDirecs.BasePath.Length));  // writing as string? can change all others?

                // KFreon: Write Textures
                bin.Write(TexCount);
                for (int i = 0; i < TexCount; i++)
                {
                    TreeTexInfo tex = Textures[i];

                    // KFreon: Set texname if unknown - Prevents crashes on broken texture
                    if (String.IsNullOrEmpty(tex.EntryName))
                        tex.EntryName = "UNKNOWN";


                    bin.Write(tex.EntryName);

                    bin.Write(tex.Hash);

                    string fullpackage = tex.FullPackage;
                    if (String.IsNullOrEmpty(fullpackage))
                        fullpackage = "Base Package";
                    bin.Write(fullpackage);

                    /*string thumbpath = tex.ThumbnailPath != null ? tex.ThumbnailPath.Split('\\').Last() : "placeholder.ico";
                    bin.Write(thumbpath);*/

                    bin.Write(tex.NumMips);
                    bin.Write((int)tex.Format);
                    bin.Write(tex.PCCs.Count);


                    foreach (PCCEntry entry in tex.PCCs)
                    {
                        string tempfile = entry.File;
                        tempfile = tempfile.Remove(0, MEExDirecs.BasePath.Length + 1);

                        // KFreon: Write file entry
                        bin.Write(tempfile);

                        // KFreon: Write corresponding expID
                        bin.Write(entry.ExpID);
                    }
                }
            }
        }

        public void ConstructTree()
        {
            if (TreeTexes.Count != 0)
            {
                OnPropertyChanged("TreeTexes");
                OnPropertyChanged("NumTreeTexes");
                DebugOutput.PrintLn("TOTAL ME" + GameVersion + " TEXTURES: " + NumTreeTexes);
                return;
            }


            var folderPaths = Textures.Select(tex => tex.FullPackage).Distinct();

            List<TreeTexFolders> folders = new List<TreeTexFolders>();
            foreach (string path in folderPaths)
            {
                string[] nodes = path.Split('.');

                var topNodes = folders.Where(t => t.FolderName == nodes[0]);
                TreeTexFolders folder = null;
                if (topNodes?.Count() > 0)
                    folder = topNodes.First();
                else
                    folder = new TreeTexFolders(nodes.First(), null);

                folder.CreatePath(nodes.GetRange(1));
            }

            // KFreon: Sort tree
            folders.Sort((tex1, tex2) => tex1.FolderName.CompareTo(tex2.FolderName));
            Dispatcher.CurrentDispatcher.Invoke(() => TreeTexes.AddRange(folders));


            NumTreeTexes = Textures.Count;
            DebugOutput.PrintLn("TOTAL ME" + GameVersion + " TEXTURES: " + NumTreeTexes);
        }

        public void LoadTree(string filename = null)
        {
            Exists = false;
            Valid = false;
            AdvancedFeatures = false;
            if (File.Exists(filename ?? TreeLocation))
            {
                Exists = true;
                try
                {
                    FileStream fs = new FileStream(filename ?? TreeLocation, FileMode.Open, FileAccess.Read);
                    MemoryStream mem = UsefulThings.General.DecompressStream(fs);
                    if (mem == null)
                    {
                        ReadTree(fs);
                        fs.Dispose();
                    }
                    else
                    {
                        fs.Dispose();
                        using (mem)
                        {
                            ReadTree(mem);
                        }
                    }

                    Valid = true;
                }
                catch (Exception e)
                {
                    PCCs.Clear();
                    DebugOutput.PrintLn("Failed to load tree. Reason: ", "TreeDB Load", e);
                }
            }

            Task.Run(() =>
            {
                foreach (TreePCC pcc in PCCs)
                    if (File.Exists(pcc.Name))
                        pcc.Exists = true;
            });
        }

        private void ReadTree(Stream input)
        {
            input.Seek(0, SeekOrigin.Begin);
            using (BinaryReader bin = new BinaryReader(input))
            {
                int numTexes = bin.ReadInt32();
                if (numTexes == 1991)
                {
                    // KFreon: Pre WPF tree. ~rev 686
                    AdvancedFeatures = null;
                    numTexes = bin.ReadInt32();
                    DebugOutput.PrintLn("Medium ME" + GameVersion + " Tree features detected.");
                }
                else if (numTexes == 631991)
                {
                    // KFreon: WPF Tree
                    AdvancedFeatures = true;
                    TreeVersion = bin.ReadInt32();
                    numTexes = bin.ReadInt32();
                    DebugOutput.PrintLn("Advanced ME" + GameVersion + " Tree features detected.");
                }
                else
                    DebugOutput.PrintLn("ME" + GameVersion + " Tree features disabled.");


                if (AdvancedFeatures == true)
                {
                    // KFreon: numTexes = numPCCs here
                    for (int i = 0; i < numTexes; i++)
                    {
                        TreePCC pcc = new TreePCC();
                        string test = bin.ReadString();
                        pcc.Name = Path.Combine(MEExDirecs.BasePath, test);
                        PCCs.Add(pcc);
                    }

                    numTexes = bin.ReadInt32();
                }


                for (int i = 0; i < numTexes; i++)
                {
                    TreeTexInfo tempStruct = new TreeTexInfo(GameVersion, MEExDirecs.PathBIOGame, thumbs);

                    tempStruct.EntryName = ReadString(bin);
                    tempStruct.Hash = bin.ReadUInt32();
                    tempStruct.FullPackage = ReadString(bin);


                    if (AdvancedFeatures == null)
                    {
                        ReadString(bin);  // KFreon: Not used anymore, so just ignore it
                        //tempStruct.ThumbnailPath = Path.Combine(MEExDirecs.ThumbnailCache, thum);
                    }

                    tempStruct.NumMips = bin.ReadInt32();

                    if (AdvancedFeatures != true)
                    {
                        string format = ReadString(bin);
                        tempStruct.Format = CSharpImageLibrary.General.ImageFormats.FindFormatInString(format).InternalFormat;
                    }
                    else
                        tempStruct.Format = (CSharpImageLibrary.General.ImageEngineFormat)bin.ReadInt32();

                    int numFiles = bin.ReadInt32();

                    if (AdvancedFeatures != true)
                    {
                        List<string> pccs = new List<string>(numFiles);
                        for (int j = 0; j < numFiles; j++)
                        {
                            string tempStr = ReadString(bin);
                            pccs.Add(Path.Combine(MEExDirecs.BasePath, tempStr));
                        }

                        List<int> ExpIDs = new List<int>(numFiles);
                        for (int j = 0; j < numFiles; j++)
                            ExpIDs.Add(bin.ReadInt32());


                        tempStruct.PCCs.AddRange(PCCEntry.PopulatePCCEntries(pccs, ExpIDs));
                    }
                    else
                    {
                        List<PCCEntry> tempEntries = new List<PCCEntry>(numFiles);
                        for (int j = 0; j < numFiles; j++)
                        {
                            string file = ReadString(bin);
                            file = Path.Combine(MEExDirecs.BasePath, file);

                            int expID = bin.ReadInt32();

                            PCCEntry entry = new PCCEntry(file, expID);
                            tempEntries.Add(entry);
                        }
                        tempStruct.PCCs.AddRange(tempEntries);
                    }

                    Textures.Add(tempStruct);
                }
            }
        }

        private string ReadString(BinaryReader bin)
        {
            if (AdvancedFeatures == true)
                return bin.ReadString();
            else
            {
                int length = bin.ReadInt32();
                char[] str = bin.ReadChars(length);
                return new string(str);
            }
        }

        public void AddPCCs(List<string> files)
        {
            files.RemoveAll(file => file.ToUpperInvariant().EndsWith(".TFC"));

            List<TreePCC> temp = new List<TreePCC>();
            DateTime now = DateTime.Now;
            files.ForEach(file => temp.Add(new TreePCC(file, now)));

            PCCs.AddRange(temp);
        }

        public void AddPCC(string file)
        {
            if (!file.ToUpperInvariant().EndsWith(".TFC"))
                PCCs.Add(new TreePCC(file, DateTime.Now));
        }

        private TreeTexInfo CheckTreeAddition(TreeTexInfo tex, string UpperCaseFilenameOnly)
        {
            int count = 0;
            foreach (TreeTexInfo treeTex in Textures)
            {
                count++;
                if (treeTex.Compare(tex, UpperCaseFilenameOnly))
                    return treeTex;
            }

            return null;
        }

        public bool AddTex(TreeTexInfo tex, string UpperCaseFilenameOnly)
        {
            bool Added = true;
            lock (RunningLocker)
            {
                TreeTexInfo treeTex = CheckTreeAddition(tex, UpperCaseFilenameOnly);
                if (treeTex != null)
                {
                    treeTex.Update(tex);
                    Added = false;
                }
                else
                    Textures.Add(tex);
            }

            return Added;
        }

        internal void Delete()
        {
            if (Textures != null)
                Textures.Clear();

            if (PCCs != null)
                PCCs.Clear();

            if (TreeTexes != null)
                TreeTexes.Clear();

            NumTreeTexes = 0;
        }
    }
}
