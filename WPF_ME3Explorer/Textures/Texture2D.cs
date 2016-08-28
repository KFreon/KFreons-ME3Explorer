using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AmaroK86.MassEffect3.ZlibBlock;
using CSharpImageLibrary;
using UsefulThings;
using WPF_ME3Explorer.PCCObjectsAndBits;

namespace WPF_ME3Explorer.Textures
{
    public class Texture2D : IDisposable, IEquatable<Texture2D>
    {
        public enum storage
        {
            ME3arcCpr = 0x3, // archive compressed
            arcCpr = 0x11, // archive compressed
            arcUnc = 0x1, // archive uncompressed (DLC)
            pccSto = 0x0, // pcc local storage
            empty = 0x21,  // unused image (void pointer sorta)
            pccCpr = 0x10 // PCC Compressed, ME1 Only
        }

        public struct ImageInfo : IComparable
        {
            public storage storageType;
            public int CompressedSize { get; set; }
            public ImageSize ImageSize { get; set; }
            public int Offset { get; set; }
            public int GameVersion { get; set; }
            public int UncompressedSize { get; set; }

            public int CompareTo(object obj)
            {
                if (obj == null)
                    throw new ArgumentNullException();

                return ImageSize.CompareTo(((ImageInfo)obj).ImageSize);
            }
        }

        public string texName { get; set; }
        public string arcName { get; set; }
        public string FullArcPath { get; set; }
        public string LODGroup { get; set; }
        public uint Hash { get; set; }
        public byte[] headerData;
        public byte[] imageData;
        private uint dataOffset = 0;
        private uint numMipMaps;
        public Dictionary<string, SaltPropertyReader.Property> properties;

        public int exportOffset;
        private byte[] footerData;
        public int UnpackNum;
        private int ArcDataSize;
        public String Class;
        public string Compression;
        public int GameVersion = 0;
        public string ME1_PackageFullName = null;
        public List<ImageInfo> ImageList { get; set; } // showable image list
        public ImageEngineFormat texFormat { get; set; }
        public uint pccOffset { get; set; }
        public bool hasChanged { get; set; }
        public List<string> allPccs { get; set; }
        public List<int> expIDs { get; set; }
        public int pccExpIdx { get; set; }

        public static List<string> ME3TFCs = new List<string>();


        /// <summary>
        /// Creates a blank Texture2D.
        /// </summary>
        public Texture2D()
        {
            allPccs = new List<String>();
            expIDs = new List<int>();
            hasChanged = false;
        }


        /// <summary>
        /// Creates a Texture2D based on given information.
        /// </summary>
        /// <param name="name">Name of texture.</param>
        /// <param name="pccs">PCC's containing this texture.</param>
        /// <param name="ExpIDs">Export ID's of texture in given PCC's.</param>
        /// <param name="hash">Hash of texture.</param>
        /// <param name="gameVersion">Version of Mass Effect texture is from.</param>
        public Texture2D(string name, List<string> pccs, List<int> ExpIDs, uint hash, int gameVersion)  // Not calling base constructor to avoid double assigning expIDs and allPccs.
        {
            texName = name;
            hasChanged = false;

            List<string> temppccs = new List<string>(pccs);
            List<int> tempexp = new List<int>(ExpIDs);

            allPccs = temppccs;
            expIDs = tempexp;
            Hash = hash;
            ImageList = new List<ImageInfo>();
            GameVersion = gameVersion;
        }


