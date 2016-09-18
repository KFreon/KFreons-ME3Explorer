using CSharpImageLibrary;
using SaltTPF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using UsefulThings;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer.Textures
{
    public class TPFTexInfo : AbstractTexInfo
    {
        ZipReader.ZipEntryFull ZipEntry = null;
        public MTRangedObservableCollection<TPFTexInfo> FileDuplicates { get; set; }
        public MTRangedObservableCollection<TPFTexInfo> HashDuplicates { get; set; }
        static CRC32 crc = new CRC32();

        uint fileHash = 0;
        public uint FileHash
        {
            get
            {
                return fileHash;
            }
            set
            {
                SetProperty(ref fileHash, value);
            }
        }

        bool analysed = false;
        public bool Analysed
        {
            get
            {
                return analysed;
            }
            set
            {
                SetProperty(ref analysed, value);
            }
        }

        public override string TexName
        {
            get
            {
                return base.TexName;
            }

            set
            {
                base.TexName = value;
                OnPropertyChanged(nameof(FoundInTree));
                Analysed = true;
                OnPropertyChanged(nameof(Name));
            }
        }

        public override string DefaultSaveName
        {
            get
            {
                string ext = Path.GetExtension(FileName);
                var hash = ToolsetTextureEngine.FormatTexmodHashAsString(Hash);
                return $"{Name.Replace(ext, "")}_{(Name.Contains(hash) ? "" : hash)}{ext}";
            }
        }

        public BitmapSource Preview
        {
            get
            {
                if (IsDef)
                    return null; // TODO: Text preview

                byte[] data = Extract();
                using (ImageEngineImage img = new ImageEngineImage(data))
                    return img.GetWPFBitmap();
            }
        }

        public bool FoundInTree
        {
            get
            {
                return TexName != null;
            }
        }

        public bool IsExternal
        {
            get
            {
                return FilePath != null;
            }
        }

        public string FullPath
        {
            get
            {
                return Path.Combine(FilePath, FileName);
            }
        }

        string filePath = null;
        public string FilePath
        {
            get
            {
                return filePath;
            }
            set
            {
                SetProperty(ref filePath, value);
                OnPropertyChanged(nameof(IsExternal));
            }
        }

        string filename = null;
        public string FileName
        {
            get
            {
                return filename;
            }
            set
            {
                SetProperty(ref filename, value);
                OnPropertyChanged(nameof(IsDef));
            }
        }

        public bool IsDef
        {
            get
            {
                return Path.GetExtension(FileName) == ".def";
            }
        }

        public string Name
        {
            get
            {
                return TexName ?? FileName;
            }
        }

        int treeMips = -1;
        public int TreeMips
        {
            get
            {
                return treeMips;
            }
            set
            {
                SetProperty(ref treeMips, value);
            }
        }

        ImageEngineFormat treeFormat = new ImageEngineFormat();
        public ImageEngineFormat TreeFormat
        {
            get
            {
                return treeFormat;
            }
            set
            {
                SetProperty(ref treeFormat, value);
            }
        }

        public TPFTexInfo(MEDirectories.MEDirectories gameDirecs) : base(gameDirecs)
        {
            GameDirecs = gameDirecs;
        }

        public TPFTexInfo(string file, ZipReader.ZipEntryFull entry, MEDirectories.MEDirectories gameDirecs) : this(gameDirecs)
        {
            ZipEntry = entry;

            if (Path.IsPathRooted(file))
            {
                FilePath = Path.GetDirectoryName(file);
                FileName = Path.GetFileName(file);
            }
            else
                FileName = file;
        }

        internal byte[] Extract()
        {
            byte[] data = null;
            if (IsExternal)
                data = File.ReadAllBytes(FullPath);
            else
                data = ZipEntry.Extract(true);

            return data;
        }

        internal void GetDetails()
        {
            byte[] data = Extract();
            GetDetails(data);
        }

        internal void GetDetails(byte[] imgData)
        {
            if (IsDef)
                return;

            try
            {
                // Hash data for duplicate checking purposes
                FileHash = crc.BlockChecksum(imgData);

                // Get image details and build thumbnail.
                DDSGeneral.DDS_HEADER header = null;
                using (MemoryStream ms = new MemoryStream(imgData))
                {
                    Format = ImageFormats.ParseFormat(ms, null, ref header).SurfaceFormat;
                    ImageEngineImage image = null;

                    if (header != null)
                    {
                        Width = header.dwWidth;
                        Height = header.dwWidth;
                        Mips = header.dwMipMapCount;
                        image = new ImageEngineImage(ms, null, 64, true);
                    }
                    else
                    {
                        image = new ImageEngineImage(ms);
                        Width = image.Width;
                        Height = image.Height;
                        Mips = image.NumMipMaps;
                    }

                    // Often the header of DDS' are not set properly resulting in Mips = 0
                    if (Mips == 0 && header != null)
                    {
                        int tempMips = 0;
                        DDSGeneral.EnsureMipInImage(ms.Length, Width, Height, 4, new CSharpImageLibrary.Format(Format), out tempMips);
                        Mips = tempMips;
                    }

                    // Thumbnail
                    Thumb.StreamThumb = new MemoryStream();
                    image.Save(Thumb.StreamThumb, ImageEngineFormat.JPG, MipHandling.Default, 64);

                    image.Dispose();
                }
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn($"Failed to get image information for: {Name}. Reason: {e.ToString()}.");
            }
        }

        internal void Extract(string destFilePath)
        {
            File.WriteAllBytes(destFilePath, Extract());
        }

        public override List<string> Searchables
        {
            get
            {
                if (IsDef)
                    return new List<string>();  // Blank so as not to be included in search

                var baseSearchables = base.Searchables;

                // TPFTools specific ones
                baseSearchables.Add(TreeFormat.ToString());
                baseSearchables.Add(FilePath);
                baseSearchables.Add(FileName);
                baseSearchables.Add(ZipEntry.Filename);

                baseSearchables.RemoveAll(t => t == null); // Remove any nulls again.

                return baseSearchables.Distinct().ToList();  // Remove any duplicates
            }
        }

    }
}
