using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;
using UsefulThings;
using CSharpImageLibrary;

namespace WPF_ME3Explorer.Textures
{
    public class TreeDB : ViewModelBase
    {
        #region Static Properties
        static MTRangedObservableCollection<TreeTexInfo> ME1Textures { get; set; } = new MTRangedObservableCollection<TreeTexInfo>();
        static MTRangedObservableCollection<TreeTexInfo> ME2Textures { get; set; } = new MTRangedObservableCollection<TreeTexInfo>();
        static MTRangedObservableCollection<TreeTexInfo> ME3Textures { get; set; } = new MTRangedObservableCollection<TreeTexInfo>();

        static MTRangedObservableCollection<string> ME1ScannedPCCs { get; set; } = new MTRangedObservableCollection<string>();
        static MTRangedObservableCollection<string> ME2ScannedPCCs { get; set; } = new MTRangedObservableCollection<string>();
        static MTRangedObservableCollection<string> ME3ScannedPCCs { get; set; } = new MTRangedObservableCollection<string>();

        static MTRangedObservableCollection<TexplorerTextureFolder> ME1TextureFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();
        static MTRangedObservableCollection<TexplorerTextureFolder> ME2TextureFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();
        static MTRangedObservableCollection<TexplorerTextureFolder> ME3TextureFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();

        static MTRangedObservableCollection<TexplorerTextureFolder> ME1AllFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();
        static MTRangedObservableCollection<TexplorerTextureFolder> ME2AllFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();
        static MTRangedObservableCollection<TexplorerTextureFolder> ME3AllFolders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();
        #endregion Static Properties


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

        public MTRangedObservableCollection<TreeTexInfo> Textures
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1Textures;
                    case 2:
                        return ME2Textures;
                    case 3:
                        return ME3Textures;
                }