        /// <summary>
        /// Creates Texture2D based on PCCObject.
        /// </summary>
        /// <param name="pccObj">PCCObject containing texture.</param>
        /// <param name="texIdx">Export ID of texture in pcc.</param>
        /// <param name="gameVersion">Version of Mass Effect texture is from.</param>
        /// <param name="hash">Hash of texture.</param>
        public Texture2D(PCCObject pccObj, int texIdx, int gameVersion, uint hash = 0) : this()
        {
            GameVersion = gameVersion;
            allPccs.Add(pccObj.pccFileName);
            expIDs.Add(texIdx);
            Hash = hash;

            ExportEntry expEntry = pccObj.Exports[texIdx];
            if (pccObj.IsExport(texIdx) && PCCObject.ValidTexClass(expEntry.ClassName))
            {
                ME1_PackageFullName = expEntry.PackageFullName;
                Class = expEntry.ClassName;
                properties = new Dictionary<string, SaltPropertyReader.Property>();
                //byte[] rawData = (byte[])expEntry.Data.Clone();
                byte[] rawData = expEntry.Data;
                int propertiesOffset = SaltPropertyReader.detectStart(pccObj, rawData);
                headerData = new byte[propertiesOffset];
                Buffer.BlockCopy(rawData, 0, headerData, 0, propertiesOffset);
                pccOffset = (uint)expEntry.DataOffset;
                List<SaltPropertyReader.Property> tempProperties = SaltPropertyReader.getPropList(pccObj, rawData);
                texName = expEntry.ObjectName;
                for (int i = 0; i < tempProperties.Count; i++)
                {
                    SaltPropertyReader.Property property = tempProperties[i];
                    if (property.Name == "UnpackMin")
                        UnpackNum++;

                    if (!properties.ContainsKey(property.Name))
                        properties.Add(property.Name, property);

                    switch (property.Name)
                    {
                        case "Format":
                            texFormat = Textures.Misc.ParseFormat(pccObj.Names[property.Value.IntValue].Substring(3));
                            break;
                        case "TextureFileCacheName": arcName = property.Value.NameValue.Name; break;
                        case "LODGroup": LODGroup = property.Value.NameValue.Name; break;
                        case "CompressionSettings": Compression = property.Value.StringValue; break;
                        case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                    }
                }
                if (GameVersion == 3 && !String.IsNullOrEmpty(arcName))
                    FullArcPath = GetTexArchive();

                // if "None" property isn't found throws an exception
                if (dataOffset == 0)
                    throw new Exception("\"None\" property not found");
                else
                {
                    imageData = new byte[rawData.Length - dataOffset];
                    Buffer.BlockCopy(rawData, (int)dataOffset, imageData, 0, (int)(rawData.Length - dataOffset));
                }
            }
            else
                throw new Exception("Texture2D " + texIdx + " not found");

            pccExpIdx = texIdx;
            MemoryStream dataStream = new MemoryStream(imageData);  // FG: we will move forward with the memorystream (we are reading an export entry for a texture object data inside the pcc)
            numMipMaps = dataStream.ReadUInt32();                 // FG: 1st int32 (4 bytes / 32bits) is number of mipmaps
                                                                    // KFREON: Might need to read a u32 before this one for ME1 at least
            uint count = numMipMaps;

            ImageList = new List<ImageInfo>();
            ArcDataSize = 0;
            while (dataStream.Position < dataStream.Length && count > 0)
            {
                ImageInfo imgInfo = new ImageInfo();                            // FG: store properties in ImageInfo struct (code at top)
                imgInfo.storageType = (storage)dataStream.ReadInt32();       // FG: 2nd int32 storage type (see storage types above in enum_struct)
                imgInfo.UncompressedSize = dataStream.ReadInt32();                    // FG: 3rd int32 uncompressed texture size
                imgInfo.CompressedSize = dataStream.ReadInt32();                    // FG: 4th int32 compressed texture size
                imgInfo.Offset = dataStream.ReadInt32();                     // FG: 5th int32 texture offset
                if (imgInfo.storageType == storage.pccSto)
                {
                    imgInfo.Offset = (int)dataStream.Position; // saving pcc offset as relative to exportdata offset, not absolute
                    dataStream.Seek(imgInfo.UncompressedSize, SeekOrigin.Current);       // FG: if local storage, texture data follows, so advance datastream to after uncompressed_size (pcc storage type only)
                }
                else if (imgInfo.storageType == storage.pccCpr)
                {
                    imgInfo.Offset = (int)dataStream.Position;
                    dataStream.Seek(imgInfo.CompressedSize, SeekOrigin.Current);
                }
                else if (imgInfo.storageType == storage.arcCpr || imgInfo.storageType == storage.arcUnc)
                    ArcDataSize += imgInfo.UncompressedSize;
                imgInfo.ImageSize = new ImageSize(dataStream.ReadUInt32(), dataStream.ReadUInt32());  // FG: 6th & 7th [or nth and (nth + 1) if local] int32 are width x height

                // KFreon: Test - instead of filtering out the null entries later, just don't add them here.
                if (imgInfo.Offset != -1)
                    ImageList.Add(imgInfo);                                                                   // FG: A salty's favorite, add the struct to a list<struct>
                count--;
            }


            // save what remains
            int remainingBytes = (int)(dataStream.Length - dataStream.Position);
            footerData = new byte[remainingBytes];
            dataStream.Read(footerData, 0, footerData.Length);


            dataStream.Dispose();
        }

        public byte[] ExtractImage(ImageSize size, Dictionary<string, MemoryStream> TFCs = null)
        {
            byte[] retval;
            if (ImageList.Exists(img => img.ImageSize == size))
                retval = ExtractImage(ImageList.Find(img => img.ImageSize == size), TFCs);
            else
                throw new FileNotFoundException($"Image with resolution { size.ToString() } not found");
            return retval;
        }

