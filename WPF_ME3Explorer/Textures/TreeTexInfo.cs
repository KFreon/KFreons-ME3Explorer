using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpImageLibrary;
using SaltTPF;
using UsefulThings;
using WPF_ME3Explorer.PCCObjectsAndBits;
using static WPF_ME3Explorer.Textures.Texture2D;
using UsefulThings.WPF;
using System.Diagnostics;

namespace WPF_ME3Explorer.Textures
{
    public class TreeTexInfo : AbstractTexInfo, IEquatable<TreeTexInfo>, IComparable
    {
        static int ThumbnailSize = 128;

        public static CommandHandler ExtractCommand { get; set; }
        public static CommandHandler ChangeCommand { get; set; }
        public static CommandHandler LowResFixCommand { get; set; }
        public static CommandHandler RegenerateThumbCommand { get; set; }
        #region Properties


        public Action GenerateThumbnail = null;

        public List<Texture2D> Textures = new List<Texture2D>();

        string fullPackage = null;
        public string FullPackage
        {
            get
            {
                return fullPackage;
            }
            set
            {
                SetProperty(ref fullPackage, value);
            }
        }

        public string Package
        {
            get
            {
                if (String.IsNullOrEmpty(FullPackage))
                    return "";
                string temppack = FullPackage.Remove(FullPackage.Length - 1);
                var bits = temppack.Split('.');
                if (bits.Length > 1)
                    return bits[bits.Length - 1];
                else
                    return bits[0];
            }
        }

        public storage StorageType { get; set; }

        string textureCache = null;
        public string TextureCache
        {
            get
            {
                return textureCache;
            }
            set
            {
                SetProperty(ref textureCache, value);
            }
        }

        string lodGroup = null;
        public string LODGroup
        {
            get
            {
                return lodGroup;
            }
            set
            {
                SetProperty(ref lodGroup, value);
            }
        }

        int height = 0;
        public override int Height
        {
            get
            {
                return height;
            }

            set
            {
                SetProperty(ref height, value);
            }
        }

        int width = 0;
        public override int Width
        {
            get
            {
                return width;
            }

            set
            {
                SetProperty(ref width, value);
            }
        }

        int mips = 0;
        public override int Mips
        {
            get
            {
                return mips;
            }

            set
            {
                SetProperty(ref mips, value);
            }
        }
#endregion Properties

        public TreeTexInfo() : base()
        {

        }

        public TreeTexInfo(Texture2D tex2D, ThumbnailWriter ThumbWriter, ExportEntry export, Dictionary<string, MemoryStream> TFCs) : this()
        {
            TexName = tex2D.texName;
            Format = tex2D.texFormat;
            GameVersion = tex2D.GameVersion;
            FullPackage = export.PackageFullName;

            // Hash
            ImageInfo info = tex2D.ImageList[0];
            uint hash = 0;
            CRC32 crcgen = new CRC32();
            StorageType = info.storageType;

            byte[] imgData = null;
            try
            {
                imgData = tex2D.ExtractImage(info.ImageSize, TFCs);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"LZO failure on {TexName}, {FullPackage} in {tex2D.allPccs[0]}. Reason: {e.ToString()}");
                throw;
            }

            if (info.storageType == storage.pccSto)
            {
                if (tex2D.texFormat == ImageEngineFormat.DDS_ATI2_3Dc)
                    hash = ~crcgen.BlockChecksum(imgData, 0, info.UncompressedSize / 2);
                else
                    hash = ~crcgen.BlockChecksum(imgData);
            }
            else
            {
                if (imgData == null)
                    hash = 0;
                else
                {
                    if (tex2D.texFormat == ImageEngineFormat.DDS_ATI2_3Dc)
                        hash = ~crcgen.BlockChecksum(imgData, 0, info.UncompressedSize / 2);
                    else
                        hash = ~crcgen.BlockChecksum(imgData);
                }
            }

            tex2D.Hash = hash;
            Hash = hash;

            // Don't generate thumbnail till necessary i.e. not a duplicate texture - This is done after the check in the TreeDB
            GenerateThumbnail = new Action(() => CreateThumbnail(imgData, tex2D, ThumbWriter, info, TFCs));

            for (int i = 0; i < tex2D.allPccs.Count; i++)
                PCCS.Add(new PCCEntry(tex2D.allPccs[i], tex2D.expIDs[i]));

            // KFreon: ME2 only?
            if (export.PackageFullName == "Base Package")
                FullPackage = Path.GetFileNameWithoutExtension(PCCS[0].Name).ToUpperInvariant();   // Maybe not right?
            else
                FullPackage = export.PackageFullName.ToUpperInvariant();
        }

        public MemoryStream CreateThumbnail()
        {
            using (PCCObject pcc = new PCCObject(PCCS[0].Name, GameVersion))
            {
                using (Texture2D tex2D = new Texture2D(pcc, PCCS[0].ExpID, GameVersion))
                {
                    byte[] imgData = null;
                    var size = tex2D.ImageList.Where(img => img.ImageSize.Width == ThumbnailSize && img.ImageSize.Height == ThumbnailSize);
                    if (size.Count() == 0)
                        imgData = tex2D.ExtractMaxImage();
                    else
                        imgData = tex2D.ExtractImage(size.First());

                    using (MemoryStream ms = new MemoryStream(imgData))
                        return ImageEngine.GenerateThumbnailToStream(ms, ThumbnailSize, ThumbnailSize);
                }
            }
        }

