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
using System.Diagnostics;

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
        public int GameVersion
        {
            get
            {
                return GameDirecs.GameVersion;
            }
        }
        public MEDirectories.MEDirectories GameDirecs = null;
        public string ME1_PackageFullName = null;
        public List<ImageInfo> ImageList { get; set; } // showable image list
        public ImageEngineFormat texFormat { get; set; }
        public uint pccOffset { get; set; }
        public List<string> allPccs { get; set; }
        public List<int> expIDs { get; set; }
        public int pccExpIdx { get; set; }

        public static IEnumerable<string> ME3TFCs = null;
        public static IEnumerable<string> ME2TFCs = null;
        public static Dictionary<string, string> ME1_Filenames_Paths = new Dictionary<string, string>();


        /// <summary>
        /// Creates a blank Texture2D.
        /// </summary>
        public Texture2D()
        {
            allPccs = new List<String>();
            expIDs = new List<int>();
        }

        static Texture2D()
        {
            List<string> GameFiles = MEDirectories.MEDirectories.ME1Files;

            if (ME1_Filenames_Paths.Keys.Count == 0)
            {
                for (int i = 0; i < GameFiles.Count; i++)
                {
                    string tempFile = Path.GetFileNameWithoutExtension(GameFiles[i]).ToUpperInvariant();
                    ME1_Filenames_Paths.Add(tempFile, GameFiles[i]);
                }
            }

            // Doesn't matter that it does ME2 and 3, it's all deferred evaluation, so it only assigns a "job" to each one and doesn't do anything with it unless asked to.
            if (ME3TFCs == null)
                ME3TFCs = MEDirectories.MEDirectories.ME3Files?.Where(file => file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase));

            if (ME2TFCs == null)
                ME2TFCs = MEDirectories.MEDirectories.ME2Files?.Where(file => file.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Creates a Texture2D based on given information.
        /// </summary>
        /// <param name="name">Name of texture.</param>
        /// <param name="pccs">PCC's containing this texture.</param>
        /// <param name="ExpIDs">Export ID's of texture in given PCC's.</param>
        /// <param name="hash">Hash of texture.</param>
        /// <param name="direcs">Mass Effect Directory Information.</param>
        public Texture2D(string name, List<string> pccs, List<int> ExpIDs, uint hash, MEDirectories.MEDirectories direcs)  // Not calling base constructor to avoid double assigning expIDs and allPccs.
        {
            texName = name;

            List<string> temppccs = new List<string>(pccs);
            List<int> tempexp = new List<int>(ExpIDs);

            allPccs = temppccs;
            expIDs = tempexp;
            Hash = hash;
            ImageList = new List<ImageInfo>();
            GameDirecs = direcs;
        }


        /// <summary>
        /// Creates Texture2D based on PCCObject.
        /// </summary>
        /// <param name="pccObj">PCCObject containing texture.</param>
        /// <param name="texIdx">Export ID of texture in pcc.</param>
        /// <param name="direcs">Mass Effect Directory information.</param>
        /// <param name="hash">Hash of texture.</param>
        public Texture2D(PCCObject pccObj, int texIdx, MEDirectories.MEDirectories direcs, uint hash = 0) : this()
        {
            GameDirecs = direcs;
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
                            texFormat = ToolsetTextureEngine.ParseFormat(GameVersion == 3 ? pccObj.Names[property.Value.IntValue].Substring(3) : property.Value.StringValue);
                            break;
                        case "TextureFileCacheName": arcName = property.Value.NameValue.Name; break;
                        case "LODGroup": LODGroup = GameVersion == 3 ? property.Value.NameValue.Name : property.Value.StringValue; break;
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

            // Don't know why...
            if (GameVersion != 3)
                dataStream.Seek(4, SeekOrigin.Current);  

            numMipMaps = dataStream.ReadUInt32();                 // FG: 1st int32 (4 bytes / 32bits) is number of mipmaps
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
                else if (imgInfo.storageType == storage.pccCpr)  // ME1 only
                {
                    imgInfo.Offset = (int)dataStream.Position;
                    dataStream.Seek(imgInfo.CompressedSize, SeekOrigin.Current);
                }
                else if (imgInfo.storageType == storage.arcCpr || imgInfo.storageType == storage.arcUnc)
                    ArcDataSize += imgInfo.UncompressedSize;

                imgInfo.ImageSize = new ImageSize(dataStream.ReadUInt32(), dataStream.ReadUInt32());  // FG: 6th & 7th [or nth and (nth + 1) if local] int32 are width x height

                // KFreon: Test - instead of filtering out the null entries later, just don't add them here.
                if (imgInfo.Offset != -1 && imgInfo.CompressedSize != -1)
                    ImageList.Add(imgInfo);                                                                   // FG: A salty's favorite, add the struct to a list<struct>
                count--;
            }


            // save what remains
            int remainingBytes = (int)(dataStream.Length - dataStream.Position);
            footerData = new byte[remainingBytes];
            dataStream.Read(footerData, 0, footerData.Length);


            dataStream.Dispose();
        }

        /// <summary>
        /// Extracts image data from game files, optionally adding a proper header.
        /// </summary>
        /// <param name="size">Size of image to extract from mipmaps.</param>
        /// <param name="RequireHeader">True = adds a DDS header to the data.</param>
        /// <param name="TFCs">List of TFC's if available.</param>
        /// <returns>Byte[] containing image data, optionally including header.</returns>
        public byte[] ExtractImage(ImageSize size, bool RequireHeader, Dictionary<string, MemoryStream> TFCs = null)
        {
            byte[] retval = null;
            if (ImageList.Exists(img => img.ImageSize == size))
                retval = ExtractImage(ImageList.Find(img => img.ImageSize == size), RequireHeader, TFCs);

            return retval;
        }

        public byte[] ExtractImage(ImageInfo imgInfo, bool RequireHeader, Dictionary<string, MemoryStream> TFCs = null)
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
                    imgBuffer = ExtractME2_3ArcTex(TFCs, imgInfo);
                    break;
                case storage.arcCpr:  // ME1 only
                    if (GameVersion == 2)
                        imgBuffer = ExtractME2_3ArcTex(TFCs, imgInfo);
                    else
                    {
                        string archivePath = FullArcPath;
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
                                Texture2D temptex = new Texture2D(temp, i, GameDirecs);
                                byte[] tempBuffer = temptex.ExtractImage(imgInfo.ImageSize, RequireHeader);
                                if (tempBuffer != null)
                                    imgBuffer = tempBuffer;   // Should really just be able to exit here, but for some reason, you can't. Early exports seem to be broken in some way, so you have to extract all the damn things.
                            }
                        }
                    }
                    break;
                case storage.pccCpr:
                    using (MemoryStream ms = new MemoryStream(imageData))
                        imgBuffer = SaltLZOHelper.DecompressTex(ms, imgInfo.Offset, imgInfo.UncompressedSize, imgInfo.CompressedSize);
                    break;
                default:
                    throw new FormatException("Unsupported texture storage type");
            }

            if (imgBuffer == null)
                Debugger.Break();

            if (RequireHeader)
                return ToolsetTextureEngine.AddDDSHeader(imgBuffer, imgInfo, texFormat);
            else
                return imgBuffer;
        }

        byte[] ExtractME2_3ArcTex(Dictionary<string, MemoryStream> TFCs, ImageInfo imgInfo)
        {
            byte[] imgBuffer = null;
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
                else if (imgInfo.storageType == storage.arcCpr) // ME2
                    imgBuffer = SaltLZOHelper.DecompressTex(archiveStream, imgInfo.Offset, imgInfo.UncompressedSize, imgInfo.CompressedSize);
                else
                {
                    imgBuffer = new byte[imgInfo.UncompressedSize];
                    archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
                }
            }

            // Can dispose of stream if not a memory-loaded ME2 or 3 TFC
            if (TFCs == null)
                archiveStream.Dispose();

            return imgBuffer;
        }

        /// <summary>
        /// Extracts texture to file.
        /// </summary>
        /// <param name="fileName">Filename to save extracted image as.</param>
        /// <param name="info">Information of texture to be extracted.</param>
        public void ExtractImage(string fileName, ImageInfo info)
        {
            byte[] data = ExtractImage(info, true); // Always want a header for a file
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
        public byte[] ExtractMaxImage(bool RequireHeader)
        {
            // select max image size
            ImageSize maxImgSize = ImageList.Max(image => image.ImageSize);
            // extracting max image
            return ExtractImage(ImageList.Find(img => img.ImageSize == maxImgSize), RequireHeader);
        }

        /// <summary>
        /// Extracts largest image to file.
        /// </summary>
        /// <param name="filename">Filename to save extracted image as.</param>
        public void ExtractMaxImage(string filename)
        {
            byte[] data = ExtractMaxImage(true);  // Always want header for file.
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

            byte[] imgData = imgFile.Save(imgFile.Format.SurfaceFormat, MipHandling.KeepTopOnly);
            imgBuffer = ToolsetTextureEngine.RemoveDDSHeader(imgData);

            switch (imgInfo.storageType)
            {
                case storage.ME3arcCpr:
                case storage.arcCpr:
                case storage.arcUnc:
                    if (GameVersion == 1)
                        Debugger.Break();

                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive();
                    if (archivePath == null)
                        throw new FileNotFoundException("Teture archive not found!");
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    if (imgBuffer.Length != imgInfo.UncompressedSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.UncompressedSize + ", new is " + imgBuffer.Length);

                    // TODO ME1 completely different...
                    if (!arcName.Contains(Path.GetFileNameWithoutExtension(GameDirecs.CachePath), StringComparison.OrdinalIgnoreCase))  // CachePath is usually CustTextures, but arcName can be CustTextures#, so check for substring
                    {
                        ChooseNewCache(imgBuffer.Length);
                        archivePath = FullArcPath;
                    }
                    else
                    {
                        // Check cache not full.
                        FileInfo arc = new FileInfo(archivePath);
                        if (arc.Length + imgBuffer.Length >= 0x80000000)
                        {
                            ChooseNewCache(imgBuffer.Length);
                            archivePath = FullArcPath;
                        }
                    }

                    using (FileStream archiveStream = new FileStream(archivePath, FileMode.Append, FileAccess.Write))
                    {
                        int newOffset = (int)archiveStream.Position;

                        if (imgInfo.storageType == storage.ME3arcCpr)
                        {
                            imgBuffer = ZBlock.Compress(imgBuffer);
                            imgInfo.CompressedSize = imgBuffer.Length;
                        }
                        else if (GameVersion == 2 && imgInfo.storageType == storage.arcCpr)
                        {
                            byte[] tempBuff;
                            tempBuff = SaltLZOHelper.CompressTex(imgBuffer);
                            imgBuffer = new byte[tempBuff.Length];
                            Buffer.BlockCopy(tempBuff, 0, imgBuffer, 0, tempBuff.Length);
                            imgInfo.CompressedSize = imgBuffer.Length;
                        }
                        archiveStream.Write(imgBuffer, 0, imgBuffer.Length);

                        imgInfo.Offset = newOffset;
                    }
                    break;
                case storage.pccSto:
                    // Get image data and remove header.

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
                        imgBuffer = SaltLZOHelper.CompressTex(imgBuffer);

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

            if (GameVersion == 3)
            {
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
        }

        /// <summary>
        /// Replaces/upscales texture as required.
        /// </summary>
        /// <param name="newImg">Image to change to.</param>
        public void OneImageToRuleThemAll(ImageEngineImage newImg)
        {
            // starts from the smaller image
            ImageEngineFormat mipFormat = newImg.Format.SurfaceFormat;
            for (int i = newImg.NumMipMaps - 1; i >= 0; i--)
            {
                MipMap mip = newImg.MipMaps[i];

                if (mip.Height <= 4 || mip.Width <= 4)
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

        public void CopyImgList(Texture2D inTex, PCCObject pcc)
        {
            switch (GameVersion)
            {
                case 1:
                    ME1_CopyImgList(inTex, pcc);
                    break;
                case 2:
                    ME2_CopyImgList(inTex, pcc);
                    break;
                case 3:
                    ME3_CopyImgList(inTex, pcc);
                    break;
            }
        }

        void ME1_CopyImgList(Texture2D inTex, PCCObject pcc, bool norender = false)
        {
            List<ImageInfo> tempList = new List<ImageInfo>();
            MemoryStream tempData = new MemoryStream();
            numMipMaps = inTex.numMipMaps;

            // forced norenderfix
            // norender = true;

            int type = -1;
            if (!norender)
            {
                if (ImageList.Exists(img => img.storageType == storage.arcCpr) && ImageList.Count > 1)
                    type = 1;
                else if (ImageList.Exists(img => img.storageType == storage.pccCpr))
                    type = 2;
                else if (ImageList.Exists(img => img.storageType == storage.pccSto) || ImageList.Count == 1)
                    type = 3;
            }
            else
                type = 3;

            switch (type)
            {
                case 1:
                    for (int i = 0; i < inTex.ImageList.Count; i++)
                    {
                        try
                        {
                            ImageInfo newImg = new ImageInfo();
                            ImageInfo replaceImg = inTex.ImageList[i];
                            storage replaceType = ImageList.Find(img => img.ImageSize == replaceImg.ImageSize).storageType;

                            int j = 0;
                            while (replaceType == storage.empty)
                            {
                                j++;
                                replaceType = ImageList[ImageList.FindIndex(img => img.ImageSize == replaceImg.ImageSize) + j].storageType;
                            }

                            if (replaceType == storage.arcCpr || !ImageList.Exists(img => img.ImageSize == replaceImg.ImageSize))
                            {
                                newImg.storageType = storage.arcCpr;
                                newImg.UncompressedSize = replaceImg.UncompressedSize;
                                newImg.CompressedSize = replaceImg.CompressedSize;
                                newImg.ImageSize = replaceImg.ImageSize;
                                newImg.Offset = (int)(replaceImg.Offset + inTex.pccOffset + inTex.dataOffset);
                            }
                            else
                            {
                                newImg.storageType = storage.pccSto;
                                newImg.UncompressedSize = replaceImg.UncompressedSize;
                                newImg.CompressedSize = replaceImg.UncompressedSize;
                                newImg.ImageSize = replaceImg.ImageSize;
                                newImg.Offset = (int)(tempData.Position);
                                using (MemoryStream tempStream = new MemoryStream(inTex.imageData))
                                    tempData.WriteBytes(SaltLZOHelper.DecompressTex(tempStream, replaceImg.Offset, replaceImg.UncompressedSize, replaceImg.CompressedSize));
                            }
                            tempList.Add(newImg);
                        }
                        catch
                        {
                            ImageInfo replaceImg = inTex.ImageList[i];
                            if (!ImageList.Exists(img => img.ImageSize == replaceImg.ImageSize))
                                throw new Exception("An error occurred during imglist copying and no suitable replacement was found");
                            ImageInfo newImg = ImageList.Find(img => img.ImageSize == replaceImg.ImageSize);
                            if (newImg.storageType != storage.pccCpr && newImg.storageType != storage.pccSto)
                                throw new Exception("An error occurred during imglist copying and no suitable replacement was found");
                            int temppos = newImg.Offset;
                            newImg.Offset = (int)tempData.Position;
                            tempData.Write(imageData, temppos, newImg.CompressedSize);
                            tempList.Add(newImg);
                        }
                    }
                    break;
                case 2:
                    for (int i = 0; i < inTex.ImageList.Count; i++)
                    {
                        ImageInfo newImg = new ImageInfo();
                        ImageInfo replaceImg = inTex.ImageList[i];
                        newImg.storageType = storage.pccCpr;
                        newImg.UncompressedSize = replaceImg.UncompressedSize;
                        newImg.CompressedSize = replaceImg.CompressedSize;
                        newImg.ImageSize = replaceImg.ImageSize;
                        newImg.Offset = (int)(tempData.Position);
                        byte[] buffer = new byte[newImg.CompressedSize];
                        Buffer.BlockCopy(inTex.imageData, replaceImg.Offset, buffer, 0, buffer.Length);
                        tempData.WriteBytes(buffer);
                        tempList.Add(newImg);
                    }
                    break;
                case 3:
                    for (int i = 0; i < inTex.ImageList.Count; i++)
                    {
                        ImageInfo newImg = new ImageInfo();
                        ImageInfo replaceImg = inTex.ImageList[i];
                        newImg.storageType = storage.pccSto;
                        newImg.UncompressedSize = replaceImg.UncompressedSize;
                        newImg.CompressedSize = replaceImg.UncompressedSize;
                        newImg.ImageSize = replaceImg.ImageSize;
                        newImg.Offset = (int)(tempData.Position);
                        if (replaceImg.storageType == storage.pccCpr)
                        {
                            using (MemoryStream tempStream = new MemoryStream(inTex.imageData))
                            {
                                tempData.WriteBytes(SaltLZOHelper.DecompressTex(tempStream, replaceImg.Offset, replaceImg.UncompressedSize, replaceImg.CompressedSize));
                            }
                        }
                        else if (replaceImg.storageType == storage.pccSto)
                        {
                            byte[] buffer = new byte[newImg.CompressedSize];
                            Buffer.BlockCopy(inTex.imageData, replaceImg.Offset, buffer, 0, buffer.Length);
                            tempData.WriteBytes(buffer);
                        }
                        else
                            throw new NotImplementedException("Copying from non package stored texture no available");
                        tempList.Add(newImg);
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }

            for (int i = 0; i < tempList.Count; i++)
            {
                ImageInfo tempinfo = tempList[i];
                if (inTex.ImageList[i].storageType == storage.empty)
                    tempinfo.storageType = storage.empty;
                tempList[i] = tempinfo;
            }

            ImageList = tempList;
            imageData = tempData.ToArray();
            tempData.Close();

            byte[] buff;
            //Copy properties
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
                    {
                        for (int j = 0; j < 12; j++)
                            tempMem.WriteByte(0);
                    }
                    else
                    {
                        tempMem.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                        tempMem.WriteInt64(prop.Size);

                        switch (prop.TypeVal)
                        {
                            case SaltPropertyReader.Type.IntProperty:
                                tempMem.WriteInt32(prop.Value.IntValue);
                                break;
                            case SaltPropertyReader.Type.BoolProperty:
                                tempMem.Seek(-4, SeekOrigin.Current);
                                tempMem.WriteInt32(prop.Value.IntValue);
                                tempMem.Seek(4, SeekOrigin.Current);
                                break;
                            case SaltPropertyReader.Type.NameProperty:
                                tempMem.WriteInt64(pcc.AddName(prop.Value.StringValue));
                                // Heff: Modified to handle name references.
                                //var index = pcc.AddName(prop.Value.StringValue);
                                //tempMem.WriteInt32(index);
                                //tempMem.WriteInt32(prop.Value.NameValue.count);
                                break;
                            case SaltPropertyReader.Type.StrProperty:
                                tempMem.WriteInt32(prop.Value.StringValue.Length + 1);
                                foreach (char c in prop.Value.StringValue)
                                    tempMem.WriteByte((byte)c);
                                tempMem.WriteByte(0);
                                break;
                            case SaltPropertyReader.Type.StructProperty:
                                tempMem.WriteInt64(pcc.AddName(prop.Value.StringValue));
                                foreach (SaltPropertyReader.PropertyValue value in prop.Value.Array)
                                    tempMem.WriteInt32(value.IntValue);
                                break;
                            case SaltPropertyReader.Type.ByteProperty:
                                tempMem.WriteInt32(pcc.AddName(prop.Value.StringValue));
                                tempMem.WriteInt32(prop.Value.IntValue);
                                break;
                            case SaltPropertyReader.Type.FloatProperty:
                                tempMem.WriteFloat32(prop.Value.FloatValue);
                                break;
                            default:
                                throw new FormatException("unknown property");
                        }
                    }
                }
                buff = tempMem.ToArray();
            }

            int propertiesOffset = SaltPropertyReader.detectStart(pcc, buff);
            headerData = new byte[propertiesOffset];
            Buffer.BlockCopy(buff, 0, headerData, 0, propertiesOffset);
            properties = new Dictionary<string, SaltPropertyReader.Property>();
            List<SaltPropertyReader.Property> tempProperties = SaltPropertyReader.getPropList(pcc, buff);
            UnpackNum = 0;
            for (int i = 0; i < tempProperties.Count; i++)
            {
                SaltPropertyReader.Property property = tempProperties[i];
                if (property.Name == "UnpackMin")
                    UnpackNum++;

                if (!properties.ContainsKey(property.Name))
                    properties.Add(property.Name, property);

                switch (property.Name)
                {
                    case "Format": texFormat = ToolsetTextureEngine.ParseFormat(property.Value.StringValue); break;
                    case "LODGroup": LODGroup = property.Value.StringValue; break;
                    case "CompressionSettings": Compression = property.Value.StringValue; break;
                    case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                }
            }

            // if "None" property isn't found throws an exception
            if (dataOffset == 0)
                throw new Exception("\"None\" property not found");
        }

        void ME2_CopyImgList(Texture2D inTex, PCCObject pcc)
        {
            numMipMaps = inTex.numMipMaps;

            if (properties.ContainsKey("NeverStream") && properties["NeverStream"].Value.IntValue == 1)
            {
                imageData = null;
                // store images as pccSto format
                ImageList = new List<ImageInfo>();
                MemoryStream tempData = new MemoryStream();

                for (int i = 0; i < inTex.ImageList.Count; i++)
                {
                    ImageInfo newImg = new ImageInfo();
                    ImageInfo replaceImg = inTex.ImageList[i];
                    newImg.storageType = storage.pccSto;
                    newImg.UncompressedSize = replaceImg.UncompressedSize;
                    newImg.CompressedSize = replaceImg.UncompressedSize;
                    newImg.ImageSize = replaceImg.ImageSize;
                    newImg.Offset = (int)(tempData.Position);
                    if (replaceImg.storageType == storage.arcCpr)
                    {
                        string archivePath = inTex.FullArcPath;
                        if (!File.Exists(archivePath))
                            throw new FileNotFoundException("Texture archive not found in " + archivePath);

                        using (FileStream archiveStream = File.OpenRead(archivePath))
                        {
                            archiveStream.Seek(replaceImg.Offset, SeekOrigin.Begin);
                            tempData.WriteBytes(SaltLZOHelper.DecompressTex(archiveStream, replaceImg.Offset, replaceImg.UncompressedSize, replaceImg.CompressedSize));
                        }
                    }
                    else if (replaceImg.storageType == storage.pccSto)
                    {
                        byte[] buffer = new byte[newImg.CompressedSize];
                        Buffer.BlockCopy(inTex.imageData, replaceImg.Offset, buffer, 0, buffer.Length);
                        tempData.WriteBytes(buffer);
                    }
                    else
                        throw new NotImplementedException("Copying from non package stored texture no available");
                    ImageList.Add(newImg);
                }

                for (int i = 0; i < ImageList.Count; i++)
                {
                    ImageInfo tempinfo = ImageList[i];
                    if (inTex.ImageList[i].storageType == storage.empty)
                        tempinfo.storageType = storage.empty;
                    ImageList[i] = tempinfo;
                }

                imageData = tempData.ToArray();
                tempData.Close();
                tempData = null;
                GC.Collect();
            }
            else
            {
                imageData = inTex.imageData;
                ImageList = inTex.ImageList;
            }

            // add properties "TextureFileCacheName" and "TFCFileGuid" if they are missing,
            if (!properties.ContainsKey("TextureFileCacheName") && inTex.properties.ContainsKey("TextureFileCacheName"))
            {
                SaltPropertyReader.Property none = properties["None"];
                properties.Remove("None");

                SaltPropertyReader.Property property = new SaltPropertyReader.Property();
                property.TypeVal = SaltPropertyReader.Type.NameProperty;
                property.Name = "TextureFileCacheName";
                property.Size = 8;
                SaltPropertyReader.PropertyValue value = new SaltPropertyReader.PropertyValue();
                value.StringValue = "Textures";
                property.Value = value;
                properties.Add("TextureFileCacheName", property);
                arcName = value.StringValue;

                if (!properties.ContainsKey("TFCFileGuid"))
                {
                    SaltPropertyReader.Property guidprop = new SaltPropertyReader.Property();
                    guidprop.TypeVal = SaltPropertyReader.Type.StructProperty;
                    guidprop.Name = "TFCFileGuid";
                    guidprop.Size = 16;
                    SaltPropertyReader.PropertyValue guid = new SaltPropertyReader.PropertyValue();
                    guid.len = guidprop.Size;
                    guid.StringValue = "Guid";
                    guid.IntValue = pcc.AddName(guid.StringValue);
                    guid.Array = new List<SaltPropertyReader.PropertyValue>();
                    for (int i = 0; i < 4; i++)
                        guid.Array.Add(new SaltPropertyReader.PropertyValue());
                    guidprop.Value = guid;
                    properties.Add("TFCFileGuid", guidprop);
                }

                properties.Add("None", none);
            }

            // copy specific properties from inTex
            for (int i = 0; i < inTex.properties.Count; i++)
            {
                SaltPropertyReader.Property prop = inTex.properties.ElementAt(i).Value;
                switch (prop.Name)
                {
                    case "TextureFileCacheName":
                        arcName = prop.Value.StringValue;
                        properties["TextureFileCacheName"].Value.StringValue = arcName;
                        break;
                    case "TFCFileGuid":
                        SaltPropertyReader.Property GUIDProp = properties["TFCFileGuid"];
                        for (int l = 0; l < 4; l++)
                        {
                            SaltPropertyReader.PropertyValue tempVal = GUIDProp.Value.Array[l];
                            tempVal.IntValue = prop.Value.Array[l].IntValue;
                            GUIDProp.Value.Array[l] = tempVal;
                        }
                        break;
                    case "MipTailBaseIdx":
                        properties["MipTailBaseIdx"].Value.IntValue = prop.Value.IntValue;
                        break;
                    case "SizeX":
                        properties["SizeX"].Value.IntValue = prop.Value.IntValue;
                        break;
                    case "SizeY":
                        properties["SizeY"].Value.IntValue = prop.Value.IntValue;
                        break;
                }
            }
        }

        void ME3_CopyImgList(Texture2D inTex, PCCObject pcc)
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
                        texFormat = ToolsetTextureEngine.ParseFormat(pcc.Names[property.Value.IntValue].Substring(3));
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


        private void ChooseNewCache(int buffLength)
        {
            string pathCooked = GameDirecs.PathCooked;

            string cachePath = GameDirecs.CachePath;
            string cacheName = Path.GetFileNameWithoutExtension(cachePath);
            string cacheFileName = Path.GetFileName(cachePath);
            string rootFolder = Path.GetDirectoryName(cachePath);

            // Cache needs creating
            int i = 0;
            while (i < 100)  // KFreon: Arbitrary limit on number of TFC's - was 10, turns out that isn't enough.
            {
                FileInfo cacheInfo = new FileInfo(cachePath);

                if (!cacheInfo.Exists)
                {
                    MakeCache(cachePath);
                    break;
                }
                else if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)  // Test if cache is full
                    break;
                i++;

                // Cache full - move to next one
                cachePath = Path.Combine(rootFolder, Path.GetFileNameWithoutExtension(GameDirecs.CachePath), i + ".tfc");  // Add number to end

                // Update working properties.
                cacheName = Path.GetFileNameWithoutExtension(cachePath);
                cacheFileName = Path.GetFileName(cachePath);
            }


            // Move texture to new cache and update properties
            GameDirecs.CachePath = cachePath;
            MoveCaches(rootFolder, cacheFileName);
            properties["TextureFileCacheName"].Value.StringValue = cacheName;
            arcName = cacheName;
            FullArcPath = cachePath;
        }

        private void MoveCaches(string cookedPath, string NewCache)
        {
            //Fix the GUID
            using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Open, FileAccess.Read))
            {
                SaltPropertyReader.Property GUIDProp = properties["TFCFileGuid"];

                for (int i = 0; i < (GameVersion == 3 ? 16 : 4); i++)
                {
                    SaltPropertyReader.PropertyValue tempVal = GUIDProp.Value.Array[i];
                    tempVal.IntValue = GameVersion == 3 ? newCache.ReadByte() : newCache.ReadInt32();
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
                            case storage.ME3arcCpr:
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
            // Currently ME1 never gets here. Uses FindFile
            if (!String.IsNullOrEmpty(arcName))
            {
                int dotInd = arcName.IndexOf('.');
                if (dotInd != -1)
                    arcName = arcName.Substring(0, dotInd);
            }

            var TFCs = GameVersion == 3 ? ME3TFCs : ME2TFCs;

            string arc = TFCs.Where(file => String.Compare(Path.GetFileNameWithoutExtension(file), arcName, true) == 0).FirstOrDefault();
            if (arc == null)
                throw new FileNotFoundException($"Texture archive called {arcName} not found.");
            else
            {
                FullArcPath = Path.GetFullPath(arc);
                return FullArcPath;
            }
        }

        /// <summary>
        /// This function will first guess and then do a thorough search to find the original location of the texture
        /// </summary>
        private string FindFile() 
        {
            List<string> GameFiles = MEDirectories.MEDirectories.ME1Files;
            
            // Use mapping to determine file.
            int dotInd = ME1_PackageFullName.IndexOf('.');
            string package = dotInd == -1 ? ME1_PackageFullName.ToUpperInvariant() : ME1_PackageFullName.Substring(0, dotInd).ToUpperInvariant();
            if (ME1_Filenames_Paths.ContainsKey(package))
                return ME1_Filenames_Paths[package];
            

            // Not in the main list for some reason. Search the slooooooooow way.
            for (int i = 0; i < GameFiles.Count; i++)
            {
                PCCObject temp = new PCCObject(GameFiles[i], GameVersion);
                for (int j = 0; j < temp.Exports.Count; j++)
                {
                    ExportEntry exp = temp.Exports[j];
                    if (String.Compare(texName, exp.ObjectName, true) == 0 && exp.ClassName == "Texture2D")// && (GameVersion == 1 ? ME1_PackageFullName == exp.PackageFullName : true))
                    {
                        Texture2D temptex = new Texture2D(temp, j, GameDirecs);  
                        if (temptex.ImageList[0].storageType == storage.pccCpr || temptex.ImageList[0].storageType == storage.pccSto)
                        {
                            return GameFiles[i];
                        }
                    }
                }
            }
            return null;
        }

        private void MakeCache(String filename)
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
                byte[] imgdata = ExtractMaxImage(true);
                if (imgdata == null)
                    return null;
                using (ImageEngineImage img = new ImageEngineImage(imgdata))
                    return img.GetWPFBitmap();
            }
            catch { }
            return null;
        }

        [Obsolete("Why are you even using this...")]
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


        public byte[] ToArray(uint pccExportDataOffset, PCCObject pcc)
        {
            switch (GameVersion)
            {
                case 1:
                    return ME1_ToArray(pccExportDataOffset, pcc);
                case 2:
                    return ME2_ToArray(pccExportDataOffset, pcc);
                case 3:
                    return ME3_ToArray(pccExportDataOffset, pcc);
            }
            return null;
        }

        byte[] ME1_ToArray(uint pccExportDataOffset, PCCObject pcc)
        {
            MemoryStream buffer = new MemoryStream();
            buffer.Write(headerData, 0, headerData.Length);

            if (properties.ContainsKey("LODGroup"))
            {
                properties["LODGroup"].Value.StringValue = "TEXTUREGROUP_LightAndShadowMap";
                //properties["LODGroup"].Value.IntValue = 1025;
            }
            else
            {
                buffer.WriteInt64(pcc.AddName("LODGroup"));
                buffer.WriteInt64(pcc.AddName("ByteProperty"));
                buffer.WriteInt64(8);
                buffer.WriteInt32(pcc.AddName("TEXTUREGROUP_LightAndShadowMap"));
                buffer.WriteInt32(1025);
            }

            foreach (KeyValuePair<string, SaltPropertyReader.Property> kvp in properties)
            {
                SaltPropertyReader.Property prop = kvp.Value;

                if (prop.Name == "UnpackMin")
                {
                    for (int j = 0; j < UnpackNum; j++)
                    {
                        buffer.WriteInt64(pcc.AddName(prop.Name));
                        buffer.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                        buffer.WriteInt32(prop.Size);
                        buffer.WriteInt32(j);
                        buffer.WriteFloat32(prop.Value.FloatValue);
                    }
                    continue;
                }

                buffer.WriteInt64(pcc.AddName(prop.Name));
                if (prop.Name == "None")
                {
                    for (int j = 0; j < 12; j++)
                        buffer.WriteByte(0);
                }
                else
                {
                    buffer.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                    buffer.WriteInt64(prop.Size);

                    switch (prop.TypeVal)
                    {
                        case SaltPropertyReader.Type.IntProperty:
                            buffer.WriteInt32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.BoolProperty:
                            buffer.Seek(-4, SeekOrigin.Current);
                            buffer.WriteInt32(prop.Value.IntValue);
                            buffer.Seek(4, SeekOrigin.Current);
                            break;
                        case SaltPropertyReader.Type.NameProperty:
                            buffer.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            // Heff: Modified to handle name references.
                            //var index = pcc.AddName(prop.Value.StringValue);
                            //buffer.WriteInt32(index);
                            //buffer.WriteInt32(prop.Value.NameValue.count);
                            break;
                        case SaltPropertyReader.Type.StrProperty:
                            buffer.WriteInt32(prop.Value.StringValue.Length + 1);
                            foreach (char c in prop.Value.StringValue)
                                buffer.WriteByte((byte)c);
                            buffer.WriteByte(0);
                            break;
                        case SaltPropertyReader.Type.StructProperty:
                            buffer.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            foreach (SaltPropertyReader.PropertyValue value in prop.Value.Array)
                                buffer.WriteInt32(value.IntValue);
                            break;
                        case SaltPropertyReader.Type.ByteProperty:
                            buffer.WriteInt32(pcc.AddName(prop.Value.StringValue));
                            buffer.WriteInt32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.FloatProperty:
                            buffer.WriteFloat32(prop.Value.FloatValue);
                            break;
                        default:
                            throw new FormatException("unknown property");
                    }
                }
            }

            buffer.WriteInt32((int)(pccOffset + buffer.Position + 4));

            //Remove empty textures
            List<ImageInfo> tempList = new List<ImageInfo>();
            foreach (ImageInfo imgInfo in ImageList)
            {
                if (imgInfo.storageType != storage.empty)
                    tempList.Add(imgInfo);
            }
            ImageList = tempList;
            numMipMaps = (uint)ImageList.Count;

            buffer.WriteUInt32(numMipMaps);

            foreach (ImageInfo imgInfo in ImageList)
            {
                buffer.WriteInt32((int)imgInfo.storageType);
                buffer.WriteInt32(imgInfo.UncompressedSize);
                buffer.WriteInt32(imgInfo.CompressedSize);
                if (imgInfo.storageType == storage.pccSto)
                {
                    buffer.WriteInt32((int)(imgInfo.Offset + pccExportDataOffset + dataOffset));
                    buffer.Write(imageData, imgInfo.Offset, imgInfo.UncompressedSize);
                }
                else if (imgInfo.storageType == storage.pccCpr)
                {
                    buffer.WriteInt32((int)(imgInfo.Offset + pccExportDataOffset + dataOffset));
                    buffer.Write(imageData, imgInfo.Offset, imgInfo.CompressedSize);
                }
                else
                    buffer.WriteInt32(imgInfo.Offset);
                if (imgInfo.ImageSize.Width < 4)
                    buffer.WriteUInt32(4);
                else
                    buffer.WriteUInt32(imgInfo.ImageSize.Width);
                if (imgInfo.ImageSize.Height < 4)
                    buffer.WriteUInt32(4);
                else
                    buffer.WriteUInt32(imgInfo.ImageSize.Height);
            }
            buffer.WriteBytes(footerData);
            return buffer.ToArray();
        }

        byte[] ME2_ToArray(uint pccExportDataOffset, PCCObject pcc)
        {
            MemoryStream buffer = new MemoryStream();
            buffer.Write(headerData, 0, headerData.Length);

            if (properties.ContainsKey("LODGroup"))
            {
                properties["LODGroup"].Value.StringValue = "TEXTUREGROUP_LightAndShadowMap";
                properties["LODGroup"].Value.String2 = pcc.Names[0];
            }
            else
            {
                buffer.WriteInt64(pcc.AddName("LODGroup"));
                buffer.WriteInt64(pcc.AddName("ByteProperty"));
                buffer.WriteInt64(8);
                buffer.WriteInt64(pcc.AddName("TEXTUREGROUP_LightAndShadowMap"));
            }

            foreach (KeyValuePair<string, SaltPropertyReader.Property> kvp in properties)
            {
                SaltPropertyReader.Property prop = kvp.Value;

                if (prop.Name == "UnpackMin")
                {
                    for (int j = 0; j < UnpackNum; j++)
                    {
                        buffer.WriteInt64(pcc.AddName(prop.Name));
                        buffer.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                        buffer.WriteInt32(prop.Size);
                        buffer.WriteInt32(j);
                        buffer.WriteFloat32(prop.Value.FloatValue);
                    }
                    continue;
                }

                buffer.WriteInt64(pcc.AddName(prop.Name));
                if (prop.Name == "None")
                {
                    for (int j = 0; j < 12; j++)
                        buffer.WriteByte(0);
                }
                else
                {
                    buffer.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                    buffer.WriteInt64(prop.Size);

                    switch (prop.TypeVal)
                    {
                        case SaltPropertyReader.Type.IntProperty:
                            buffer.WriteInt32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.BoolProperty:
                            buffer.WriteInt32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.NameProperty:
                            buffer.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            // Heff: Modified to handle name references.
                            //var index = pcc.AddName(prop.Value.StringValue);
                            //buffer.WriteInt32(index);
                            //buffer.WriteInt32(prop.Value.NameValue.count);
                            break;
                        case SaltPropertyReader.Type.StrProperty:
                            buffer.WriteInt32(prop.Value.StringValue.Length + 1);
                            foreach (char c in prop.Value.StringValue)
                                buffer.WriteByte((byte)c);
                            buffer.WriteByte(0);
                            break;
                        case SaltPropertyReader.Type.StructProperty:
                            string strVal = prop.Value.StringValue;
                            if (prop.Name.ToLowerInvariant().Contains("guid"))
                                strVal = "Guid";

                            buffer.WriteInt64(pcc.AddName(strVal));
                            foreach (SaltPropertyReader.PropertyValue value in prop.Value.Array)
                                buffer.WriteInt32(value.IntValue);
                            break;
                        case SaltPropertyReader.Type.ByteProperty:
                            buffer.WriteInt32(pcc.AddName(prop.Value.StringValue));
                            buffer.WriteInt32(pcc.AddName(prop.Value.String2));
                            break;
                        case SaltPropertyReader.Type.FloatProperty:
                            buffer.WriteFloat32(prop.Value.FloatValue);
                            break;
                        default:
                            throw new FormatException("unknown property");
                    }
                }

            }

            buffer.WriteInt32((int)buffer.Position + (int)pccExportDataOffset);

            //Remove empty textures
            List<ImageInfo> tempList = new List<ImageInfo>();
            foreach (ImageInfo imgInfo in ImageList)
            {
                if (imgInfo.storageType != storage.empty)
                    tempList.Add(imgInfo);
            }
            ImageList = tempList;
            numMipMaps = (uint)ImageList.Count;

            buffer.WriteUInt32(numMipMaps);
            foreach (ImageInfo imgInfo in ImageList)
            {
                buffer.WriteInt32((int)imgInfo.storageType);
                buffer.WriteInt32(imgInfo.UncompressedSize);
                buffer.WriteInt32(imgInfo.CompressedSize);
                if (imgInfo.storageType == storage.pccSto)
                {
                    buffer.WriteInt32((int)(buffer.Position + pccExportDataOffset));
                    buffer.Write(imageData, imgInfo.Offset, imgInfo.UncompressedSize);
                }
                else
                    buffer.WriteInt32(imgInfo.Offset);
                if (imgInfo.ImageSize.Width < 4)
                    buffer.WriteUInt32(4);
                else
                    buffer.WriteUInt32(imgInfo.ImageSize.Width);
                if (imgInfo.ImageSize.Height < 4)
                    buffer.WriteUInt32(4);
                else
                    buffer.WriteUInt32(imgInfo.ImageSize.Height);
            }
            buffer.WriteBytes(footerData);
            return buffer.ToArray();
        }

        byte[] ME3_ToArray(uint pccExportDataOffset, PCCObject pcc)
        {
            using (MemoryStream tempStream = new MemoryStream())
            {
                tempStream.WriteBytes(headerData);

                // Whilst testing get rid of this
                // Heff: Seems like the shadowmap was the best solution in most cases,
                // adding an exception for known problematic animated textures for now.
                // (See popup in tpftools)
                if (properties.ContainsKey("LODGroup"))
                    properties["LODGroup"].Value.String2 = "TEXTUREGROUP_Shadowmap";
                else
                {
                    tempStream.WriteInt64(pcc.AddName("LODGroup"));
                    tempStream.WriteInt64(pcc.AddName("ByteProperty"));
                    tempStream.WriteInt64(8);
                    tempStream.WriteInt64(pcc.AddName("TextureGroup"));
                    tempStream.WriteInt64(pcc.AddName("TEXTUREGROUP_Shadowmap"));
                }

                foreach (KeyValuePair<string, SaltPropertyReader.Property> kvp in properties)
                {
                    SaltPropertyReader.Property prop = kvp.Value;

                    if (prop.Name == "UnpackMin")
                    {
                        for (int i = 0; i < UnpackNum; i++)
                        {
                            tempStream.WriteInt64(pcc.AddName(prop.Name));
                            tempStream.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                            tempStream.WriteInt32(prop.Size);
                            tempStream.WriteInt32(i);
                            tempStream.WriteFloat32(prop.Value.FloatValue);
                        }
                        continue;
                    }

                    tempStream.WriteInt64(pcc.AddName(prop.Name));

                    if (prop.Name == "None")
                        continue;

                    tempStream.WriteInt64(pcc.AddName(prop.TypeVal.ToString()));
                    tempStream.WriteInt64(prop.Size);

                    switch (prop.TypeVal)
                    {
                        case SaltPropertyReader.Type.FloatProperty:
                            tempStream.WriteFloat32(prop.Value.FloatValue);
                            break;
                        case SaltPropertyReader.Type.IntProperty:
                            tempStream.WriteInt32(prop.Value.IntValue);
                            break;
                        case SaltPropertyReader.Type.NameProperty:
                            tempStream.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            break;
                        case SaltPropertyReader.Type.ByteProperty:
                            tempStream.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            tempStream.WriteInt64(pcc.AddName(prop.Value.String2));
                            break;
                        case SaltPropertyReader.Type.BoolProperty:
                            tempStream.WriteByte((byte)(prop.Value.Boolereno ? 1 : 0));
                            break;
                        case SaltPropertyReader.Type.StructProperty:
                            tempStream.WriteInt64(pcc.AddName(prop.Value.StringValue));
                            for (int i = 0; i < prop.Size; i++)
                                tempStream.WriteByte((byte)prop.Value.Array[i].IntValue);
                            break;
                        default:
                            throw new NotImplementedException("Property type: " + prop.TypeVal.ToString() + ", not yet implemented. TELL ME ABOUT THIS!");
                    }
                }


                numMipMaps = (uint)ImageList.Count;

                tempStream.WriteUInt32(numMipMaps);
                foreach (ImageInfo imgInfo in ImageList)
                {
                    tempStream.WriteInt32((int)imgInfo.storageType);
                    tempStream.WriteInt32(imgInfo.UncompressedSize);
                    tempStream.WriteInt32(imgInfo.CompressedSize);
                    if (imgInfo.storageType == storage.pccSto)
                    {
                        tempStream.WriteInt32((int)(imgInfo.Offset + pccExportDataOffset + dataOffset));
                        tempStream.Write(imageData, (int)imgInfo.Offset, imgInfo.UncompressedSize);
                    }
                    else
                        tempStream.WriteInt32(imgInfo.Offset);
                    tempStream.WriteUInt32(imgInfo.ImageSize.Width);
                    tempStream.WriteUInt32(imgInfo.ImageSize.Height);
                }
                //// Texture2D footer, 24 bytes size - changed to 20
                tempStream.WriteBytes(footerData);
                return tempStream.ToArray();
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