        public byte[] ExtractImage(ImageInfo imgInfo, Dictionary<string, MemoryStream> TFCs = null)
        {
            byte[] imgBuffer = null;

            switch (imgInfo.storageType)
            {
                case storage.pccSto:
                    imgBuffer = new byte[imgInfo.UncompressedSize];
                    Buffer.BlockCopy(imageData, (int)imgInfo.Offset, imgBuffer, 0, imgInfo.UncompressedSize);
                    break;
                case storage.ME3arcCpr:
                case storage.arcUnc:
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive();

                    if (archivePath == null)
                        throw new FileNotFoundException("Texture archive not found!");
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    // Treescanning uses full list of TFCs read in, switch based on whether scanning or just getting texture.
                    Stream archiveStream = null;
                    if (TFCs != null)
                        archiveStream = TFCs[archivePath];
                    else
                        archiveStream = File.OpenRead(archivePath);

                    lock (archiveStream)
                    {
                        archiveStream.Seek(imgInfo.Offset, SeekOrigin.Begin);
                        if (imgInfo.storageType == storage.ME3arcCpr)
                            imgBuffer = ZBlock.Decompress(archiveStream, imgInfo.CompressedSize);
                        else
                        {
                            imgBuffer = new byte[imgInfo.UncompressedSize];
                            archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
                        }
                    }

                    // Can dispose of stream if not treescanning
                    if (TFCs == null)
                        archiveStream.Dispose();
                    break;
                case storage.arcCpr:  // ME1, ME2?
                    archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = FindFile();

                    if (archivePath == null)
                        throw new FileNotFoundException("Texture archive not found!");
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    PCCObject temp = new PCCObject(archivePath, GameVersion);
                    for (int i = 0; i < temp.Exports.Count; i++)
                    {
                        if (String.Compare(texName, temp.Exports[i].ObjectName, true) == 0 && temp.Exports[i].ValidTextureClass())
                        {
                            Texture2D temptex = new Texture2D(temp, i, 1);
                            imgBuffer = temptex.ExtractImage(imgInfo.ImageSize);
                        }
                    }
                    break;
                case storage.pccCpr:
                    using (MemoryStream ms = new MemoryStream(imageData))
                    {
                        SaltLZOHelper lzohelp = new SaltLZOHelper();
                        imgBuffer = lzohelp.DecompressTex(ms, imgInfo.Offset, imgInfo.UncompressedSize, imgInfo.CompressedSize);
                    }
                    break;
                default:
                    throw new FormatException("Unsupported texture storage type");
            }

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms, Encoding.Default, true))
                {
                    var header = CSharpImageLibrary.DDSGeneral.Build_DDS_Header(1, (int)imgInfo.ImageSize.Height, (int)imgInfo.ImageSize.Width, texFormat);
                    CSharpImageLibrary.DDSGeneral.Write_DDS_Header(header, bw);
                    bw.Write(imgBuffer);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Extracts texture to file.
        /// </summary>
        /// <param name="fileName">Filename to save extracted image as.</param>
        /// <param name="info">Information of texture to be extracted.</param>
        public void ExtractImage(string fileName, ImageInfo info)
        {
            byte[] data = ExtractImage(info);
            File.WriteAllBytes(fileName, data);
        }

        /// <summary>
        /// Extracts texture to file
        /// </summary>
        /// <param name="fileName">Filename to save extracted image as.</param>
        /// <param name="size">Size of image in texture list to extract.</param>
        public void ExtractImage(string fileName, ImageSize size)
        {
            if (ImageList.Exists(img => img.ImageSize == size))
                ExtractImage(fileName, ImageList.Find(img => img.ImageSize == size));
        }

        /// <summary>
        /// Extracts largest image to byte[].
        /// </summary>
        /// <returns>Byte[] containing largest image.</returns>
        public byte[] ExtractMaxImage()
        {
            // select max image size
            ImageSize maxImgSize = ImageList.Max(image => image.ImageSize);
            // extracting max image
            return ExtractImage(ImageList.Find(img => img.ImageSize == maxImgSize));
        }

        /// <summary>
        /// Extracts largest image to file.
        /// </summary>
        /// <param name="filename">Filename to save extracted image as.</param>
        public void ExtractMaxImage(string filename)
        {
            byte[] data = ExtractMaxImage();
            File.WriteAllBytes(filename, data);
        }

        int GetUncompressedSize(ImageEngineImage img)
        {
            return GetUncompressedSize(img.Width, img.Height, img.Format.SurfaceFormat, img.header);
        }

        int GetUncompressedSize(int width, int height, ImageEngineFormat format, DDSGeneral.DDS_HEADER header)
        {
            float BPP = 0;
            switch (format)
            {
                case ImageEngineFormat.DDS_DXT1: BPP = 0.5F; break;
                case ImageEngineFormat.DDS_DXT5:
                case ImageEngineFormat.DDS_ATI2_3Dc: BPP = 1F; break;
                case ImageEngineFormat.DDS_V8U8: BPP = 2F; break;
                default: BPP = (float)header.ddspf.dwRGBBitCount / 8; break;
            }

            if (width < 4)
                width = 4;
            if (height < 4)
                height = 4;

            return (int)(width * height * BPP);
        }

        void SingleImageUpscale(ImageEngineImage imgFile)
        {
            ImageSize biggerImageSizeOnList = ImageList.Max(image => image.ImageSize);
            // check if replacing image is supported
            ImageEngineFormat imageFileFormat = imgFile.Format.SurfaceFormat;


            //NEW Check for correct image format
            if (texFormat != imageFileFormat)
                throw new FormatException($"Different image format, original is {texFormat}, new is  {imgFile.Format.SurfaceFormat}");

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = ImageList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
            newImgInfo.ImageSize = new ImageSize((uint)imgFile.Width, (uint)imgFile.Height);
            newImgInfo.UncompressedSize = GetUncompressedSize(imgFile.Width, imgFile.Height, imgFile.Format.SurfaceFormat, imgFile.header);
            newImgInfo.CompressedSize = 0x00; // not yet filled
            newImgInfo.Offset = 0x00; // not yet filled
            ImageList.RemoveAt(0);  // Remove old single image and add new one
            ImageList.Add(newImgInfo);
            //now I let believe the program that I'm doing an image replace, saving lot of code ;)
            ReplaceImage(newImgInfo.ImageSize, imgFile);

            // update Sizes
            int propVal = (int)newImgInfo.ImageSize.Width;
            properties["SizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteInt32(propVal);
                properties["SizeX"].raw = rawStream.ToArray();
            }
            properties["SizeY"].Value.IntValue = (int)newImgInfo.ImageSize.Height;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteInt32(propVal);
                properties["SizeY"].raw = rawStream.ToArray();
            }
            properties["OriginalSizeX"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteInt32(propVal);
                properties["OriginalSizeX"].raw = rawStream.ToArray();
            }
            properties["OriginalSizeY"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteInt32(propVal);
                properties["OriginalSizeY"].raw = rawStream.ToArray();
            }
        }

