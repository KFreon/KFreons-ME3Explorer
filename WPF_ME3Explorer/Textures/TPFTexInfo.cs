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
        public MTRangedObservableCollection<TPFTexInfo> FileDuplicates { get; set; } = new MTRangedObservableCollection<TPFTexInfo>();
        public MTRangedObservableCollection<TPFTexInfo> HashDuplicates { get; set; } = new MTRangedObservableCollection<TPFTexInfo>();

        public static CommandHandler InstallCommand { get; set; }
        public static CommandHandler ExtractCommand { get; set; }
        public static CommandHandler ReplaceCommand { get; set; }

        int inGameWidth = -1;
        public int InGameWidth
        {
            get
            {
                return inGameWidth;
            }
            set
            {
                SetProperty(ref inGameWidth, value);
            }
        }

        int inGameHeight = -1;
        public int InGameHeight
        {
            get
            {
                return inGameHeight;
            }
            set
            {
                SetProperty(ref inGameHeight, value);
            }
        }

        public bool RequiresAutofix
        {
            get
            {
                return Analysed && !FormatOK;
            }
        }

        public bool MipsOK
        {
            get
            {
                return Mips >= TreeMips;
            }
        }

        public bool FormatOK
        {
            get
            {
                return Format == TreeFormat;
            }
        }

        string error = null;
        public string Error
        {
            get
            {
                return error;
            }
            set
            {
                SetProperty(ref error, value);
            }
        }

        bool isChecked = true;
        public bool IsChecked
        {
            get
            {
                return isChecked;
            }
            set
            {
                SetProperty(ref isChecked, value);
            }
        }

        public bool IsFromTPF
        {
            get
            {
                return ZipEntry != null;
            }
        }

        public String TPF_Comment
        {
            get
            {
                return ZipEntry?.TPF_Comment;
            }
        }

        public String TPF_Author
        {
            get
            {
                return ZipEntry?.TPF_Author;
            }
        }

        public String TPF_FileName
        {
            get
            {
                return ZipEntry?.TPF_FileName;
            }
        }

        public int TPF_EntryCount
        {
            get
            {
                return ZipEntry?.TPF_EntryCount ?? 0;
            }
        }

        // KFreon: Actual hash of the file, since 'hash' is the hash of the texture it's replacing. 
        // This is mostly used for duplicate detection.
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
                OnPropertyChanged(nameof(RequiresAutofix));
                OnPropertyChanged(nameof(FoundInTree));
                OnPropertyChanged(nameof(FormatOK));
                OnPropertyChanged(nameof(MipsOK));
                OnPropertyChanged(nameof(PCCs));
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
                return $"{Name.Replace(ext, "")}{(Name.Contains(hash) ? "" : "_" + hash)}{ext}";
            }
        }

        public BitmapSource Preview
        {
            get
            {
                if (IsDef)
                    return null;

                byte[] data = Extract();


                // Need to do it this way to support images that are unsupported by WIC.
                using (ImageEngineImage img = new ImageEngineImage(data))
                {
                        return img.GetWPFBitmap();
                        /*img.Save(ms, new ImageFormats.ImageEngineFormatDetails(ImageEngineFormat.PNG), MipHandling.KeepTopOnly, removeAlpha: false);
                        var overlayed = ToolsetTextureEngine.OverlayAndPickDetailed(ms);
                        return UsefulThings.WPF.Images.CreateWPFBitmap(overlayed);*/
                }
            }
        }

        public string DefPreview
        {
            get
            {
                if (!IsDef)
                    return null; 

                byte[] data = Extract();
                char[] text = Array.ConvertAll(data, t => (char)t);

                return new string(text).Replace("\0", "");
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
                string ext = Path.GetExtension(FileName);
                return ext.EndsWith("def", StringComparison.OrdinalIgnoreCase) || ext.EndsWith("log", StringComparison.OrdinalIgnoreCase);
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

        public TPFTexInfo(TreeTexInfo tex, string newFile)
        {
            Width = tex.Width;
            Height = tex.Height;
            Hash = tex.Hash;
            OriginalHash = Hash;
            GameDirecs = tex.GameDirecs;
            FileName = Path.GetFileName(newFile);
            FilePath = Path.GetDirectoryName(newFile);

            var bytes = File.ReadAllBytes(newFile);
            FileHash = CRC32.BlockChecksum(bytes);

            using (ImageEngineImage img = new ImageEngineImage(bytes))
            {
                Format = img.Format;
                Mips = img.NumMipMaps;
                Thumb = new Thumbnail(img.Save(new ImageFormats.ImageEngineFormatDetails(ImageEngineFormat.JPG), MipHandling.KeepTopOnly, 64));
            }
            PCCs.AddRange(tex.PCCs.Where(p => p.IsChecked));
            TexName = tex.TexName;
            TreeFormat = tex.Format;
            TreeMips = tex.Mips;

            Analysed = true;
        }

        public TPFTexInfo(TreeTexInfo tex, ImageEngineImage newImage)
        {
            Width = tex.Width;
            Height = tex.Height;
            Hash = tex.Hash;
            OriginalHash = Hash;
            GameDirecs = tex.GameDirecs;
            FileName = Path.GetFileName(newImage.FilePath);
            FilePath = Path.GetDirectoryName(newImage.FilePath);

            var bytes = newImage.OriginalData;
            FileHash = CRC32.BlockChecksum(bytes);


            Format = newImage.Format;
            Mips = newImage.NumMipMaps;
            Thumb = new Thumbnail(newImage.Save(new ImageFormats.ImageEngineFormatDetails(ImageEngineFormat.JPG), MipHandling.KeepTopOnly, 64));
            
            PCCs.AddRange(tex.PCCs.Where(p => p.IsChecked));
            TexName = tex.TexName;
            TreeFormat = tex.Format;
            TreeMips = tex.Mips;

            Analysed = true;
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

        internal async Task GetDetails()
        {
            byte[] data = Extract();
            await GetDetails(data);
        }

        internal async Task GetDetails(byte[] imgData)
        {
            if (IsDef)
                return;

            try
            {
                // Hash data for duplicate checking purposes
                var hashGetter = Task.Run(() => CRC32.BlockChecksum(imgData)); // Put it off thread

                // Get image details and build thumbnail.
                using (MemoryStream ms = new MemoryStream(imgData, 0, imgData.Length, false, true))
                {
                    CSharpImageLibrary.Headers.DDS_Header header = new CSharpImageLibrary.Headers.DDS_Header(ms);
                    Format = header.Format;
                    ImageEngineImage image = new ImageEngineImage(ms);
                    Width = image.Width;
                    Height = image.Height;
                    Mips = image.NumMipMaps;
                    

                    // Thumbnail
                    Thumb.StreamThumb = new MemoryStream(image.Save(new ImageFormats.ImageEngineFormatDetails(ImageEngineFormat.JPG), MipHandling.Default, 64));
                    image.Dispose();
                }

                FileHash = await hashGetter;
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
                baseSearchables.Add(ZipEntry?.Filename);

                baseSearchables.RemoveAll(t => t == null); // Remove any nulls again.

                return baseSearchables.Distinct().ToList();  // Remove any duplicates
            }
        }
    }
}
