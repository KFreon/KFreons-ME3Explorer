using CSharpImageLibrary;
using SaltTPF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer.Textures
{
    public class TPFTexInfo : AbstractTexInfo
    {
        ZipReader.ZipEntryFull ZipEntry = null;

        public override string DefaultSaveName
        {
            get
            {
                string ext = Path.GetExtension(FileName);
                return $"{Name.Replace(ext, "")}_{ToolsetTextureEngine.FormatTexmodHashAsString(Hash)}.{ext}.";
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

        internal void GetDetails()
        {                    
            byte[] imgData = Extract();
            if (imgData == null)
                return;

            try
            {
                using (ImageEngineImage image = new ImageEngineImage(imgData))
                {
                    Width = image.Width;
                    Height = image.Height;
                    Mips = image.NumMipMaps;
                    Format = image.Format.SurfaceFormat;

                    // Thumbnail
                    Thumb.StreamThumb = new MemoryStream();
                    image.Save(Thumb.StreamThumb, ImageEngineFormat.JPG, MipHandling.Default, 64);
                }
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn($"Failed to get image information for: {Name}. Reason: {e.ToString()}.");
            }
        }

        private byte[] Extract()
        {
            byte[] data = null;
            if (IsExternal)
                data = File.ReadAllBytes(FullPath);
            else
                data = ZipEntry.Extract(true);

            return data;
        }

        private void Extract(string destFilePath)
        {
            File.WriteAllBytes(destFilePath, Extract());
        }
    }
}