        void CreateThumbnail(byte[] imgData, Texture2D tex2D, ThumbnailWriter ThumbWriter, ImageInfo info, Dictionary<string, MemoryStream> TFCs)
        {
            byte[] thumbImageData = imgData;

            // Try to get a smaller mipmap to use so don't need to resize.
            var thumbInfo = tex2D.ImageList.Where(img => img.ImageSize.Width <= ThumbnailSize && img.ImageSize.Height <= ThumbnailSize).FirstOrDefault();
            if (thumbInfo.ImageSize != null) // i.e.image size doesn't exist.
                thumbImageData = tex2D.ExtractImage(thumbInfo.ImageSize, TFCs);

            using (MemoryStream ms = new MemoryStream(thumbImageData))
            {
                uint width = info.ImageSize.Width;
                uint height = info.ImageSize.Height;

                try
                {
                    MemoryStream thumbStream = ImageEngine.GenerateThumbnailToStream(ms, width > height ? ThumbnailSize : 0, width > height ? 0 : ThumbnailSize);
                    thumbStream = ToolsetTextureEngine.OverlayAndPickDetailed(thumbStream);
                    if (thumbStream != null)
                        Thumb = ThumbWriter.Add(thumbStream);
                }
                catch(Exception e)
                {
                    Debug.WriteLine($"Failed to create thumbnail for {tex2D.texName} in {tex2D.allPccs[0]}. Reason: {e.ToString()}.");
                }
            }

            tex2D.Dispose();
        }

        public void ReorderME2Files()
        {
            if (GameVersion == 2 && (!String.IsNullOrEmpty(Textures[0].arcName) && Textures[0].arcName != "None"))
            {
                for (int i = 0; i < PCCS.Count; i++)
                {
                    using (PCCObject pcc = new PCCObject(PCCS[i].Name, GameVersion))
                    {
                        using (Texture2D tex = new Texture2D(pcc, PCCS[i].ExpID, GameVersion))
                        {
                            string arc = tex.arcName;
                            if (i == 0 && (arc != "None" && !String.IsNullOrEmpty(arc)))
                                break;
                            else if (arc != "None" && !String.IsNullOrEmpty(arc))
                            {
                                PCCEntry file = PCCS.Pop(i);  // Removes and returns
                                PCCS.Insert(0, file);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void Update(TreeTexInfo tex)
        {
            // Add different PCCs and corresponding expIDs
            for (int i = 0; i < tex.PCCS.Count; i++)
            {
                if (PCCS.Contains(tex.PCCS[i]))
                {
                    bool duplicateDetected = false;
                    for (int j = 0; j < PCCS.Count; j++)
                    {
                        // To be a "duplicate", both pcc and expid must be the same.
                        if (PCCS[j] == tex.PCCS[i])
                        {
                            duplicateDetected = true;
                            break;
                        }
                    }

                    // Don't add if already existing
                    if (duplicateDetected)
                        continue;
                }

                PCCS.Add(tex.PCCS[i]);
            }

            foreach (Texture2D tex2D in tex.Textures)
                if (!Textures.Contains(tex2D))
                    Textures.Add(tex2D);

            if (Hash == 0)
                Hash = tex.Hash;
        }

        public bool Equals(TreeTexInfo tex)
        {
            if (TexName == tex.TexName && Hash != 0 && Hash == tex.Hash)
                return true; // ME1 thing with packages?

            return false;
        }

        public override bool Equals(object obj)
        {
            TreeTexInfo tex = obj as TreeTexInfo;
            if (tex == null)
                return false;

            return Equals(tex);
        }

        public override int GetHashCode()
        {
            return (int)(TexName.GetHashCode() ^ Hash);  // No reason, just made up this bit.
        }

        public int CompareTo(object obj)
        {
            // Any sorting is to be done on Texture Name only.
            TreeTexInfo tex = obj as TreeTexInfo;
            if (tex == null)
                throw new InvalidCastException("Object must be TreeTexInfo.");

            return TexName.CompareTo(tex.TexName);
        }

        internal void PopulateDetails()
        {
            using (PCCObject pcc = new PCCObject(PCCS[0].Name, GameVersion))
            {
                using (Texture2D tex2D = new Texture2D(pcc, PCCS[0].ExpID, GameVersion))
                {
                    ImageInfo maxImg = tex2D.ImageList.Max();
                    Width = (int)maxImg.ImageSize.Width;
                    Height = (int)maxImg.ImageSize.Height;
                    TextureCache = tex2D?.FullArcPath?.Remove(0, MEDirectories.MEDirectories.BasePathLength) ?? "PCC Stored";
                    LODGroup = tex2D.LODGroup ?? "None (Uses World)";
                    Mips = tex2D.ImageList.Count;
                }
            }
        }
    }
}
