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

namespace WPF_ME3Explorer.Textures
{
    public class TreeTexInfo : AbstractTexInfo, IEquatable<TreeTexInfo>, IComparable
    {
        public List<Texture2D> Textures = new List<Texture2D>();
        public string FullPackage = null;
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

        public string TextureCache
        {
            get
            {
                // TODO
                return null;
            }
        }

        public string LODGroup
        {
            get
            {
                // TODO
                return null;
            }
        }

        public override int Height
        {
            get
            {
                // TODO
                return 0;
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Width
        {
            get
            {
                // TODO
                return 0;
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Mips
        {
            get
            {
                // TODO
                return 0;
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public TreeTexInfo() : base()
        {

        }

        public TreeTexInfo(Texture2D tex2D, ThumbnailWriter ThumbWriter, ExportEntry export) : this()
        {
            TexName = tex2D.texName;
            Format = Misc.ConvertFormat(tex2D.texFormat);
            GameVersion = tex2D.GameVersion;

            // Hash
            ImageInfo info = tex2D.ImageList[0];
            uint hash = 0;
            CRC32 crcgen = new CRC32();
            StorageType = info.storageType;

            byte[] imgData = tex2D.ExtractImage(info.ImageSize, ThumbWriter.GameDirecs.PathBIOGame);

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

            // Thumbnail
            using (MemoryStream ms = new MemoryStream(imgData))
            {
                uint width = info.ImageSize.Width;
                uint height = info.ImageSize.Height;

                MemoryStream thumbStream = ImageEngine.GenerateThumbnailToStream(ms, width > height ? 128 : 0, width > height ? 0 : 128);
                if (thumbStream != null)
                    Thumb = ThumbWriter.Add(thumbStream);                    
            }

            PCCS.AddRange(tex2D.allPccs);
            ExpIDs.AddRange(tex2D.expIDs);

            // KFreon: ME2 only?
            if (export.PackageFullName == "Base Package")
                FullPackage = Path.GetFileNameWithoutExtension(PCCS[0]).ToUpperInvariant();   // Maybe not right?
            else
                FullPackage = export.PackageFullName.ToUpperInvariant();
        }

        public void ReorderME2Files(string pathBIOGame)
        {
            if (GameVersion == 2 && (!String.IsNullOrEmpty(Textures[0].arcName) && Textures[0].arcName != "None"))
            {
                for (int i = 0; i < PCCS.Count; i++)
                {
                    using (PCCObject pcc = new PCCObject(PCCS[i], GameVersion))
                    {
                        using (Texture2D tex = new Texture2D(pcc, ExpIDs[i], pathBIOGame, GameVersion))
                        {
                            string arc = tex.arcName;
                            if (i == 0 && (arc != "None" && !String.IsNullOrEmpty(arc)))
                                break;
                            else if (arc != "None" && !String.IsNullOrEmpty(arc))
                            {
                                string file = PCCS.Pop(i);  // Removes and returns
                                int expid = ExpIDs.Pop(i);
                                PCCS.Insert(0, file);
                                ExpIDs.Insert(0, expid);
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
                        if (PCCS[j] == tex.PCCS[i] && ExpIDs[j] == tex.ExpIDs[i])
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
                ExpIDs.Add(tex.ExpIDs[i]);
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
            return Equals((TreeTexInfo)obj);
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
    }
}