	    void ReplaceImage(ImageSize imgSize, ImageEngineImage imgFile)
        {
            if (!ImageList.Exists(img => img.ImageSize == imgSize))
                throw new FileNotFoundException($"Image with resolution {imgSize} isn't found");

            int imageIdx = ImageList.FindIndex(img => img.ImageSize == imgSize);
            ImageInfo imgInfo = ImageList[imageIdx];

            ImageEngineFormat imgFileFormat = imgFile.Format.SurfaceFormat;

            // check if images have same format type
            if (texFormat != imgFileFormat)  // Had restriction on G8 L8 for some reason. Don't know why.
                throw new FormatException($"Different image format, original is {texFormat}, new is {imgFile.Format.SurfaceFormat}");

            byte[] imgBuffer;

            // if the image is empty then recover the archive compression from the image list
            if (imgInfo.storageType == storage.empty)
            {
                imgInfo.storageType = ImageList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
                imgInfo.UncompressedSize = GetUncompressedSize(imgFile);
                imgInfo.CompressedSize = imgInfo.UncompressedSize; // Don't know why...
            }

            switch (imgInfo.storageType)
            {
                case storage.arcCpr:
                case storage.arcUnc:
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive();
                    if (archivePath == null)
                        throw new FileNotFoundException("Teture archive not found!");
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    imgBuffer = imgFile.Save(imgFile.Format.SurfaceFormat, MipHandling.KeepExisting);

                    if (imgBuffer.Length != imgInfo.UncompressedSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.UncompressedSize + ", new is " + imgBuffer.Length);

                    // archiveDir is just BIOGame path
                    string archiveDir = null;
                    switch (GameVersion)
                    {
                        case 1:
                            archiveDir = MEDirectories.MEDirectories.ME1BIOGame;
                            break;
                        case 2:
                            archiveDir = MEDirectories.MEDirectories.ME2BIOGame;
                            break;
                        case 3:
                            archiveDir = MEDirectories.MEDirectories.ME3BIOGame;
                            break;
                    }

                    if (!arcName.ToLower().Contains(Path.GetFileNameWithoutExtension(MEDirectories.MEDirectories.CachePath.ToLower())))  // CachePath is usually CustTextures, but arcName can be CustTextures#, so check for substring
                    {
                        ChooseNewCache(archiveDir, imgBuffer.Length);
                        archivePath = FullArcPath;
                    }
                    else
                    {
                        FileInfo arc = new FileInfo(archivePath);
                        if (arc.Length + imgBuffer.Length >= 0x80000000)
                        {
                            ChooseNewCache(archiveDir, imgBuffer.Length);
                            archivePath = FullArcPath;
                        }
                    }

                    using (FileStream archiveStream = new FileStream(archivePath, FileMode.Append, FileAccess.Write))
                    {
                        int newOffset = (int)archiveStream.Position;

                        if (imgInfo.storageType == storage.arcCpr)
                        {
                            imgBuffer = ZBlock.Compress(imgBuffer);
                            imgInfo.CompressedSize = imgBuffer.Length;
                        }
                        archiveStream.Write(imgBuffer, 0, imgBuffer.Length);

                        imgInfo.Offset = newOffset;
                    }
                    break;
                case storage.pccSto:
                    // Get image data and remove header.
                    byte[] imgData = imgFile.Save(imgFile.Format.SurfaceFormat, MipHandling.KeepExisting);
                    imgBuffer = new byte[imgData.Length - 128];
                    Array.Copy(imgData, 128, imgBuffer, 0, imgBuffer.Length);

                    if (imgBuffer.Length != imgInfo.UncompressedSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.UncompressedSize + ", new is " + imgBuffer.Length);
                    using (MemoryStream dataStream = new MemoryStream())
                    {
                        dataStream.WriteBytes(imageData);
                        if (imgBuffer.Length <= imgInfo.UncompressedSize && imgInfo.Offset > 0)
                            dataStream.Seek(imgInfo.Offset, SeekOrigin.Begin);
                        else
                            imgInfo.Offset = (int)dataStream.Position;
                        dataStream.WriteBytes(imgBuffer);
                        imgInfo.CompressedSize = imgBuffer.Length;
                        imgInfo.UncompressedSize = imgBuffer.Length;
                        imageData = dataStream.ToArray();
                    }
                    break;
                case storage.pccCpr:
                    using (MemoryStream dataStream = new MemoryStream())
                    {
                        dataStream.WriteBytes(imageData);
                        SaltLZOHelper lzohelper = new SaltLZOHelper();
                        imgBuffer = lzohelper.CompressTex(imgFile.Save(imgFile.Format.SurfaceFormat, MipHandling.KeepExisting));
                        if (imgBuffer.Length <= imgInfo.CompressedSize && imgInfo.Offset > 0)
                            dataStream.Seek(imgInfo.Offset, SeekOrigin.Begin);
                        else
                            imgInfo.Offset = (int)dataStream.Position;
                        dataStream.WriteBytes(imgBuffer);
                        imgInfo.CompressedSize = imgBuffer.Length;
                        imgInfo.UncompressedSize = GetUncompressedSize(imgFile);
                        imageData = dataStream.ToArray();
                    }
                    break;
            }

            ImageList[imageIdx] = imgInfo;
        }

