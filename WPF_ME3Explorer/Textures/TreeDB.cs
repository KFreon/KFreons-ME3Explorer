using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer.Textures
{
    public class TreeDB : ViewModelBase
    {
        public MTRangedObservableCollection<TreeTexInfo> Textures { get; set; }
        readonly object Locker = new object();
        public MTRangedObservableCollection<string> ScannedPCCs { get; set; }
        MEDirectories.MEDirectories GameDirecs = null;

        public string TreeVersion { get; set; }

        public string TreePath { get; set; }
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
            Textures = new MTRangedObservableCollection<TreeTexInfo>();
            ScannedPCCs = new MTRangedObservableCollection<string>();
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
                }
            }
        }

        public bool ReadFromFile(string fileName)
        {
            if (!File.Exists(fileName))
                return false;

            TreePath = fileName;

            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Open))
                {
                    using (GZipStream compressed = new GZipStream(fs, CompressionMode.Decompress))  // Compressed for nice small trees
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

                            int length = bin.ReadInt32();
                            TreeVersion = new string(bin.ReadChars(length));
                            int texCount = bin.ReadInt32();
                            for (int i = 0; i < texCount; i++)
                            {
                                TreeTexInfo tex = new TreeTexInfo();
                                length = bin.ReadInt32();
                                tex.TexName = new string(bin.ReadChars(length));
                                tex.Hash = bin.ReadUInt32();
                                tex.StorageType = (Texture2D.storage)bin.ReadInt32();
                                length = bin.ReadInt32();
                                tex.FullPackage = new string(bin.ReadChars(length));
                                tex.Format = (TextureFormat)bin.ReadInt32();

                                Thumbnail thumb = new Thumbnail(GameDirecs.ThumbnailCachePath);
                                thumb.Offset = bin.ReadInt32();
                                thumb.Length = bin.ReadInt32();
                                tex.Thumb = thumb;

                                int numPccs = bin.ReadInt32();
                                for (int j = 0; j < numPccs; j++)
                                {
                                    length = bin.ReadInt32();
                                    string userAgnosticPath = new string(bin.ReadChars(length));
                                    tex.PCCS.Add(Path.Combine(GameDirecs.BasePath, userAgnosticPath));
                                }

                                for (int k = 0; k < numPccs; k++)
                                    tex.ExpIDs.Add(bin.ReadInt32());

                                Textures.Add(tex);
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

        public void SaveToFile(string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            {
                using (GZipStream compressed = new GZipStream(fs, CompressionMode.Compress))  // Compress for nice small trees
                {
                    using (BinaryWriter bw = new BinaryWriter(compressed))
                    {
                        bw.Write(631991);
                        bw.Write(GameVersion);
                        bw.Write(ToolsetInfo.Version.Length);
                        bw.Write(ToolsetInfo.Version);
                        bw.Write(Textures.Count);

                        foreach (TreeTexInfo tex in Textures)
                        {
                            bw.Write(tex.TexName.Length);
                            bw.Write(tex.TexName);
                            bw.Write(tex.Hash);
                            bw.Write((int)tex.StorageType);
                            bw.Write(tex.FullPackage.Length);
                            bw.Write(tex.FullPackage);
                            bw.Write((int)tex.Format);
                            bw.Write(tex.Thumb.Offset);
                            bw.Write(tex.Thumb.Length);
                            bw.Write(tex.PCCS.Count);
                            foreach (string pcc in tex.PCCS)
                            {
                                string tempPath = pcc.Remove(0, GameDirecs.BasePath.Length + 1);
                                bw.Write(tempPath.Length);
                                bw.Write(tempPath);
                            }

                            tex.ExpIDs.ForEach(exp => bw.Write(exp));
                        }
                    }
                }
            }
        }

        public void Clear(bool ClearPCCs = false)
        {
            Textures?.Clear();

            if (ClearPCCs)
                ScannedPCCs?.Clear();
        }
    }
}