                return null;
            }
        }

        public MTRangedObservableCollection<string> ScannedPCCs
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1ScannedPCCs;
                    case 2:
                        return ME2ScannedPCCs;
                    case 3:
                        return ME3ScannedPCCs;
                }

                return null;
            }
        }

        public MTRangedObservableCollection<TexplorerTextureFolder> TextureFolders
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1TextureFolders;
                    case 2:
                        return ME2TextureFolders;
                    case 3:
                        return ME3TextureFolders;
                }

                return null;
            }
        }

        public MTRangedObservableCollection<TexplorerTextureFolder> AllFolders
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1AllFolders;
                    case 2:
                        return ME2AllFolders;
                    case 3:
                        return ME3AllFolders;
                }

                return null;
            }
        }

        readonly object Locker = new object();
        MEDirectories.MEDirectories GameDirecs = null;

        static string ME1TreeVersion = null;
        static string ME2TreeVersion = null;
        static string ME3TreeVersion = null;
        public string TreeVersion
        {
            get
            {
                switch (GameVersion)
                {
                    case 1:
                        return ME1TreeVersion;
                    case 2:
                        return ME2TreeVersion;
                    case 3:
                        return ME3TreeVersion;
                }
                return null;
            }
            set
            {
                switch (GameVersion)
                {
                    case 1:
                        ME1TreeVersion = value;
                        break;
                    case 2:
                        ME2TreeVersion = value;
                        break;
                    case 3:
                        ME3TreeVersion = value;
                        break;
                }
            }
        }

        public bool Exists
        {
            get
            {
                if (String.IsNullOrEmpty(TreePath))
                    return false;

                return File.Exists(TreePath);
            }
        }

        public string TreePath
        {
            get
            {
                return Path.Combine(MEDirectories.MEDirectories.StorageFolder, "Trees", $"ME{GameVersion}.tree");
            }
        }

        public int GameVersion
        {
            get
            {
                return GameDirecs.GameVersion;
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
                SetProperty(ref valid, value);
            }
        }

        public TreeDB(int gameversion)
        {
            GameDirecs = new MEDirectories.MEDirectories(gameversion);
        }

        public TreeDB(List<string> givenPCCs, int gameversion) : this(gameversion)
        {
            ScannedPCCs.AddRange(givenPCCs);
        }

        public void AddTexture(TreeTexInfo tex)
        {
            lock (Locker)
            {
                if (!Textures.Contains<TreeTexInfo>(tex))  // Enable comparison by IEquatable interface
                    Textures.Add(tex);
                else
                {
                    var existing = Textures[Textures.IndexOf(tex)];
                    existing.Update(tex);
                    tex.GenerateThumbnail = null;   // Clear generation code for GC to free up
                    return;
                }
            }
        }

        public bool ReadFromFile(string fileName = null)
        {
            lock (Textures)
            {
                if (Textures.Count > 0)  // When it comes back into this after Texplorer has been closed but the Toolset hasn't, it needs to "rebuild" itself i.e. mark itself as valid if it's been loaded previously.
                {
                    Valid = true;
                    return true;
                }
            }
            

            OnPropertyChanged(nameof(Exists));

            string tempFilename = fileName;
            if (fileName == null)
                tempFilename = TreePath;

            if (!File.Exists(tempFilename))
                return false;

            List<TreeTexInfo> TempTextures = new List<TreeTexInfo>();
            try
            {
                using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(tempFilename)))
                {
                    using (GZipStream compressed = new GZipStream(ms, CompressionMode.Decompress))  // Compressed for nice small trees
                    {
                        using (BinaryReader bin = new BinaryReader(compressed))
                        {
                            // Check tree is suitable for this version
                            int magic = bin.ReadInt32();
                            if (magic != 631991)
                            {
                                DebugOutput.PrintLn("Tree too old. Delete and rebuild tree.");
                                return false;
                            }

                            // Tree is suitable. Begin reading
                            int gameVersion = bin.ReadInt32();
                            if (GameDirecs.GameVersion != GameVersion)
                                throw new InvalidOperationException($"Incorrect Tree Loaded. Expected: ME{GameDirecs.GameVersion}, Got: {GameVersion}");

                            TreeVersion = bin.ReadString();

                            // PCCS
                            lock (ScannedPCCs)
                            {
                                int pccCount = bin.ReadInt32();
                                for (int i = 0; i < pccCount; i++)
                                    ScannedPCCs.Add(bin.ReadString());
                            }
                            

                            // Textures
                            lock (Textures)
                            {
                                int texCount = bin.ReadInt32();
                                for (int i = 0; i < texCount; i++)
                                {
                                    TreeTexInfo tex = new TreeTexInfo(GameDirecs);
                                    tex.TexName = bin.ReadString();
                                    tex.Hash = bin.ReadUInt32();
                                    //tex.StorageType = (Texture2D.storage)bin.ReadInt32();
                                    tex.FullPackage = bin.ReadString();
                                    tex.Format = (ImageEngineFormat)bin.ReadInt32();

                                    Thumbnail thumb = new Thumbnail(GameDirecs.ThumbnailCachePath);
                                    thumb.Offset = bin.ReadInt64();
                                    thumb.Length = bin.ReadInt32();
                                    tex.Thumb = thumb;

                                    tex.Mips = bin.ReadInt32();
                                    tex.Width = bin.ReadInt32();
                                    tex.Height = bin.ReadInt32();
                                    tex.LODGroup = bin.ReadString();

                                    int numPccs = bin.ReadInt32();
                                    for (int j = 0; j < numPccs; j++)
                                    {
                                        string userAgnosticPath = ScannedPCCs[bin.ReadInt32()];
                                        int ExpID = bin.ReadInt32();
                                        tex.PCCs.Add(new PCCEntry(Path.Combine(GameDirecs.BasePath, userAgnosticPath), ExpID, GameDirecs));
                                    }

                                    TempTextures.Add(tex);
                                }
                            }

                            lock (Textures)
                                Textures.AddRange(TempTextures);

                            // Sort ME1 files
                            if (GameVersion == 1)
                                ToolsetTextureEngine.ME1_SortTexturesPCCs(Textures);

                            // Texture folders
                            // Top all encompassing node
                            lock (TextureFolders)
                            {
                                TexplorerTextureFolder TopTextureFolder = new TexplorerTextureFolder("All Texture Files", null, null);

                                var folderCount = bin.ReadInt32();
                                var tempFolders = new List<TexplorerTextureFolder>();
                                for (int i = 0; i < folderCount; i++)
                                {
                                    var folder = ReadTreeFolders(bin, TopTextureFolder);
                                    tempFolders.Add(folder);
                                }

                                TopTextureFolder.Folders.AddRange(tempFolders);
                                TextureFolders.Add(TopTextureFolder);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn($"Failed to load tree: {fileName}. Reason: {e.ToString()}");
                return false;
            }

            Valid = true;
            return true;
        }

        public void SaveToFile(string fileName = null)
        {
            var start = Environment.TickCount;
            string tempFilename = fileName;

            if (fileName == null)
                tempFilename = TreePath;

            Directory.CreateDirectory(TreePath.GetDirParent()); // Create Trees directory if required

            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream compressed = new GZipStream(ms, CompressionMode.Compress, true))  // Compress for nice small trees
                {
                    using (BinaryWriter bw = new BinaryWriter(compressed, Encoding.Default, true))
                    {
                        bw.Write(631991);
                        bw.Write(GameVersion);
                        bw.Write(ToolsetInfo.Version);

                        // PCCs
                        bw.Write(ScannedPCCs.Count);
                        foreach (string pcc in ScannedPCCs)
                            bw.Write(pcc.Remove(0, GameDirecs.BasePath.Length + 1));

                        // Textures
                        bw.Write(Textures.Count);
                        foreach (TreeTexInfo tex in Textures)
                        {
                            bw.Write(tex.TexName);
                            bw.Write(tex.Hash);
                            //bw.Write((int)tex.StorageType);
                            bw.Write(tex.FullPackage);
                            bw.Write((int)tex.Format);
                            bw.Write(tex.Thumb.Offset);
                            bw.Write(tex.Thumb.Length);
                            bw.Write(tex.Mips);
                            bw.Write(tex.Width);
                            bw.Write(tex.Height);
                            bw.Write(tex.LODGroup);
                            bw.Write(tex.PCCs.Count);
                            foreach (PCCEntry pcc in tex.PCCs)
                            {
                                bw.Write(ScannedPCCs.IndexOf(pcc.Name));
                                bw.Write(pcc.ExpID);
                            }
                        }

                        // TextureFolders - NOT including top folder
                        bw.Write(TextureFolders[0].Folders.Count);
                        foreach (var folder in TextureFolders[0].Folders)
                            WriteTreeFolders(bw, folder);
                    }
                }

                ms.Seek(0, SeekOrigin.Begin);
                using (FileStream fs = new FileStream(tempFilename, FileMode.Create))
                    ms.CopyTo(fs);
            }
            var end = Environment.TickCount;
            Console.WriteLine($"Tree save elapsed: {end - start}");
        }

        void WriteTreeFolders(BinaryWriter bw, TexplorerTextureFolder folder)
        {
            // Details
            bw.Write(folder.Name);
            bw.Write(folder.Filter);

            // Folders
            bw.Write(folder.Folders.Count);
            if (folder.Folders.Count != 0)
                foreach (var fold in folder.Folders)
                    WriteTreeFolders(bw, fold);

            // Textures
            bw.Write(folder.Textures.Count);
            foreach (var tex in folder.Textures)  // Write indexes of textures instead of entire textures
                bw.Write(Textures.IndexOf(tex));
        }

        TexplorerTextureFolder ReadTreeFolders(BinaryReader br, TexplorerTextureFolder parent)
        {
            TexplorerTextureFolder folder = new TexplorerTextureFolder();
            AllFolders.Add(folder);
            folder.Parent = parent;

            // Details
            folder.Name = br.ReadString();
            folder.Filter = br.ReadString();

            // Folders
            int folderCount = br.ReadInt32();
            if (folderCount != 0)
                for (int i = 0; i < folderCount; i++)
                    folder.Folders.Add(ReadTreeFolders(br, folder));

            // Textures
            var texCount = br.ReadInt32();
            for (int i = 0; i < texCount; i++)
                folder.Textures.Add(Textures[br.ReadInt32()]);

            return folder;
        }

        public void ExportToCSV(string fileName, bool ShowFilesExpIDs)
        {
            StringBuilder sb = new StringBuilder();

            // KFreon: Headers
            sb.AppendLine("Texture Name, Format, Texmod Hash, Texture Package (LOD Group Stand-in)" + (ShowFilesExpIDs ? ", Files, Export IDs" : ""));

            for (int i = 0; i < Textures.Count; i++)
            {
                var tex = Textures[i];
                string line = $"{tex.TexName}, {tex.Format}, {ToolsetTextureEngine.FormatTexmodHashAsString(tex.Hash)}, {tex.FullPackage}";

                // KFreon: Make sure the lists have stuff in them - ? stops a null list from breaking when calling count. i.e. list.count when null = exception, but list?.count = null/false.
                if (ShowFilesExpIDs && tex.PCCs?.Count > 0)
                {
                    line += $", {tex.PCCs[0].Name}, {tex.PCCs[0].ExpID}";  // First line
                    sb.AppendLine(line);

                    if (tex.PCCs.Count > 1)
                        for (int j = 1; j < tex.PCCs.Count; j++)
                            sb.AppendLine($",,,,{tex.PCCs[j].Name}, {tex.PCCs[j].ExpID}");  // KFreon: ,,,'s are blank columns so these file/expid combos are in line with the others
                }
                else
                    sb.AppendLine(line);
            }

            File.WriteAllText(fileName, sb.ToString());
        }

        public void Clear(bool ClearPCCs = false)
        {
            Textures?.Clear();
            AllFolders?.Clear();
            TextureFolders?.Clear();

            if (ClearPCCs)
                ScannedPCCs?.Clear();

            Valid = false;
        }

        public void Delete()
        {
            try
            {
                File.Delete(TreePath);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn($"Unable to delete tree at: {TreePath}. Reason: {e.ToString()}.");
            }

            OnPropertyChanged(nameof(Exists));
            Valid = false;
        }

        internal void ConstructTree()
        {
            DebugOutput.PrintLn($"Constructing ME{GameVersion}Tree...");

            // Top all encompassing node
            TexplorerTextureFolder TopTextureFolder = new TexplorerTextureFolder("All Texture Files", null, null);

            // Normal nodes
            foreach (var tex in Textures)
                RecursivelyCreateFolders(tex.FullPackage, "", TopTextureFolder, tex);

            Console.WriteLine($"Total number of folders: {AllFolders.Count}");
            // Alphabetical order
            TopTextureFolder.Folders = new MTRangedObservableCollection<TexplorerTextureFolder>(TopTextureFolder.Folders.OrderBy(p => p));

            TextureFolders.Add(TopTextureFolder);  // Only one item in this list. Chuckles.

            DebugOutput.PrintLn($"ME{GameVersion} Tree Constructed!");
        }

        void RecursivelyCreateFolders(string package, string oldFilter, TexplorerTextureFolder topFolder, TreeTexInfo texture)
        {
            int dotInd = package.IndexOf('.') + 1;
            string name = package;
            if (dotInd != 0)
                name = package.Substring(0, dotInd).Trim('.');

            string filter = oldFilter + '.' + name;
            filter = filter.Trim('.');

            TexplorerTextureFolder newFolder = new TexplorerTextureFolder(name, filter, topFolder);

            // Add texture if part of this folder
            if (newFolder.Filter == texture.FullPackage)
                newFolder.Textures.Add(texture);

            TexplorerTextureFolder existingFolder = topFolder.Folders.FirstOrDefault(folder => newFolder.Name == folder.Name);
            if (existingFolder == null)  // newFolder not found in existing folders
            {
                topFolder.Folders.Add(newFolder);
                AllFolders.Add(newFolder);

                // No more folders in package
                if (dotInd == 0)
                    return;

                string newPackage = package.Substring(dotInd).Trim('.');
                RecursivelyCreateFolders(newPackage, filter, newFolder, texture);
            }
            else
            {  // No subfolders for newFolder yet, need to make them if there are any

                // Add texture if necessary
                if (existingFolder.Filter == texture.FullPackage)
                    existingFolder.Textures.Add(texture);

                // No more folders in package
                if (dotInd == 0)
                    return;

                string newPackage = package.Substring(dotInd).Trim('.');
                RecursivelyCreateFolders(newPackage, filter, existingFolder, texture);
            }
        }
    }
}