        void AddBiggerImage(ImageEngineImage imgFile)
        {
            ImageSize biggerImageSizeOnList = ImageList.Max(image => image.ImageSize);
            // check if replacing image is supported
            ImageEngineFormat imgFileFormat = imgFile.Format.SurfaceFormat;

            //NEW Check for correct image format
            if (texFormat != imgFileFormat)
                throw new FormatException($"Different image format, original is {texFormat}, new is {imgFile.Format.SurfaceFormat}");

            // check if image to add is valid
            if (biggerImageSizeOnList.Width * 2 != imgFile.Width || biggerImageSizeOnList.Height * 2 != imgFile.Height)
                throw new FormatException($"image size {imgFile.Width}x{imgFile.Height} isn't valid, must be " + new ImageSize(biggerImageSizeOnList.Width * 2, biggerImageSizeOnList.Height * 2));

            // this check avoids insertion inside textures that have only 1 image stored inside pcc
            if (ImageList.Count <= 1)
                throw new Exception("Unable to add image, texture must have more than one existing image");

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = ImageList.Find(img => img.storageType != storage.empty && img.storageType != storage.pccSto).storageType;
            newImgInfo.ImageSize = new ImageSize((uint)imgFile.Width, (uint)imgFile.Height);
            newImgInfo.UncompressedSize = GetUncompressedSize(imgFile);
            newImgInfo.CompressedSize = 0x00; // not yet filled
            newImgInfo.Offset = 0x00; // not yet filled
            ImageList.Insert(0, newImgInfo); // insert new image on top of the list
                                                  //now I let believe the program that I'm doing an image replace, saving lot of code ;)
            ReplaceImage(newImgInfo.ImageSize, imgFile);

            //updating num of images
            numMipMaps++;

            // update MipTailBaseIdx
            int propVal = properties["MipTailBaseIdx"].Value.IntValue;
            propVal++;
            properties["MipTailBaseIdx"].Value.IntValue = propVal;
            using (MemoryStream rawStream = new MemoryStream(properties["MipTailBaseIdx"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteInt32(propVal);
                properties["MipTailBaseIdx"].raw = rawStream.ToArray();
            }

            // update Sizes

            // Heff: Fixed(?) to account for non-square images
            int propX = (int)newImgInfo.ImageSize.Width;
            int propY = (int)newImgInfo.ImageSize.Height;
            properties["SizeX"].Value.IntValue = propX;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeX"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteInt32(propX);
                properties["SizeX"].raw = rawStream.ToArray();
            }
            properties["SizeY"].Value.IntValue = propY;
            using (MemoryStream rawStream = new MemoryStream(properties["SizeY"].raw))
            {
                rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                rawStream.WriteInt32(propY);
                properties["SizeY"].raw = rawStream.ToArray();
            }
            try
            {
                properties["OriginalSizeX"].Value.IntValue = propX;
                using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeX"].raw))
                {
                    rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                    rawStream.WriteInt32(propX);
                    properties["OriginalSizeX"].raw = rawStream.ToArray();
                }
                properties["OriginalSizeY"].Value.IntValue = propY;
                using (MemoryStream rawStream = new MemoryStream(properties["OriginalSizeY"].raw))
                {
                    rawStream.Seek(rawStream.Length - 4, SeekOrigin.Begin);
                    rawStream.WriteInt32(propY);
                    properties["OriginalSizeY"].raw = rawStream.ToArray();
                }
            }
            catch
            {
                // Some lightmaps don't have these properties. I'm ignoring them cos I'm ignorant. KFreon.
            }
        }

        /// <summary>
        /// Replaces/upscales texture as required.
        /// </summary>
        /// <param name="newImg">Image to change to.</param>
        public void OneImageToRuleThemAll(ImageEngineImage newImg)
        {
            // starts from the smaller image
            ImageEngineFormat mipFormat = newImg.Format.SurfaceFormat;
            for (int i = newImg.NumMipMaps; i >= 0; i--)
            {
                MipMap mip = newImg.MipMaps[i];

                if (mip.Height < 4 || mip.Width < 4)
                    continue;

                //NEW Check for correct format
                if (texFormat != mipFormat)
                    throw new FormatException("Different image format, original is " + texFormat + ", new is " + mipFormat);

                ImageSize mipSize = new ImageSize((uint)mip.Width, (uint)mip.Height);

                // if the image size exists inside the texture2d image list then we have to replace it
                if (ImageList.Exists(img => img.ImageSize == mipSize))
                {
                    // ...but at least for now I can reuse my replaceImage function... ;)
                    ReplaceImage(mipSize, new ImageEngineImage(mip, newImg.Format.SurfaceFormat));
                }
                else // if the image doesn't exists then we have to add it
                {
                    // ...and use my addBiggerImage function! :P
                    AddBiggerImage(new ImageEngineImage(mip, newImg.Format.SurfaceFormat));
                }
            }

            while (ImageList[0].ImageSize.Width > newImg.MipMaps[0].Width)
            {
                ImageList.RemoveAt(0);
                numMipMaps--;
                if (properties.ContainsKey("MipTailBaseIdx"))
                    properties["MipTailBaseIdx"].Value.IntValue--;
            }
            if (properties.ContainsKey("SizeX"))
                properties["SizeX"].Value.IntValue = (int)newImg.MipMaps[0].Width;
            if (properties.ContainsKey("SizeY"))
                properties["SizeY"].Value.IntValue = (int)newImg.MipMaps[0].Height;
        }

        public List<SaltPropertyReader.Property> GetPropertyList()
        {
            List<SaltPropertyReader.Property> propertyList = new List<SaltPropertyReader.Property>();
            foreach (KeyValuePair<string, SaltPropertyReader.Property> kvp in properties)
                propertyList.Add(kvp.Value);
            return propertyList;
        }

        private void CopyImgList(Texture2D inTex, PCCObject pcc)
        {
            imageData = inTex.imageData;
            ImageList = inTex.ImageList;
            numMipMaps = inTex.numMipMaps;

            //Copy Properties
            byte[] buff;
            using (MemoryStream tempMem = new MemoryStream())
            {
                tempMem.WriteBytes(headerData);
                for (int i = 0; i < inTex.properties.Count; i++)
                {
                    SaltPropertyReader.Property prop = inTex.properties.ElementAt(i).Value;

                    if (prop.Name == "UnpackMin")
                    {
                        for (int j = 0; j < inTex.UnpackNum; j++)
                        {
                            tempMem.WriteInt64(pcc.AddName(prop.Name));
                            tempMem.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                            tempMem.WriteInt32(prop.Size);
                            tempMem.WriteInt32(j);
                            tempMem.WriteFloat32(prop.Value.FloatValue);
                        }
                        continue;
                    }

                    tempMem.WriteInt64(pcc.AddName(prop.Name));

                    if (prop.Name == "None")
                        continue;


                    tempMem.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                    tempMem.WriteInt64(prop.Size);

                    switch (prop.TypeVal)
                    {
                        case SaltPropertyReader.Type.FloatProperty:
                            tempMem.WriteFloat32(prop.Value.FloatValue);
                            break;
                        case SaltPropertyReader.Type.IntProperty:
                            tempMem.WriteInt32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.NameProperty:
                            tempMem.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            // Heff: Modified to handle name references.
                            break;
                        case SaltPropertyReader.Type.ByteProperty:
                            tempMem.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            tempMem.WriteInt32(pcc.AddName(prop.Value.String2));
                            byte[] footer = new byte[4];
                            Buffer.BlockCopy(prop.raw, prop.raw.Length - 4, footer, 0, 4);
                            tempMem.WriteBytes(footer);
                            break;
                        case SaltPropertyReader.Type.BoolProperty:
                            byte[] bytes = BitConverter.GetBytes(prop.Value.Boolereno);
                            tempMem.Write(bytes, 0, bytes.Length);
                            break;
                        case SaltPropertyReader.Type.StructProperty:
                            tempMem.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            for (int k = 0; k < prop.Size; k++)
                                tempMem.WriteByte((byte)prop.Value.Array[k].IntValue);
                            break;
                        default:
                            throw new NotImplementedException("Property type: " + prop.TypeVal.ToString() + ", not yet implemented. TELL ME ABOUT THIS!");
                    }
                }
                buff = tempMem.ToArray();
            }

            properties = new Dictionary<string, SaltPropertyReader.Property>();

            List<SaltPropertyReader.Property> tempProperties = SaltPropertyReader.ReadProp(pcc, buff, headerData.Length);
            for (int i = 0; i < tempProperties.Count; i++)
            {
                SaltPropertyReader.Property property = tempProperties[i];
                if (property.Name == "UnpackMin")
                    UnpackNum++;

                if (!properties.ContainsKey(property.Name))
                    properties.Add(property.Name, property);

                switch (property.Name)
                {
                    case "Format":
                        texFormat = Textures.Misc.ParseFormat(pcc.Names[property.Value.IntValue].Substring(3));
                        break;
                    case "TextureFileCacheName": arcName = property.Value.NameValue.Name; break;
                    case "LODGroup": LODGroup = property.Value.NameValue.Name; break;
                    case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                }
            }

            // if "None" property isn't found throws an exception
            if (dataOffset == 0)
                throw new Exception("\"None\" property not found");
        }

        private void ChooseNewCache(string bioPath, int buffLength)
        {
            if (File.Exists(MEDirectories.MEDirectories.CachePath))
            {
                FileInfo cacheInfo = new FileInfo(MEDirectories.MEDirectories.CachePath);
                string cacheName = Path.GetFileNameWithoutExtension(MEDirectories.MEDirectories.CachePath);
                string cacheFileName = Path.GetFileName(MEDirectories.MEDirectories.CachePath);
                string rootFolder = Path.GetDirectoryName(MEDirectories.MEDirectories.CachePath);

                if (!cacheInfo.Exists)
                    MakeCache(cacheInfo.FullName, bioPath);

                MoveCaches(rootFolder, cacheFileName);
                properties["TextureFileCacheName"].Value.StringValue = cacheName;
                arcName = cacheName;
                FullArcPath = cacheInfo.FullName;
            }
            else
            {
                int i = 0;
                string CustCache = "CustTextures";
                while (i < 10)
                {
                    FileInfo cacheInfo;
                    List<string> parts = new List<string>(bioPath.Split('\\'));
                    parts.Remove("");
                    if (parts[parts.Count - 1] == "CookedPCConsole")
                        cacheInfo = new FileInfo(Path.Combine(bioPath, CustCache + i + ".tfc"));
                    else
                        cacheInfo = new FileInfo(Path.Combine(bioPath, "CookedPCConsole", CustCache + i + ".tfc"));

                    if (!cacheInfo.Exists)
                    {
                        MakeCache(cacheInfo.FullName, bioPath);
                        List<string> parts1 = new List<string>(bioPath.Split('\\'));
                        parts1.Remove("");
                        if (parts1[parts1.Count - 1] == "CookedPCConsole")
                            MoveCaches(bioPath, CustCache + i + ".tfc");
                        else
                            MoveCaches(bioPath + "\\CookedPCConsole", CustCache + i + ".tfc");

                        properties["TextureFileCacheName"].Value.StringValue = CustCache + i;
                        arcName = CustCache + i;
                        FullArcPath = cacheInfo.FullName;
                        return;
                    }
                    else if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)
                    {
                        List<string> parts1 = new List<string>(bioPath.Split('\\'));
                        parts1.Remove("");
                        if (parts1[parts1.Count - 1] == "CookedPCConsole")
                            MoveCaches(bioPath, CustCache + i + ".tfc");
                        else
                            MoveCaches(bioPath + "\\CookedPCConsole", CustCache + i + ".tfc");
                        properties["TextureFileCacheName"].Value.StringValue = CustCache + i;
                        arcName = CustCache + i;
                        FullArcPath = cacheInfo.FullName;
                        return;
                    }
                    i++;
                }
            }
        }

