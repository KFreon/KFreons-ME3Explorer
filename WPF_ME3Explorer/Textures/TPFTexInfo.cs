using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CSharpImageLibrary.General;
using SaltTPF;
using UsefulThings;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer.Textures
{
    public class TPFTexInfo : AbstractTexInfo
    {
        public string AutoFixPath
        {
            get
            {
                return Path.Combine(UsefulThings.General.GetExecutingLoc(), "TPFToolsTEMP", "ME" + GameVersion, EntryName + (Path.GetExtension(EntryName) != ".dds" ? ".dds" : ""));
            }
        }

        public BitmapSource Preview
        {
            get
            {
                if (IsDef)
                    return null;

                byte[] data = Extract();
                if (data == null)
                    return null;

                try
                {
                    using (MemoryStream datastream = new MemoryStream(data))
                    using (MemoryStream ms = ImageEngine.GenerateThumbnailToStream(datastream, 1024))
                        return UsefulThings.WPF.Images.CreateWPFBitmap(ms);
                }
                catch (Exception e)
                {
                    DebugOutput.PrintLn("Failed to get preview: " + OriginalEntryName, "TPFTexInfo", e);
                }

                return null;
            }
        }


        #region Properties
        public MTRangedObservableCollection<TPFTexInfo> FileDuplicates { get; set; }

        public string SourceInfo
        {
            get
            {
                return IsExternal ? "External file\n\nPath: " + FilePath + "\\" + EntryName + Path.GetExtension(OriginalEntryName) : Zippy.Description;
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

        int expectedMips = -1;
        public int ExpectedMips
        {
            get
            {
                return expectedMips;
            }
            set
            {
                SetProperty(ref expectedMips, value);
                ValidMips = ExpectedMips <= NumMips;
            }
        }

        ImageEngineFormat expectedFormat = ImageEngineFormat.Unknown;
        public ImageEngineFormat ExpectedFormat
        {
            get
            {
                return expectedFormat;
            }
            set
            {
                SetProperty(ref expectedFormat, value);
                ValidFormat = ExpectedFormat == Format;
            }
        }

        public string FilePath { get; set; }
        public int TPFEntryIndex { get; set; }
        readonly object previewlocker = new object();


        public bool IsExternal
        {
            get
            {
                return FilePath != null;
            }
        }
        public bool IsDef
        {
            get
            {
                return (EntryName.Contains(".def", StringComparison.CurrentCultureIgnoreCase) || EntryName.Contains(".txt", StringComparison.CurrentCultureIgnoreCase) || EntryName.Contains(".log", StringComparison.CurrentCultureIgnoreCase)) ? true : false;
            }
        }
        public ZipReader Zippy { get; set; }

        public string DefPreview
        {
            get
            {
                return String.Join(Environment.NewLine, Zippy.DefLines);
            }
        }

        BitmapSource thumbnail = null;
        public BitmapSource Thumbnail
        {
            get
            {
                return thumbnail;
            }
            set
            {
                SetProperty(ref thumbnail, value);
            }
        }
        public List<string> LogContents { get; set; }
        #endregion Properties


        public ICommand AutoFixCommand { get; set; }

        public ICommand ExtractConvertCommand { get; set; }   // KFreon: Called by ExtractConvertButton


        private ImageEngineFormat saveFormat = ImageEngineFormat.Unknown;
        public ImageEngineFormat SaveFormat
        {
            get
            {
                return saveFormat;
            }
            set
            {
                SetProperty(ref saveFormat, value);
            }
        }

        private bool validDimension = false;
        public bool ValidDimension
        {
            get
            {
                return validDimension;
            }
            set
            {
                SetProperty(ref validDimension, value);
            }
        }

        public bool ValidTexture
        {
            get
            {
                return ValidFormat && ValidMips && ValidDimension;
            }
        }
        public Action<TPFTexInfo, ImageEngineFormat> ExtractConvertDelegate { get; set; }
        public Action<TPFTexInfo> InstallDelegate { get; set; }

        public List<string> ValidImageFormats
        {
            get
            {
                return Enum.GetNames(typeof(ImageEngineFormat)).ToList();
            }
        }

        public ICommand InstallCommand { get; set; }
        public ICommand ResetHashCommand { get; set; }

        bool validFormat = false;
        public bool ValidFormat
        {
            get
            {
                return validFormat;
            }
            set
            {
                SetProperty(ref validFormat, value);
                OnPropertyChanged("ValidTexture");
            }
        }

        bool validMips = false;
        public bool ValidMips
        {
            get
            {
                return validMips;
            }
            set
            {
                SetProperty(ref validMips, value);
                OnPropertyChanged("ValidTexture");
            }
        }


        string originalEntryName = null;
        public string OriginalEntryName
        {
            get
            {
                return originalEntryName;
            }
            set
            {
                SetProperty(ref originalEntryName, value);
            }
        }

        public RangedObservableCollection<TPFTexInfo> TreeDuplicates { get; set; }

        public Action<TPFTexInfo> ReplaceDelegate { get; set; }
        public Action<TPFTexInfo> AutoFixDelegate { get; set; }
        public ICommand ReplaceCommand { get; set; }

        public TPFTexInfo() : base()
        {
            ReplaceCommand = new UsefulThings.WPF.CommandHandler(t =>
            {
                if (ReplaceDelegate != null)
                {
                    ReplaceDelegate(this);
                }
            }, true);

            ExtractConvertCommand = new UsefulThings.WPF.CommandHandler(t =>
            {
                if (ExtractConvertDelegate != null)
                {
                    ExtractConvertDelegate(this, SaveFormat);
                }
            }, true);

            AutoFixCommand = new UsefulThings.WPF.CommandHandler(t =>
            {
                if (AutoFixDelegate != null)
                    AutoFixDelegate(this);
            });


            InstallCommand = new UsefulThings.WPF.CommandHandler(t =>
            {
                if (InstallDelegate != null)
                    InstallDelegate(this);
            });

            ResetHashCommand = new UsefulThings.WPF.CommandHandler(t =>
            {
                Hash = OriginalHash;
            }, true);

            LogContents = new List<string>();
            FileDuplicates = new MTRangedObservableCollection<TPFTexInfo>();
            TreeDuplicates = new RangedObservableCollection<TPFTexInfo>();
        }

        public TPFTexInfo(string filename, string path, int tpfind, ZipReader zippy, int gameVersion) : this()
        {
            EntryName = filename;
            OriginalEntryName = filename;
            TPFEntryIndex = tpfind;
            Zippy = zippy;
            GameVersion = gameVersion;
            FilePath = path;

            OnPropertyChanged("IsDef");
        }

        public TPFTexInfo(TreeTexInfo treetex, TPFTexInfo orig) : this(treetex.EntryName, orig.FilePath, orig.TPFEntryIndex, orig.Zippy, treetex.GameVersion)
        {
            OriginalEntryName = orig.OriginalEntryName;
            OriginalHash = orig.OriginalHash;
            AutoFixDelegate = orig.AutoFixDelegate;
            ExtractConvertDelegate = orig.ExtractConvertDelegate;
            ReplaceDelegate = orig.ReplaceDelegate;
            InstallDelegate = orig.InstallDelegate;
            Hash = orig.Hash;
            LogContents = orig.LogContents;
            EnumerateDetails();
        }

        public bool EnumerateDetails(string imagePath = null)
        {
            // KFreon: Textures only
            if (IsDef)
                return false;

            ImageEngineImage img = null;

            if (imagePath == null)
            {
                byte[] data = Extract();
                if (data == null)
                    DebugOutput.PrintLn("Unable to get image data for: " + EntryName);
                else
                    img = new ImageEngineImage(data);
            }
            else
                img = new ImageEngineImage(imagePath);

            if (img != null)
            {
                try
                {

                    Width = img.Width;
                    Height = img.Height;
                    NumMips = img.NumMipMaps;
                    Format = ImageFormats.FindFormatInString(img.Format.InternalFormat.ToString()).InternalFormat;

                    Thumbnail = img.GetWPFBitmap(64);

                }
                catch (Exception e)
                {
                    Format = ImageEngineFormat.Unknown;
                    DebugOutput.PrintLn("Failed to process image through ResIL: " + OriginalEntryName, "TPFTools EnumerateDetails", e);
                }
                finally
                {
                    img.Dispose();
                }

                ValidFormat = Format == ExpectedFormat;
                ValidMips = NumMips >= ExpectedMips;
                ValidDimension = ValidateDimensions();

                return true;
            }

            return false;
        }

        private bool ValidateDimensions()
        {
            return UsefulThings.General.IsPowerOfTwo(Width) && UsefulThings.General.IsPowerOfTwo(Height);
        }

        public byte[] Extract()
        {
            byte[] imgdata = null;
            try
            {
                if (IsExternal)
                    imgdata = UsefulThings.General.GetExternalData(File.Exists(AutoFixPath) ? AutoFixPath : OriginalEntryName);
                else
                    imgdata = Zippy.Entries[TPFEntryIndex].Extract(true);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to extract image data.", "TPFTools Extract Image", e);
            }
            return imgdata;
        }

        public bool Compare(TPFTexInfo tex)
        {
            if (tex.IsDef || IsDef)
                return false;

            return Hash == tex.Hash && tex.EntryName == EntryName;
        }

        internal TPFTexInfo UpdateFromTreeTex(TreeTexInfo treetex)
        {
            TPFTexInfo newtex = null;
            if (Hash == treetex.Hash)
            {
                if (PCCs.Count == 0)
                    UpdateTex(treetex);
                else
                {
                    newtex = new TPFTexInfo(treetex, this);
                    TreeDuplicates.Add(newtex);  // filedupes?
                    newtex.TreeDuplicates.Add(this);
                    newtex.Analysed = true;
                    newtex.UpdateTex(treetex);
                }
            }
            Analysed = true;
            return newtex;
        }

        private void UpdateTex(TreeTexInfo treetex)
        {
            PCCs.AddRange(treetex.PCCs);

            // KFreon: Get expected stuff
            ExpectedFormat = treetex.Format;
            ExpectedMips = treetex.NumMips;
            EntryName = treetex.EntryName;
        }

        public bool ExtractConvert(string destinationName, ImageEngineFormat format, ImageEngine.MipHandling fixMips = ImageEngine.MipHandling.GenerateNew)
        {
            using (ImageEngineImage img = new ImageEngineImage(Extract()))
            {
                var destinationFormat = ImageEngine.ParseFromString(format.ToString());
                return img.Save(destinationName, destinationFormat, fixMips);
            }
        }

        public bool Replace(string newImage)
        {
            this.FilePath = Path.GetDirectoryName(newImage);
            return EnumerateDetails(newImage);
        }
    }
}
