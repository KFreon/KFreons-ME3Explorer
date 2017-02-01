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
        public static CommandHandler ExtractCommand { get; set; }
        public static CommandHandler ChangeCommand { get; set; }
        public static CommandHandler LowResFixCommand { get; set; }
        public static CommandHandler RegenerateThumbCommand { get; set; }
        public static CommandHandler ExportTexAndInfoCommand { get; internal set; }

        public int TextureListIndex = -1;

        CommandHandler restoreOriginalCommand = null;
        public CommandHandler RestoreOriginalCommand
        {
            get
            {
                if (restoreOriginalCommand == null)
                {
                    restoreOriginalCommand = new CommandHandler(() =>
                    {
                        HasChanged = false;
                        ChangedAssociatedTexture = null;
                    });
                }

                return restoreOriginalCommand;
            }
        }

        #region Properties
        public Action GenerateThumbnail = null;

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

        storage storageType = storage.empty;
        public storage StorageType
        {
            get
            {
                return storageType;
            }
            set
            {
                SetProperty(ref storageType, value);
            }
        }


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

        bool hasChanged = false;
        public bool HasChanged
        {
            get
            {
                return hasChanged;
            }
            set
            {
                SetProperty(ref hasChanged, value);

                // Reset things when they need to be
                if (!value)
                {
                    Thumb.StreamThumb = null;
                    ChangedAssociatedTexture = null;
                    OnPropertyChanged(nameof(Thumb));
                }
                
            }
        }

        #endregion Properties

        public TreeTexInfo()
        {

        }

        public TreeTexInfo(MEDirectories.MEDirectories direcs)
        {
            GameDirecs = direcs;
        }

        public TreeTexInfo(Texture2D tex2D, ThumbnailWriter ThumbWriter, ExportEntry export, Dictionary<string, MemoryStream> TFCs, IList<string> Errors, MEDirectories.MEDirectories direcs, MTRangedObservableCollection<string> ScannedPCCs) : this(direcs)
        {
            TexName = tex2D.texName;
            Format = tex2D.texFormat;
            FullPackage = export.PackageFullName;

            // Hash
            ImageInfo info = tex2D.ImageList[0];
            uint hash = 0;
            //StorageType = info.storageType;

            byte[] imgData = null;
            try
            {
                imgData = tex2D.ExtractImage(info.ImageSize, false, TFCs);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Extraction failure on {TexName}, {FullPackage} in {tex2D.allPccs[0]}. Reason: {e.ToString()}");
                throw;
            }

            if (info.storageType == storage.pccSto)
            {
                if (tex2D.texFormat == ImageEngineFormat.DDS_ATI2_3Dc)
                    hash = ~CRC32.BlockChecksum(imgData, 0, info.UncompressedSize / 2);
                else
                    hash = ~CRC32.BlockChecksum(imgData);
            }
            else
            {
                if (imgData == null)
                    hash = 0;
                else
                {
                    if (tex2D.texFormat == ImageEngineFormat.DDS_ATI2_3Dc)
                        hash = ~CRC32.BlockChecksum(imgData, 0, info.UncompressedSize / 2);
                    else
                        hash = ~CRC32.BlockChecksum(imgData);
                }
            }

            tex2D.Hash = hash;
            Hash = hash;

            // Don't generate thumbnail till necessary i.e. not a duplicate texture - This is done after the check in the TreeDB
            GenerateThumbnail = new Action(() => CreateThumbnail(ToolsetTextureEngine.AddDDSHeader(imgData, info, tex2D.texFormat), tex2D, ThumbWriter, info, TFCs, Errors));

            for (int i = 0; i < tex2D.allPccs.Count; i++)
            {
                int scannedIndex = ScannedPCCs.IndexOf(tex2D.allPccs[i]);
                if (scannedIndex == -1)
                    throw new InvalidOperationException("PCC must be in ScannedPCCs.");

                PCCs.Add(new PCCEntry(tex2D.allPccs[i], tex2D.expIDs[i], GameDirecs, scannedIndex));
            }

            // KFreon: ME2 only?
            if (export.PackageFullName == "Base Package")
                FullPackage = Path.GetFileNameWithoutExtension(PCCs[0].Name).ToUpperInvariant();   // Maybe not right?
            else
                FullPackage = export.PackageFullName.ToUpperInvariant();


            // Fill other details
            PopulateDetails(tex2D);
        }

        void CreateThumbnail(byte[] imgData, Texture2D tex2D, ThumbnailWriter ThumbWriter, ImageInfo info, Dictionary<string, MemoryStream> TFCs, IList<string> Errors)
        {
            byte[] thumbImageData = imgData;

            // Try to get a smaller mipmap to use so don't need to resize.
            var thumbInfo = tex2D.ImageList.Where(img => img.ImageSize.Width <= ToolsetTextureEngine.ThumbnailSize && img.ImageSize.Height <= ToolsetTextureEngine.ThumbnailSize).FirstOrDefault();
            if (thumbInfo.ImageSize != null) // i.e.image size doesn't exist.
                thumbImageData = tex2D.ExtractImage(thumbInfo.ImageSize, true, TFCs);

            using (MemoryStream ms = new MemoryStream(thumbImageData, 0, thumbImageData.Length, false, true))
            {
                int width = info.ImageSize.Width;
                int height = info.ImageSize.Height;

                try
                {
                    MemoryStream thumbStream = ToolsetTextureEngine.GenerateThumbnailToStream(ms, ToolsetTextureEngine.ThumbnailSize);
                    //thumbStream = ToolsetTextureEngine.OverlayAndPickDetailed(thumbStream);
                    if (thumbStream != null)
                        Thumb = ThumbWriter.Add(thumbStream);
                }
                catch(Exception e)
                {
                    Debug.WriteLine($"Failed to create thumbnail for {tex2D.texName} in {tex2D.allPccs[0]}. Reason: {e.ToString()}.");
                    Errors.Add(e.ToString());
                }
                finally
                {
                    tex2D.Dispose();
                }
            }
        }

        public void ReorderME2Files()
        {
            if (GameVersion == 2 && (!String.IsNullOrEmpty(ChangedAssociatedTexture.arcName) && ChangedAssociatedTexture.arcName != "None"))
            {
                for (int i = 0; i < PCCs.Count; i++)
                {
                    using (PCCObject pcc = new PCCObject(PCCs[i].Name, GameVersion))
                    {
                        using (Texture2D tex = new Texture2D(pcc, PCCs[i].ExpID, GameDirecs))
                        {
                            string arc = tex.arcName;
                            if (i == 0 && (arc != "None" && !String.IsNullOrEmpty(arc)))
                                break;
                            else if (arc != "None" && !String.IsNullOrEmpty(arc))
                            {
                                PCCEntry file = PCCs.Pop(i);  // Removes and returns
                                PCCs.Insert(0, file);
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
            for (int i = 0; i < tex.PCCs.Count; i++)
            {
                if (PCCs.Contains(tex.PCCs[i]))
                {
                    bool duplicateDetected = false;
                    for (int j = 0; j < PCCs.Count; j++)
                    {
                        // To be a "duplicate", both pcc and expid must be the same.
                        if (PCCs[j] == tex.PCCs[i])
                        {
                            duplicateDetected = true;
                            break;
                        }
                    }

                    // Don't add if already existing
                    if (duplicateDetected)
                        continue;
                }

                PCCs.Add(tex.PCCs[i]);
            }

            if (Hash == 0)
                Hash = tex.Hash;
        }

        public bool Equals(TreeTexInfo tex)
        {
            return TexName == tex.TexName && Hash != 0 && Hash == tex.Hash;
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
            if (HasChanged)
                PopulateDetails(ChangedAssociatedTexture);
            else
                using (PCCObject pcc = new PCCObject(PCCs[0].Name, GameVersion))
                    using (Texture2D tex2D = new Texture2D(pcc, PCCs[0].ExpID, GameDirecs))
                        PopulateDetails(tex2D);   
        }

        void PopulateDetails(Texture2D tex2D)
        {
            ImageInfo maxImg = tex2D.ImageList.Max();
            Width = (int)maxImg.ImageSize.Width;
            Height = (int)maxImg.ImageSize.Height;
            TextureCache = tex2D?.FullArcPath?.Remove(0, GameDirecs.BasePathLength) ?? "PCC Stored";
            LODGroup = tex2D.LODGroup ?? "None (Uses World)";
            Mips = tex2D.ImageList.Count;
            StorageType = tex2D.ImageList[0].storageType;
        }

        internal void SetChangedThumb(MemoryStream stream)
        {
            Thumb.StreamThumb = stream;
            OnPropertyChanged(nameof(Thumb));
        }
    }
}