        private void MoveCaches(string cookedPath, string NewCache)
        {
            //Fix the GUID
            using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Open, FileAccess.Read))
            {
                SaltPropertyReader.Property GUIDProp = properties["TFCFileGuid"];

                for (int i = 0; i < 16; i++)
                {
                    SaltPropertyReader.PropertyValue tempVal = GUIDProp.Value.Array[i];
                    tempVal.IntValue = newCache.ReadByte();
                    GUIDProp.Value.Array[i] = tempVal;
                }
            }


            //Move across any existing textures
            using (FileStream oldCache = new FileStream(FullArcPath, FileMode.Open, FileAccess.Read))
            {
                using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Append, FileAccess.Write))
                {
                    for (int i = 0; i < ImageList.Count; i++)
                    {
                        ImageInfo img = ImageList[i];

                        switch (img.storageType)
                        {
                            case storage.arcCpr:
                                byte[] buff = new byte[img.CompressedSize];
                                oldCache.Seek(img.Offset, SeekOrigin.Begin);
                                Buffer.BlockCopy(oldCache.ReadBytes(img.CompressedSize), 0, buff, 0, img.CompressedSize);
                                img.Offset = (int)newCache.Position;
                                newCache.WriteBytes(buff);
                                break;
                            case storage.arcUnc:
                                buff = new byte[img.UncompressedSize];
                                oldCache.Seek(img.Offset, SeekOrigin.Begin);
                                Buffer.BlockCopy(oldCache.ReadBytes(img.CompressedSize), 0, buff, 0, img.CompressedSize);
                                img.Offset = (int)newCache.Position;
                                newCache.WriteBytes(buff);
                                break;
                            case storage.pccSto:
                                break;
                            case storage.empty:
                                break;
                            default:
                                throw new NotImplementedException("Storage type not supported yet");
                        }
                        ImageList[i] = img;
                    }
                }
            }
        }

        private String GetTexArchive()
        {
            // Currently ME1 and 2 never get here. They use FindFile.
            if (!String.IsNullOrEmpty(arcName))
            {
                int dotInd = arcName.IndexOf('.');
                if (dotInd != -1)
                    arcName = arcName.Substring(0, dotInd);
            }


            if (ME3TFCs.Count == 0)
                ME3TFCs = MEDirectories.MEDirectories.ME3Files.Where(file => file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList();

            string arc =  ME3TFCs.Where(file => String.Compare(Path.GetFileNameWithoutExtension(file), arcName, true) == 0).FirstOrDefault();
            if (arc == null)
                throw new FileNotFoundException($"Texture archive called {arcName} not found in BIOGame.");
            else
            {
                FullArcPath = Path.GetFullPath(arc);
                return FullArcPath;
            }
        }

        /// <summary>
        /// This function will first guess and then do a thorough search to find the original location of the texture
        /// </summary>
        private string FindFile()  // TODO: Merge with GetTexArchive?
        {
            // KFreon:  All files should have been added elsewhere rather than searched for here
            if (allPccs == null)
                allPccs = MEDirectories.MEDirectories.ME1Files;

            string package = ME1_PackageFullName.Split('.')[0];
            for (int i = 0; i < allPccs.Count; i++)
            {
                string[] parts = allPccs[i].Split('\\');
                string tempFile = parts.Last().Split('.')[0];
                if (String.Compare(package, tempFile, true) == 0)
                    return allPccs[i];
            }

            for (int i = 0; i < allPccs.Count; i++)
            {
                PCCObject temp = new PCCObject(allPccs[i], GameVersion);
                for (int j = 0; j < temp.Exports.Count; j++)
                {
                    ExportEntry exp = temp.Exports[j];
                    if (String.Compare(texName, exp.ObjectName, true) == 0 && exp.ClassName == "ME1Texture2D")
                    {
                        Texture2D temptex = new Texture2D(temp, j, 1);
                        if (temptex.ImageList[0].storageType == storage.pccCpr || temptex.ImageList[0].storageType == storage.pccSto)
                        {
                            return allPccs[i];
                        }
                    }
                }
            }
            return null;
        }

        private void MakeCache(String filename, String biopath)
        {
            Random r = new Random();

            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < 4; i++)
                    fs.WriteInt32(r.Next());
            }
        }

        public void LowResFix(int MipMapsToKeep = 1)
        {
            while (ImageList[0].storageType == storage.empty)
            {
                numMipMaps--;
                ImageList.RemoveAt(0);
            }

            while (ImageList.Count > MipMapsToKeep)
            {
                numMipMaps--;
                ImageList.Remove(ImageList.Last());
            }

            numMipMaps = (uint)MipMapsToKeep;
            if (properties.ContainsKey("MipTailBaseIdx"))
                properties["MipTailBaseIdx"].Value.IntValue = 0;
            if (properties.ContainsKey("SizeX"))
                properties["SizeX"].Value.IntValue = (int)ImageList[0].ImageSize.Width;
            if (properties.ContainsKey("SizeY"))
                properties["SizeY"].Value.IntValue = (int)ImageList[0].ImageSize.Height;
        }

        public BitmapSource GetImage()
        {
            try
            {
                byte[] imgdata = ExtractMaxImage();
                if (imgdata == null)
                    return null;
                using (ImageEngineImage img = new ImageEngineImage(imgdata))
                    return img.GetWPFBitmap();
            }
            catch { }
            return null;
        }

        public void DumpTexture(string filename)
        {
            using (StreamWriter sw = new StreamWriter(filename))
            {
                try
                {
                    sw.WriteLine("allPccs: " + allPccs.Count);
                    foreach (string file in allPccs)
                        sw.WriteLine(file);
                }
                catch { }


                sw.WriteLine("AllPccs: " + allPccs.Count);
                foreach (string pcc in allPccs)
                    sw.WriteLine(pcc);

                sw.WriteLine(ArcDataSize);
                sw.WriteLine(arcName);
                sw.WriteLine(Class);
                sw.WriteLine(dataOffset);
                sw.WriteLine("ExpIDs: " + expIDs.Count);
                for (int i = 0; i < expIDs.Count; i++)
                    sw.WriteLine(expIDs);
                sw.WriteLine(exportOffset);

                sw.WriteLine(footerData);
                sw.WriteLine(FullArcPath);
                sw.WriteLine(headerData);
                sw.WriteLine(imageData);

                //this.imgList;
                sw.WriteLine(LODGroup);
                sw.WriteLine(numMipMaps);
                sw.WriteLine(pccExpIdx);
                sw.WriteLine(pccOffset);
                sw.WriteLine(texFormat);
                sw.WriteLine(texName);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                this.allPccs = null;
                this.allPccs = null;
                this.expIDs = null;
                this.footerData = null;
                this.headerData = null;
                this.imageData = null;
                this.ImageList = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~Texture2D()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }


        #region IEquatable 
        public bool Equals(Texture2D other)
        {
            if (other == null)
                return false;

            if (texName == other.texName && Hash == other.Hash && texFormat == other.texFormat)
            {
                /*  Maybe these as well?
                 *  this.expIDs;
                    this.allPccs;
                    this.exportOffset;
                    this.FullArcPath;
                    this.pccOffset;
                    this.pccExpIdx;*/
                return true;
            }
            else
                return false;

            
        }

        public override bool Equals(object obj)
        {
            return Equals((Texture2D)obj);
        }

        public override int GetHashCode()
        {
            return texName.GetHashCode() ^ (int)texFormat ^ (int)Hash;
        }
        #endregion IEquatable

        #endregion
    }
}
