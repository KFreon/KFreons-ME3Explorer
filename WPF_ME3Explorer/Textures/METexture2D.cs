using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UsefulThings;
using System.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Diagnostics;
using CSharpImageLibrary.General;
using WPF_ME3Explorer.PCCObjects;
using MEDirectories;
using Gibbed.IO;
using SaltTPF;
using System.Windows.Media.Imaging;
using AmaroK86.ImageFormat;

namespace WPF_ME3Explorer.Textures
{
    public class METexture2D
    {
        const string class1 = "Texture2D";
        const string class2 = "LightMapTexture2D";
        const string class3 = "TextureFlipBook";
        const string CustCache = "CustTextures";

        public enum Tex2DPCCPopulationResult
        {
            GotRequested, FailedToGetRequested, NoPCCs
        }

        #region Properties
        public int GameVersion { get; set; }
        public List<ImageInfo> imgList { get; set; }
        public string texName { get; set; }
        public ImageEngineFormat texFormat { get; set; }
        public uint pccOffset { get; set; }
        public bool hasChanged { get; set; }
        public string arcName { get; set; }
        public string LODGroup { get; set; }
        public uint Hash { get; set; }
        public int pccExpIdx { get; set; }
        public int Mips { get; set; }
        public bool NoRenderFix { get; set; }
        public string ListName { get; set; }
        public string Class { get; set; }
        public uint exportsOffset { get; set; }
        public string FullPackage { get; set; }
        public string pccFileName { get; set; }
        public Dictionary<string, SaltPropertyReader.Property> properties { get; set; }
        string Compression { get; set; }
        byte[] headerData { get; set; }
        int UnpackNum { get; set; }
        uint dataOffset { get; set; }
        public string FullArcPath { get; set; }
        string PathBIOGame { get; set; }
        public byte[] imageData { get; set; }
        public int ArcDataSize { get; set; }
        byte[] footerData { get; set; }
        public List<PCCEntry> PCCs { get; set; }
        #endregion Properties


        #region Constructors
        /// <summary>
        /// Creates a new instance of Texture2D (Game independent)
        /// </summary>
        public METexture2D()
        {
            PCCs = new List<PCCEntry>();
            hasChanged = false;
            imgList = new List<ImageInfo>();

        }


        public METexture2D(METexture2D originalTex2D, AbstractPCCObject pcc, int expID) : this(pcc, expID, originalTex2D.PathBIOGame, originalTex2D.Hash)
        {
            CopyImgList(originalTex2D, pcc);
        }


        /// <summary>
        /// Creates a new instance of Texture2D (Game independent)
        /// </summary>
        /// <param name="tex">Base structure.</param>
        public METexture2D(AbstractTexInfo tex) : this(tex.EntryName, tex.PCCs, tex.PathBIOGame, tex.GameVersion, tex.Hash, tex.Format)
        {
            Mips = tex.NumMips;
        }


        /// <summary>
        /// Creates a new instance of Texture2D (Game independent)
        /// </summary>
        /// <param name="texname">Name of texture.</param>
        /// <param name="pccs">PCCs and ExpID's related to this texture.</param>
        /// <param name="pathbio">Path to BIOGame folder.</param>
        /// <param name="whichgame">Game to which Texture belongs. 1-3</param>
        /// <param name="hash">Texmod hash of texture.</param>
        public METexture2D(string texname, ICollection<PCCEntry> pccs, string pathbio, int gameversion, uint hash, ImageEngineFormat format) : this()
        {
            texFormat = format;
            texName = texname;
            PCCs = new List<PCCEntry>(pccs);
            GameVersion = gameversion;
            Hash = hash;
        }


        /// <summary>
        /// Creates instance of Texture2D (Game independent)
        /// </summary>
        /// <param name="texname">Name of texture.</param>
        /// <param name="pccs">List of associated PCC's.</param>
        /// <param name="ExpIDs">List of associated Export ID's.</param>
        /// <param name="pathBIOGame">Path to BIOGame folder.</param>
        /// <param name="gameversion">Game texture belongs to.</param>
        /// <param name="hash">Texmod hash.</param>
        /// <param name="listname">Not sure...</param>
        public METexture2D(string texname, List<string> pccs, List<int> ExpIDs, string pathBIOGame, int gameversion, ImageEngineFormat format, uint hash = 0, String listname = null) : this(texname, PCCEntry.PopulatePCCEntries(pccs, ExpIDs), pathBIOGame, gameversion, hash, format)
        {
            ListName = listname;
        }


        /// <summary>
        /// Creates instance of Texture2D (Game independent)
        /// </summary>
        /// <param name="pcc">PCC object containing this texture.</param>
        /// <param name="pccExpID">Export ID of this texture in PCC.</param>
        /// <param name="pathbio">Path to BIOGame folder.</param>
        /// <param name="hash">Texmod hash.</param>
        public METexture2D(AbstractPCCObject pcc, int pccExpID, string pathbio, uint hash = 0) : this()
        {
            Hash = hash;
            PathBIOGame = pathbio;
            GameVersion = pcc.GameVersion;

            PopulateFromPCC(pcc, pccExpID);
        }
        #endregion Constructors



        /// <summary>
        /// 
        /// </summary>
        /// <param name="RequestPCCStored">True = get pcc stored, false = get cache stored, null = use first pcc.</param>
        /// <returns></returns>
        public Tex2DPCCPopulationResult PopulateFromPCC(bool? RequestPCCStored = null)
        {
            if (PCCs != null && PCCs.Count > 0)
            {
                // KFreon: Filter list so that it gets only checked files AND either the first pcc (null), pccstored (true), or cache stored (false). () are the values of RequestPCCStored.
                var pcclist = PCCs.Where(t => t.Using && (RequestPCCStored == null ? true : (RequestPCCStored == true ? t.IsPCCStored : !t.IsPCCStored)));

                if (pcclist.Count() == 0)
                    return Tex2DPCCPopulationResult.FailedToGetRequested;

                PCCEntry pcc = pcclist.First();
                /*PCCEntry pcc = null;
                foreach (var temppcc in pcclist)
                    if (!temppcc.File.Contains("END"))
                    {
                        pcc = temppcc;
                        //break;
                    }*/


                PopulateFromPCC(AbstractPCCObject.Create(pcc.File, GameVersion, PathBIOGame), pcc.ExpID);
                return Tex2DPCCPopulationResult.GotRequested;
            }
            return Tex2DPCCPopulationResult.NoPCCs;
        }

        public void PopulateFromPCC(AbstractPCCObject pcc, int pccExpID)
        {
            if (pcc.isExport(pccExpID))
            {
                AbstractExportEntry exp = pcc.Exports[pccExpID];

                if (exp.ClassName == class1 || exp.ClassName == class2 || exp.ClassName == class3)
                {
                    Class = exp.ClassName;
                    exportsOffset = exp.DataOffset;
                    FullPackage = exp.PackageFullName;
                    texName = exp.ObjectName;
                    pccFileName = pcc.pccFileName;

                    /*if (texName == "RadialLinesFB")
                        Debugger.Break();*/


                    if (!PCCs.Any(t => t.File.Contains(pccFileName)))
                        PCCs.Add(new PCCEntry(pccFileName, pccExpID));

                    properties = new Dictionary<string, SaltPropertyReader.Property>();
                    byte[] rawData = (byte[])exp.Data.Clone();
                    Compression = "No Compression";
                    int propertiesOffset = SaltPropertyReader.detectStart(pcc, rawData);
                    headerData = new byte[propertiesOffset];
                    Buffer.BlockCopy(rawData, 0, headerData, 0, propertiesOffset);
                    pccOffset = (uint)exp.DataOffset;
                    UnpackNum = 0;
                    List<SaltPropertyReader.Property> tempProperties = SaltPropertyReader.getPropList(pcc, rawData);
                    SaltPropertyReader.Property property = null;
                    for (int i = 0; i < tempProperties.Count; i++)
                    {
                        property = tempProperties[i];
                        if (property.Name == "UnpackMin")
                            UnpackNum++;

                        if (!properties.ContainsKey(property.Name))
                            properties.Add(property.Name, property);

                        switch (property.Name)
                        {
                            case "Format": texFormat = CSharpImageLibrary.General.ImageFormats.ParseDDSFormat((GameVersion == 3 ? property.Value.String2.Substring(3) : property.Value.StringValue)).InternalFormat; break;
                            case "TextureFileCacheName": arcName = property.Value.StringValue; break;
                            case "LODGroup": LODGroup = property.Value.StringValue; break;
                            case "CompressionSettings": Compression = property.Value.StringValue; break;
                            case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                        }
                    }

                    // if "None" property isn't found throws an exception
                    if (dataOffset == 0)
                        throw new Exception("\"None\" property not found");

                    if (GameVersion != 1 && !String.IsNullOrEmpty(arcName))
                        FullArcPath = GetTexArchive(PathBIOGame);

                    imageData = new byte[rawData.Length - dataOffset];
                    Buffer.BlockCopy(rawData, (int)dataOffset, imageData, 0, (int)(rawData.Length - dataOffset));
                }
                else
                    throw new InvalidDataException("Export isn't correct type. Got: " + exp.ClassName + "  Expected: " + class1 + " or " + class2 + " or " + class3);
            }
            else
                throw new KeyNotFoundException("Texture2D at " + pccExpID + " not found.");


            pccExpIdx = pccExpID;

            MemoryStream dataStream = UsefulThings.RecyclableMemoryManager.GetStream(imageData);  // FG: we will move forward with the memorystream (we are reading an export entry for a texture object data inside the pcc)
            imgList = new List<ImageInfo>();
            if (GameVersion != 3)
                dataStream.ReadUInt32FromStream(); //Current position in pcc
            Mips = (int)dataStream.ReadUInt32FromStream();                       // FG: 1st int32 (4 bytes / 32bits) is number of mipmaps
            int count = Mips;

            ArcDataSize = 0;
            while (dataStream.Position < dataStream.Length && count > 0)
            {
                ImageInfo imgInfo = new ImageInfo();                            // FG: store properties in ImageInfo struct (code at top)
                imgInfo.GameVersion = GameVersion;
                imgInfo.storageType = (PCCStorageType)dataStream.ReadInt32FromStream();       // FG: 2nd int32 storage type (see storage types above in enum_struct)
                imgInfo.UncSize = dataStream.ReadInt32FromStream();                    // FG: 3rd int32 uncompressed texture size
                imgInfo.CprSize = dataStream.ReadInt32FromStream();                    // FG: 4th int32 compressed texture size
                imgInfo.Offset = dataStream.ReadInt32FromStream();                     // FG: 5th int32 texture offset

                if (imgInfo.storageType == PCCStorageType.pccSto)
                {
                    imgInfo.Offset = (int)dataStream.Position;
                    dataStream.Seek(imgInfo.UncSize, SeekOrigin.Current);
                }
                else if (GameVersion != 1 && (imgInfo.storageType == PCCStorageType.arcCpr || imgInfo.storageType == PCCStorageType.arcUnc))
                {
                    ArcDataSize += imgInfo.UncSize;
                }
                else if (GameVersion == 1 && imgInfo.storageType == PCCStorageType.pccCpr)
                {
                    imgInfo.Offset = (int)dataStream.Position;
                    dataStream.Seek(imgInfo.CprSize, SeekOrigin.Current);
                }

                imgInfo.ImgSize = new ImageSize(dataStream.ReadUInt32FromStream(), dataStream.ReadUInt32FromStream());
                if (imgList.Exists(img => img.ImgSize == imgInfo.ImgSize))
                {
                    uint width = imgInfo.ImgSize.width;
                    uint height = imgInfo.ImgSize.height;
                    if (width == 4 && imgList.Exists(img => img.ImgSize.width == width))
                        width = imgList.Last().ImgSize.width / 2;
                    if (width == 0)
                        width = 1;
                    if (height == 4 && imgList.Exists(img => img.ImgSize.height == height))
                        height = imgList.Last().ImgSize.height / 2;
                    if (height == 0)
                        height = 1;
                    imgInfo.ImgSize = new ImageSize(width, height);
                    if (imgList.Exists(img => img.ImgSize == imgInfo.ImgSize))
                        throw new Exception("Duplicate image size found");
                }
                imgList.Add(imgInfo);
                count--;
            }

            // Grab the rest for the footer
            if (GameVersion != 1)
            {
                footerData = new byte[dataStream.Length - dataStream.Position];
                footerData = dataStream.ReadBytes(footerData.Length);
            }
            else
            {
                dataStream.Seek(-4, SeekOrigin.End);
                footerData = dataStream.ReadBytes(4);
            }

            // KFreon: Mips count weirdness
            //int tempmips = imgList.Count(t => t.Offset != -1);
            //Debug.WriteLineIf(tempmips != Mips, "Mips count weirdness");
            //Mips = tempmips;
            dataStream.Dispose();
        }

        #region Methods
        /// <summary>
        /// Clears imageData array.
        /// </summary>
        public void ClearImageData()
        {
            Array.Clear(imageData, 0, imageData.Length);
            imageData = null;
        }

        public byte[] ExtractImage(bool ProcessToDDS, int size = -1, ImageSize imgSize = null)
        {
            byte[] imgdata = null;
            ImageSize sizeToUse = imgSize;

            if (size == -1 && sizeToUse == null)
                imgdata = ExtractMaxImage(true, out sizeToUse);
            else
            {
                if (sizeToUse == null)
                {
                    if (imgList.Count != 1)
                        sizeToUse = imgList.Where(img => (img.ImgSize.width <= size || img.ImgSize.height <= size) && img.Offset != -1).Max(image => image.ImgSize);
                    else
                        sizeToUse = imgList.First().ImgSize;
                }

                imgdata = ExtractImage(sizeToUse, true);
            }

            if (imageData != null && ProcessToDDS)
                imgdata = ProcessIntoDDS(sizeToUse, imgdata);

            return imgdata;
        }


        /// <summary>
        /// Extract largest mipmap from file.
        /// </summary>
        /// <param name="NoOutput">False = save to disk.</param>
        /// <param name="archiveDir">TFC cache containing data. Leave null for automatic detection.</param>
        /// <param name="fileName">Filename to extract to. Valid only if NoOutput = false.</param>
        /// <returns>Image data.</returns>
        public byte[] ExtractMaxImage(bool NoOutput, out ImageSize maxImgSize, string archiveDir = null, string fileName = null)
        {
            // select max image size, excluding void images with offset = -1
            maxImgSize = imgList.Where(img => img.Offset != -1).Max(image => image.ImgSize);

            // KFreon: What if img not found?

            var maxsize = maxImgSize;

            // extracting max image
            return ExtractImage(imgList.Find(img => img.ImgSize == maxsize), NoOutput, archiveDir, fileName);
        }


        /// <summary>
        /// Gets image info for current texture's first valid mipmap.
        /// </summary>
        /// <returns>Image info of largest valid mipmap.</returns>
        public ImageInfo GenerateImageInfo()
        {
            ImageInfo imginfo = imgList.First(img => img.storageType != PCCStorageType.empty);
            imginfo.GameVersion = GameVersion;
            return imginfo;
        }

        public byte[] ExtractImage(ImageSize imgSize, bool NoOutput, string archiveDir = null, string fileName = null)
        {
            byte[] retval;
            if (imgList.Exists(img => img.ImgSize == imgSize))
                retval = ExtractImage(imgList.Find(img => img.ImgSize == imgSize), NoOutput, archiveDir, fileName);
            else
                throw new FileNotFoundException("Image with resolution " + imgSize + " not found");
            return retval;
        }

        public byte[] ExtractImage(ImageInfo imgInfo, bool NoOutput, string archiveDir = null, string fileName = null)
        {
            if (fileName == null)
                fileName = texName + "_" + imgInfo.ImgSize + ".dds";

            byte[] imgBuffer = null;

            switch (imgInfo.storageType)
            {
                case PCCStorageType.pccSto:
                    imgBuffer = new byte[imgInfo.UncSize];
                    Buffer.BlockCopy(imageData, imgInfo.Offset, imgBuffer, 0, imgInfo.UncSize);
                    break;
                case PCCStorageType.arcCpr:
                case PCCStorageType.arcUnc:
                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive(archiveDir);

                    if (archivePath == null)
                    {
                        Debug.WriteLine(fileName);
                        return null;
                    }

                    if (!File.Exists(archivePath))
                    {
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);
                    }

                    using (FileStream archiveStream = File.OpenRead(archivePath))
                    {
                        switch (GameVersion)
                        {
                            case 1:
                                ME1PCCObject temp = new ME1PCCObject(archivePath);
                                for (int i = 0; i < temp.ExportCount; i++)
                                {
                                    if (String.Compare(texName, temp.Exports[i].ObjectName, true) == 0 && temp.Exports[i].ValidTextureClass())
                                    {
                                        METexture2D temptex = new METexture2D(temp, i, PathBIOGame);
                                        imgBuffer = temptex.ExtractImage(imgInfo.ImgSize.ToString(), NoOutput, null, fileName);
                                    }
                                }
                                break;
                            case 2:
                                if (imgInfo.storageType == PCCStorageType.arcCpr)
                                {
                                    SaltLZOHelper lzohelp = new SaltLZOHelper();
                                    imgBuffer = lzohelp.DecompressTex(archiveStream, imgInfo.Offset, imgInfo.UncSize, imgInfo.CprSize);
                                }
                                else
                                {
                                    archiveStream.Seek(imgInfo.Offset, SeekOrigin.Begin);
                                    imgBuffer = new byte[imgInfo.UncSize];
                                    archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
                                }
                                break;
                            case 3:
                                archiveStream.Seek(imgInfo.Offset, SeekOrigin.Begin);
                                if (imgInfo.storageType == PCCStorageType.arcCpr)
                                    imgBuffer = ZBlock.Decompress(archiveStream, imgInfo.CprSize);
                                else
                                {
                                    imgBuffer = new byte[imgInfo.UncSize];
                                    archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
                                }
                                break;
                        }
                    }
                    break;
                case PCCStorageType.pccCpr:
                    using (MemoryStream ms = UsefulThings.RecyclableMemoryManager.GetStream(imageData))
                    {
                        SaltLZOHelper lzohelp = new SaltLZOHelper();
                        imgBuffer = lzohelp.DecompressTex(ms, imgInfo.Offset, imgInfo.UncSize, imgInfo.CprSize);
                    }
                    break;
                default:
                    throw new FormatException("Unsupported texture storage type");
            }

            return imgBuffer;
        }


        /// <summary>
        /// Extract image from file.
        /// </summary>
        /// <param name="strImgSize">Mip size to extract.</param>
        /// <param name="NoOutput">True = save to byte[], False = save to disk.</param>
        /// <param name="archiveDir">Path to TFC cache. Leave null for automatic detection.</param>
        /// <param name="fileName">Path to save extracted image. Valid only if NoOutput = false.</param>
        /// <returns>Image data.</returns>
        public byte[] ExtractImage(string strImgSize, bool NoOutput, string archiveDir = null, string fileName = null)
        {
            ImageSize ImgSize = ImageSize.stringToSize(strImgSize);
            return ExtractImage(ImgSize, NoOutput, archiveDir, fileName);
        }


        /// <summary>
        /// Replace mipmap in texture.
        /// </summary>
        /// <param name="strImgSize">Size of mip to replace.</param>
        /// <param name="imgFile">Details of replacing texture.</param>
        /// <param name="archiveDir">Path to TFC cache.</param>
        private void replaceImage(string strImgSize, ImageFile imgFile, string archiveDir)
        {
            ImageSize ImgSize = ImageSize.stringToSize(strImgSize);
            if (!imgList.Exists(img => img.ImgSize == ImgSize))
                throw new FileNotFoundException("Image with resolution " + ImgSize + " isn't found");

            int imageIdx = imgList.FindIndex(img => img.ImgSize == ImgSize);
            ImageInfo imgInfo = imgList[imageIdx];


            if (imgFile.imgSize.height != imgInfo.ImgSize.height || imgFile.imgSize.width != imgInfo.ImgSize.width)
                throw new FormatException("Incorrect input texture dimensions. Expected: " + imgInfo.ImgSize.ToString());

            ImageEngineFormat imgFileFormat = CSharpImageLibrary.General.ImageFormats.FindFormatInString(imgFile.format).InternalFormat;

            if (texFormat != imgFileFormat)
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());

            byte[] imgBuffer;

            // if the image is empty then recover the archive compression from the image list
            if (imgInfo.storageType == PCCStorageType.empty)
            {
                imgInfo.storageType = imgList.Find(img => img.storageType != PCCStorageType.empty && img.storageType != PCCStorageType.pccSto).storageType;
                imgInfo.UncSize = imgFile.resize().Length;
                imgInfo.CprSize = imgFile.resize().Length;
            }

            // overwrite previous choices for specific cases
            if (GameVersion != 1 && properties.ContainsKey("NeverStream") && properties["NeverStream"].Value.IntValue == 1)
                imgInfo.storageType = PCCStorageType.pccSto;


            switch (imgInfo.storageType)
            {
                case PCCStorageType.arcCpr:
                case PCCStorageType.arcUnc:
                    if (GameVersion == 1)
                        throw new NotImplementedException("Texture replacement not supported in external packages yet.");


                    string archivePath = FullArcPath;
                    if (String.IsNullOrEmpty(archivePath))
                        archivePath = GetTexArchive(archiveDir);
                    if (!File.Exists(archivePath))
                        throw new FileNotFoundException("Texture archive not found in " + archivePath);

                    imgBuffer = imgFile.imgData;

                    if (imgBuffer.Length != imgInfo.UncSize)
                        throw new FormatException("image sizes do not match, original is " + imgInfo.UncSize + ", new is " + imgBuffer.Length);

                    if (arcName.Length <= CustCache.Length || arcName.Substring(0, CustCache.Length) != CustCache) // Check whether existing texture is in a custom cache
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
                        if (imgInfo.storageType == PCCStorageType.arcCpr)
                        {
                            byte[] tempBuff;
                            SaltLZOHelper lzohelper = new SaltLZOHelper();
                            tempBuff = lzohelper.CompressTex(imgBuffer);
                            imgBuffer = new byte[tempBuff.Length];
                            Buffer.BlockCopy(tempBuff, 0, imgBuffer, 0, tempBuff.Length);
                            imgInfo.CprSize = imgBuffer.Length;
                        }
                        archiveStream.Write(imgBuffer, 0, imgBuffer.Length);

                        imgInfo.Offset = newOffset;
                    }
                    break;
                case PCCStorageType.pccSto:
                    imgBuffer = imgFile.resize();
                    using (MemoryStream dataStream = UsefulThings.RecyclableMemoryManager.GetStream())
                    {
                        dataStream.WriteBytes(imageData);
                        if (imgBuffer.Length <= imgInfo.UncSize && imgInfo.Offset > 0)
                            dataStream.Seek(imgInfo.Offset, SeekOrigin.Begin);
                        else
                            imgInfo.Offset = (int)dataStream.Position;
                        dataStream.WriteBytes(imgBuffer);
                        imgInfo.CprSize = imgBuffer.Length;
                        imgInfo.UncSize = imgBuffer.Length;
                        imageData = dataStream.ToArray();
                    }
                    break;
                case PCCStorageType.pccCpr:
                    using (MemoryStream dataStream = UsefulThings.RecyclableMemoryManager.GetStream())
                    {
                        dataStream.WriteBytes(imageData);
                        SaltLZOHelper lzohelper = new SaltLZOHelper();
                        imgBuffer = lzohelper.CompressTex(imgFile.resize());
                        if (imgBuffer.Length <= imgInfo.CprSize && imgInfo.Offset > 0)
                            dataStream.Seek(imgInfo.Offset, SeekOrigin.Begin);
                        else
                            imgInfo.Offset = (int)dataStream.Position;
                        dataStream.WriteBytes(imgBuffer);
                        imgInfo.CprSize = imgBuffer.Length;
                        imgInfo.UncSize = imgFile.resize().Length;
                        imageData = dataStream.ToArray();
                    }
                    break;
            }

            imgList[imageIdx] = imgInfo;
        }


        /// <summary>
        /// Gets texture archive for current instance, using a base directory. Returns null if not found.
        /// </summary>
        /// <param name="dir">Base directory to start looking from.</param>
        /// <returns>Path to texture archive.</returns>
        public String GetTexArchive(string dir)
        {
            if (GameVersion != 1)
            {
                if (arcName == null)
                    return null;

                List<string> arclist = null;
                if (GameVersion == 3)
                    arclist = ME3Directory.Archives;
                else if (GameVersion == 2)
                    arclist = ME2Directory.Archives;


                foreach (String arc in arclist)
                {
                    if (String.Compare(Path.GetFileNameWithoutExtension(arc), arcName, true) == 0)
                        return arc;
                }
            }
            else
            {
                List<string> allFiles = new List<string>(GameVersion == 1 ? ME1Directory.BaseGameFiles : ME2Directory.BaseGameFiles);
                allFiles.AddRange(GameVersion == 1 ? ME1Directory.DLCFiles : ME2Directory.DLCFiles);

                string package = FullPackage.Split('.')[0];
                for (int i = 0; i < allFiles.Count; i++)
                {
                    string[] parts = allFiles[i].Split('\\');
                    string tempFile = parts.Last().Split('.')[0];
                    if (String.Compare(package, tempFile, true) == 0)
                        return allFiles[i];
                }

                for (int i = 0; i < allFiles.Count; i++)
                {
                    AbstractPCCObject temp = AbstractPCCObject.Create(allFiles[i], GameVersion, PathBIOGame);
                    for (int j = 0; j < temp.ExportCount; j++)
                    {
                        AbstractExportEntry exp = temp.Exports[j];
                        if (String.Compare(texName, exp.ObjectName, true) == 0 && exp.ClassName == "Texture2D")
                        {
                            METexture2D temptex = new METexture2D(temp, j, PathBIOGame);
                            if (temptex.imgList[0].storageType == PCCStorageType.pccCpr || temptex.imgList[0].storageType == PCCStorageType.pccSto)
                            {
                                return allFiles[i];
                            }
                        }
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// Upscales texture and changes texture.
        /// </summary>
        /// <param name="im">New image to upgrade.</param>
        /// <param name="archiveDir">Path to TFC cache containing current texture.</param>
        private void addBiggerImage(ImageFile im, string archiveDir)
        {
            ImageSize biggerImageSizeOnList = imgList.Max(image => image.ImgSize);
            // check if replacing image is supported
            ImageFile imgFile = im;

            ImageEngineFormat imgFileFormat = CSharpImageLibrary.General.ImageFormats.FindFormatInString(imgFile.format).InternalFormat;
            if (texFormat != imgFileFormat)
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());

            // check if image to add is valid
            if (biggerImageSizeOnList.width * 2 != imgFile.imgSize.width || biggerImageSizeOnList.height * 2 != imgFile.imgSize.height)
                throw new FormatException("image size " + imgFile.imgSize + " isn't valid, must be " + new ImageSize(biggerImageSizeOnList.width * 2, biggerImageSizeOnList.height * 2));

            if (imgList.Count <= 1)
                throw new Exception("Unable to add image, texture must have more than one image present");

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = imgList.Find(img => img.storageType != PCCStorageType.empty && img.storageType != PCCStorageType.pccSto).storageType;
            // for additional mipmaps keep them in external archive but only when
            // texture allready have such property
            if (properties.ContainsKey("TextureFileCacheName"))
                newImgInfo.storageType = PCCStorageType.arcCpr;
            newImgInfo.ImgSize = imgFile.imgSize;
            newImgInfo.UncSize = imgFile.resize().Length;
            newImgInfo.CprSize = 0x00; // not yet filled
            newImgInfo.Offset = 0x00; // not yet filled
            imgList.Insert(0, newImgInfo); // insert new image on top of the list

            //now I let believe the program that I'm doing an image replace, saving lot of code ;)
            replaceImage(newImgInfo.ImgSize.ToString(), im, archiveDir);

            //updating num of images
            Mips++;

            // update MipTailBaseIdx
            int propVal = properties["MipTailBaseIdx"].Value.IntValue;
            propVal++;
            properties["MipTailBaseIdx"].Value.IntValue = propVal;

            // update Sizes
            properties["SizeX"].Value.IntValue = (int)newImgInfo.ImgSize.width;
            properties["SizeY"].Value.IntValue = (int)newImgInfo.ImgSize.height;
        }

        /// <summary>
        /// Upscales a texture containing only a single image, and replaces with a new one.
        /// </summary>
        /// <param name="imgFile">New image to replace with.</param>
        /// <param name="archiveDir">Path to TFC cache.</param>
        private void NoMipsTextureUpscale(string archiveDir, byte[] imgData)
        {
            ImageFile imgFile = General.LoadAKImageFile(imgData);

            if (texFormat.ToString() != imgFile.format)
                throw new FormatException("Different image format, original is " + texFormat + ", new is " + imgFile.subtype());

            // !!! warning, this method breaks consistency between imgList and imageData[] !!!
            ImageInfo newImgInfo = new ImageInfo();
            newImgInfo.storageType = imgList.Find(img => img.storageType != PCCStorageType.empty && img.storageType != PCCStorageType.pccSto).storageType;
            newImgInfo.ImgSize = imgFile.imgSize;
            newImgInfo.UncSize = imgFile.resize().Length;
            newImgInfo.CprSize = 0x00; // not yet filled
            newImgInfo.Offset = 0x00; // not yet filled
            imgList.RemoveAt(0);  // Remove old single image and add new one
            imgList.Add(newImgInfo);

            //now I let believe the program that I'm doing an image replace, saving lot of code ;)
            replaceImage(newImgInfo.ImgSize.ToString(), imgFile, archiveDir);

            // update Sizes
            properties["SizeX"].Value.IntValue = (int)newImgInfo.ImgSize.width;
            properties["SizeY"].Value.IntValue = (int)newImgInfo.ImgSize.height;
        }


        /// <summary>
        /// One stop shop for replacing/upscaling images. Saltisgood wrote this.
        /// </summary>
        /// <param name="im">New image to replace with.</param>
        /// <param name="archiveDir">Path to TFC cache.</param>
        /// <param name="imgData"></param>
        public void OneImageToRuleThemAll(string archiveDir, byte[] imgData)
        {
            // KFreon: If only 1 mipmap, use different function
            if (imgList.Count <= 1)
                NoMipsTextureUpscale(archiveDir, imgData);
            else
            {
                // KFreon: Upscale and replace everything.
                ImageMipMapHandler imgMipMap = new ImageMipMapHandler("", imgData);

                var tempimagesize = imgMipMap.imageList[0].imgSize;
                if (!UsefulThings.General.IsPowerOfTwo(tempimagesize.width) || !UsefulThings.General.IsPowerOfTwo(tempimagesize.height))
                    throw new InvalidDataException(String.Format("Image dimensions must be powers of two. Got {0}x{1}", tempimagesize.width, tempimagesize.height));

                if (Class == class2 || Class == class3)
                    Console.WriteLine();

                // starts from the smaller image
                for (int i = imgMipMap.imageList.Count - 1; i >= 0; i--)
                {
                    ImageFile newImageFile = imgMipMap.imageList[i];
                    ImageEngineFormat newImageFileFormat = CSharpImageLibrary.General.ImageFormats.FindFormatInString(newImageFile.format).InternalFormat;
                    if (texFormat != newImageFileFormat)
                        throw new FormatException("Different image format, original is " + texFormat + ", new is " + newImageFile.subtype());

                    // if the image size exists inside the ME1Texture2D image list then we have to replace it
                    if (imgList.Exists(img => img.ImgSize == newImageFile.imgSize))
                    {
                        // ...but at least for now I can reuse my replaceImage function... ;)
                        replaceImage(newImageFile.imgSize.ToString(), newImageFile, archiveDir);
                    }
                    else if (newImageFile.imgSize.width > imgList[0].ImgSize.width) // if the image doesn't exists then we have to add it
                    {
                        // ...and use my addBiggerImage function! :P
                        addBiggerImage(newImageFile, archiveDir);
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                    // else ignore the image
                }

                // Remove higher res versions and fix up properties
                while (imgList[0].ImgSize.width > imgMipMap.imageList[0].imgSize.width)
                {
                    imgList.RemoveAt(0);
                    Mips--;
                }
                if (properties.ContainsKey("SizeX"))
                    properties["SizeX"].Value.IntValue = (int)imgList[0].ImgSize.width;
                if (properties.ContainsKey("SizeY"))
                    properties["SizeY"].Value.IntValue = (int)imgList[0].ImgSize.height;
                if (properties.ContainsKey("MipTailBaseIdx"))
                    properties["MipTailBaseIdx"].Value.IntValue = imgList.Count + 1;
            }
        }


        public void CopyImgList(METexture2D inTex, AbstractPCCObject pcc)
        {
            Mips = inTex.Mips;

            if (GameVersion == 1)
            {
                List<ImageInfo> tempList = new List<ImageInfo>();
                MemoryStream tempData = UsefulThings.RecyclableMemoryManager.GetStream();
                SaltLZOHelper lzo = new SaltLZOHelper();
                Mips = inTex.Mips;

                // forced norenderfix
                // norender = true;

                int type = -1;
                if (imgList.Exists(img => img.storageType == PCCStorageType.arcCpr) && imgList.Count > 1)
                    type = 1;
                else if (imgList.Exists(img => img.storageType == PCCStorageType.pccCpr))
                    type = 2;
                else if (imgList.Exists(img => img.storageType == PCCStorageType.pccSto) || imgList.Count == 1)
                    type = 3;

                switch (type)
                {
                    case 1:
                        for (int i = 0; i < inTex.imgList.Count; i++)
                        {
                            try
                            {
                                ImageInfo newImg = new ImageInfo();
                                ImageInfo replaceImg = inTex.imgList[i];
                                PCCStorageType replaceType = imgList.Find(img => img.ImgSize == replaceImg.ImgSize).storageType;

                                int j = 0;
                                while (replaceType == PCCStorageType.empty)
                                {
                                    j++;
                                    replaceType = imgList[imgList.FindIndex(img => img.ImgSize == replaceImg.ImgSize) + j].storageType;
                                }

                                if (replaceType == PCCStorageType.arcCpr || !imgList.Exists(img => img.ImgSize == replaceImg.ImgSize))
                                {
                                    newImg.storageType = PCCStorageType.arcCpr;
                                    newImg.UncSize = replaceImg.UncSize;
                                    newImg.CprSize = replaceImg.CprSize;
                                    newImg.ImgSize = replaceImg.ImgSize;
                                    newImg.Offset = (int)(replaceImg.Offset + inTex.pccOffset + inTex.dataOffset);
                                }
                                else
                                {
                                    newImg.storageType = PCCStorageType.pccSto;
                                    newImg.UncSize = replaceImg.UncSize;
                                    newImg.CprSize = replaceImg.UncSize;
                                    newImg.ImgSize = replaceImg.ImgSize;
                                    newImg.Offset = (int)(tempData.Position);
                                    using (MemoryStream tempStream = UsefulThings.RecyclableMemoryManager.GetStream(inTex.imageData))
                                    {
                                        tempData.WriteBytes(lzo.DecompressTex(tempStream, replaceImg.Offset, replaceImg.UncSize, replaceImg.CprSize));
                                    }
                                }
                                tempList.Add(newImg);
                            }
                            catch
                            {
                                ImageInfo replaceImg = inTex.imgList[i];
                                if (!imgList.Exists(img => img.ImgSize == replaceImg.ImgSize))
                                    throw new Exception("An error occurred during imglist copying and no suitable replacement was found");
                                ImageInfo newImg = imgList.Find(img => img.ImgSize == replaceImg.ImgSize);
                                if (newImg.storageType != PCCStorageType.pccCpr && newImg.storageType != PCCStorageType.pccSto)
                                    throw new Exception("An error occurred during imglist copying and no suitable replacement was found");
                                int temppos = newImg.Offset;
                                newImg.Offset = (int)tempData.Position;
                                tempData.Write(imageData, temppos, newImg.CprSize);
                                tempList.Add(newImg);
                            }
                        }
                        break;
                    case 2:
                        for (int i = 0; i < inTex.imgList.Count; i++)
                        {
                            ImageInfo newImg = new ImageInfo();
                            ImageInfo replaceImg = inTex.imgList[i];
                            newImg.storageType = PCCStorageType.pccCpr;
                            newImg.UncSize = replaceImg.UncSize;
                            newImg.CprSize = replaceImg.CprSize;
                            newImg.ImgSize = replaceImg.ImgSize;
                            newImg.Offset = (int)(tempData.Position);
                            byte[] buffer = new byte[newImg.CprSize];
                            Buffer.BlockCopy(inTex.imageData, replaceImg.Offset, buffer, 0, buffer.Length);
                            tempData.WriteBytes(buffer);
                            tempList.Add(newImg);
                        }
                        break;
                    case 3:
                        for (int i = 0; i < inTex.imgList.Count; i++)
                        {
                            ImageInfo newImg = new ImageInfo();
                            ImageInfo replaceImg = inTex.imgList[i];
                            newImg.storageType = PCCStorageType.pccSto;
                            newImg.UncSize = replaceImg.UncSize;
                            newImg.CprSize = replaceImg.UncSize;
                            newImg.ImgSize = replaceImg.ImgSize;
                            newImg.Offset = (int)(tempData.Position);
                            if (replaceImg.storageType == PCCStorageType.pccCpr)
                            {
                                using (MemoryStream tempStream = UsefulThings.RecyclableMemoryManager.GetStream(inTex.imageData))
                                {
                                    tempData.WriteBytes(lzo.DecompressTex(tempStream, replaceImg.Offset, replaceImg.UncSize, replaceImg.CprSize));
                                }
                            }
                            else if (replaceImg.storageType == PCCStorageType.pccSto)
                            {
                                byte[] buffer = new byte[newImg.CprSize];
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
                    if (inTex.imgList[i].storageType == PCCStorageType.empty)
                        tempinfo.storageType = PCCStorageType.empty;
                    tempList[i] = tempinfo;
                }

                imageData = tempData.ToArray();
                imgList = tempList;
            }
            else
            {
                imageData = inTex.imageData;
                imgList = inTex.imgList;
            }



            //Copy Properties
            byte[] buff = ToArray(pcc, 0);

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
                        texFormat = CSharpImageLibrary.General.ImageFormats.ParseDDSFormat(pcc.Names[property.Value.IntValue].Substring(3)).InternalFormat;
                        break;
                    case "TextureFileCacheName": arcName = pcc.Names[property.Value.IntValue]; break;
                    case "LODGroup": LODGroup = pcc.Names[property.Value.IntValue]; break; //
                    case "None": dataOffset = (uint)(property.offsetval + property.Size); break;
                }
            }

            // if "None" property isn't found throws an exception
            if (dataOffset == 0)
                throw new Exception("\"None\" property not found");
        }


        public byte[] ToArray(AbstractPCCObject pcc, uint pccExportDataOffset, bool SkipEmpties = false)
        {
            using (MemoryStream buffer = UsefulThings.RecyclableMemoryManager.GetStream())
            {
                buffer.WriteBytes(headerData);

                if (properties.ContainsKey("LODGroup"))
                {
                    if (GameVersion != 3)
                    {
                        properties["LODGroup"].Value.StringValue = "TEXTUREGROUP_LightAndShadowMap";
                        properties["LODGroup"].Value.String2 = pcc.Names[0];
                    }
                    else
                        properties["LODGroup"].Value.String2 = "TEXTUREGROUP_Shadowmap";
                }
                else
                {
                    buffer.WriteValueS64(pcc.AddName("LODGroup"));
                    buffer.WriteValueS64(pcc.AddName("ByteProperty"));
                    buffer.WriteValueS64(8);
                    if (GameVersion == 3)
                        buffer.WriteValueS64(pcc.AddName("TextureGroup"));

                    if (GameVersion != 1)
                    {
                        buffer.WriteValueS64(pcc.AddName("TEXTUREGROUP_Shadowmap"));
                        buffer.WriteValueS64(pcc.AddName(GameVersion == 2 ? "TEXTUREGROUP_LightAndShadowMap" : "TEXTUREGROUP_Shadowmap"));
                    }
                    else
                        buffer.WriteValueS32(pcc.AddName("TEXTUREGROUP_LightAndShadowMap"));

                    if (GameVersion == 1)
                        buffer.WriteValueS32(1025);
                }


                foreach (KeyValuePair<string, SaltPropertyReader.Property> kvp in properties)
                {
                    SaltPropertyReader.Property prop = kvp.Value;

                    if (prop.Name == "UnpackMin")
                    {
                        for (int j = 0; j < UnpackNum; j++)
                        {
                            buffer.WriteValueS64(pcc.AddName(prop.Name));
                            buffer.WriteValueS64(pcc.AddName(prop.TypeVal.ToString()));
                            buffer.WriteValueS32(prop.Size);
                            buffer.WriteValueS32(j);
                            buffer.WriteValueF32(prop.Value.FloatValue, Endian.Little);
                        }
                        continue;
                    }

                    buffer.WriteValueS64(pcc.AddName(prop.Name));
                    if (prop.Name == "None")
                    {
                        if (GameVersion != 3)
                            for (int j = 0; j < 12; j++)
                                buffer.WriteByte(0);
                    }
                    else
                    {
                        buffer.WriteValueS64(pcc.AddName(prop.TypeVal.ToString()));
                        buffer.WriteValueS64(prop.Size);


                        switch (prop.TypeVal)
                        {
                            case SaltPropertyReader.Type.IntProperty:
                                buffer.WriteValueS32(prop.Value.IntValue);
                                break;
                            case SaltPropertyReader.Type.BoolProperty:
                                if (GameVersion == 2)
                                    buffer.WriteValueS32(prop.Value.IntValue);
                                else if (GameVersion == 3)
                                    buffer.WriteValueBoolean(prop.Value.Boolereno);
                                else
                                {
                                    buffer.Seek(-4, SeekOrigin.Current);
                                    buffer.WriteValueS32(prop.Value.IntValue);
                                    buffer.Seek(4, SeekOrigin.Current);
                                }
                                break;
                            case SaltPropertyReader.Type.NameProperty:
                                buffer.WriteValueS64(pcc.AddName(prop.Value.StringValue));
                                break;
                            case SaltPropertyReader.Type.StrProperty:
                                buffer.WriteValueS32(prop.Value.StringValue.Length + 1);
                                foreach (char c in prop.Value.StringValue)
                                    buffer.WriteByte((byte)c);
                                buffer.WriteByte(0);
                                break;
                            case SaltPropertyReader.Type.StructProperty:
                                string strVal = prop.Value.StringValue;
                                if (prop.Name.Contains("guid", StringComparison.OrdinalIgnoreCase))
                                    strVal = "Guid";

                                buffer.WriteValueS64(pcc.AddName(strVal));
                                foreach (SaltPropertyReader.PropertyValue value in prop.Value.Array)
                                    if (GameVersion == 3)
                                        buffer.WriteByte((byte)value.IntValue);
                                    else
                                        buffer.WriteValueS32(value.IntValue);
                                break;
                            case SaltPropertyReader.Type.ByteProperty:
                                if (GameVersion == 3)
                                {
                                    buffer.WriteValueS64(pcc.AddName(prop.Value.StringValue));
                                    buffer.WriteValueS64(pcc.AddName(prop.Value.String2));
                                }
                                else
                                {
                                    buffer.WriteValueS32(pcc.AddName(prop.Value.StringValue));
                                    if (GameVersion == 2)
                                        buffer.WriteValueS32(pcc.AddName(prop.Value.String2));
                                    else
                                        buffer.WriteValueS32(prop.Value.IntValue);
                                }

                                break;
                            case SaltPropertyReader.Type.FloatProperty:
                                buffer.WriteValueF32(prop.Value.FloatValue, Endian.Little);
                                break;
                            default:
                                throw new NotImplementedException("Property type: " + prop.TypeVal.ToString() + ", not yet implemented. TELL ME ABOUT THIS!");
                        }
                    }
                }

                if (SkipEmpties)
                    return buffer.ToArray();

                if (GameVersion == 2)
                    buffer.WriteValueS32((int)buffer.Position + (int)pccExportDataOffset);
                else if (GameVersion == 1)
                    buffer.WriteValueS32((int)(pccOffset + buffer.Position + 4));

                //Remove empty textures
                List<ImageInfo> tempList = new List<ImageInfo>();
                foreach (ImageInfo imgInfo in imgList)
                {
                    if (imgInfo.storageType != PCCStorageType.empty)
                        tempList.Add(imgInfo);
                }
                imgList = tempList;
                Mips = imgList.Count;

                buffer.WriteValueU32((uint)Mips);

                foreach (ImageInfo imgInfo in imgList)
                {
                    buffer.WriteValueS32((int)imgInfo.storageType);
                    buffer.WriteValueS32(imgInfo.UncSize);
                    buffer.WriteValueS32(imgInfo.CprSize);

                    if (imgInfo.storageType == PCCStorageType.pccSto)
                    {
                        if (GameVersion == 2)
                            buffer.WriteValueS32((int)(buffer.Position + pccExportDataOffset));
                        else
                            buffer.WriteValueS32((int)(imgInfo.Offset + pccExportDataOffset + dataOffset));
                        buffer.Write(imageData, imgInfo.Offset, imgInfo.UncSize);
                    }
                    else if (GameVersion == 1 && imgInfo.storageType == PCCStorageType.pccCpr)
                    {
                        buffer.WriteValueS32((int)(imgInfo.Offset + pccExportDataOffset + dataOffset));
                        buffer.Write(imageData, imgInfo.Offset, imgInfo.CprSize);
                    }
                    else
                        buffer.WriteValueS32(imgInfo.Offset);

                    if (imgInfo.ImgSize.width < 4)
                        buffer.WriteValueU32(4);
                    else
                        buffer.WriteValueU32(imgInfo.ImgSize.width);

                    if (imgInfo.ImgSize.height < 4)
                        buffer.WriteValueU32(4);
                    else
                        buffer.WriteValueU32(imgInfo.ImgSize.height);

                }

                buffer.WriteBytes(footerData);
                return buffer.ToArray();
            }
        }

        public void LowResFix()
        {
            LowResFix(1);
        }

        public void LowResFix(int MipsToKeep = 1)
        {
            while (imgList[0].storageType == PCCStorageType.empty)
            {
                Mips--;
                imgList.RemoveAt(0);
            }

            while (imgList.Count > MipsToKeep)
            {
                Mips--;
                imgList.Remove(imgList.Last());
            }

            Mips = MipsToKeep;
            if (properties.ContainsKey("MipTailBaseIdx"))
                properties["MipTailBaseIdx"].Value.IntValue = 0;
            if (properties.ContainsKey("SizeX"))
                properties["SizeX"].Value.IntValue = (int)imgList[0].ImgSize.width;
            if (properties.ContainsKey("SizeY"))
                properties["SizeY"].Value.IntValue = (int)imgList[0].ImgSize.height;
        }

        public string GetDetailsAsString()
        {
            StringBuilder sb = new StringBuilder();
            ImageInfo info = GenerateImageInfo();
            sb.AppendLine("Texture Name:  " + texName);
            sb.AppendLine("Format:  " + texFormat.ToString());
            sb.AppendLine("Width:  " + info.ImgSize.width + ",  Height:  " + info.ImgSize.height);
            sb.AppendLine("LODGroup:  " + (hasChanged ? "TEXTUREGROUP_Shadowmap" : ((String.IsNullOrEmpty(LODGroup) ? "None (Uses World)" : LODGroup))));
            sb.AppendLine("Texmod Hash:  " + General.FormatTexmodHashAsString(Hash));
            sb.AppendLine("Num Mips: " + Mips);

            if (GameVersion != 1)
                sb.AppendLine("Texture Cache File:  " + (String.IsNullOrEmpty(arcName) ? "PCC Stored" : arcName + ".tfc"));

            return sb.ToString();
        }


        #region Texture Cache Stuff
        public void AddToTOC(String filename, String biopath)
        {
            if (GameVersion == 1)
                throw new NotImplementedException("This operation is only valid for ME2 and ME3 as this is used only for cache creation.");


            List<string> parts = new List<string>(biopath.Split('\\'));
            parts.Remove("");
            if (parts[parts.Count - 1] == "CookedPCConsole")
            {
                parts.RemoveAt(parts.Count - 1);
                biopath = String.Join("\\", parts);
            }
            if (!File.Exists(Path.Combine(biopath, "PCConsoleTOC.bin")))
                throw new FileNotFoundException("TOC.bin not found at: " + Path.Combine(biopath, "PCConsoleTOC.bin"));

            AmaroK86.MassEffect3.TOCHandler tochandler = new AmaroK86.MassEffect3.TOCHandler(Path.Combine(biopath, "PCConsoleTOC.bin"), Path.GetDirectoryName(biopath) + @"\");
            if (tochandler.existsFile(filename))
                return;

            int minval = 100000;
            int minid = 0;
            for (int i = 0; i < tochandler.chunkList.Count; i++)
            {
                if (tochandler.chunkList[i].fileList == null || tochandler.chunkList[i].fileList.Count == 0)
                    continue;
                if (tochandler.chunkList[i].fileList.Count < minval)
                {
                    minval = tochandler.chunkList[i].fileList.Count;
                    minid = i;
                }
            }
            tochandler.addFile(filename, minid);
            tochandler.saveToFile(true);
        }

        public void MakeCache(String filename, String biopath)
        {
            if (GameVersion == 1)
                throw new NotImplementedException("This operation is only valid for ME2 and ME3 as ME1 doesn't use caches like this.");

            Random r = new Random();

            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < 4; i++)
                    fs.WriteValueS32(r.Next());
            }

            if (GameVersion == 3)
                AddToTOC(filename, biopath);
        }

        public void ChooseNewCache(string bioPath, int buffLength)
        {
            if (GameVersion == 1)
                throw new NotImplementedException("This operation is only valid for ME2 and ME3 as ME1 doesn't use caches like this.");

            string cookedName = GameVersion == 3 ? "CookedPCConsole" : "CookedPC";

            int i = 0;
            while (true)
            {
                FileInfo cacheInfo;
                List<string> parts = new List<string>(bioPath.Split('\\'));
                parts.Remove("");
                if (parts[parts.Count - 1] == cookedName)
                    cacheInfo = new FileInfo(Path.Combine(bioPath, CustCache + i + ".tfc"));
                else
                    cacheInfo = new FileInfo(Path.Combine(bioPath, cookedName, CustCache + i + ".tfc"));


                if (!cacheInfo.Exists)
                {
                    MakeCache(cacheInfo.FullName, bioPath);
                    List<string> parts1 = new List<string>(bioPath.Split('\\'));
                    parts1.Remove("");
                    if (parts1[parts1.Count - 1] == cookedName)
                        MoveCaches(bioPath, CustCache + i + ".tfc");
                    else
                        MoveCaches(bioPath + "\\" + cookedName, CustCache + i + ".tfc");

                    properties["TextureFileCacheName"].Value.StringValue = CustCache + i;
                    arcName = CustCache + i;
                    FullArcPath = cacheInfo.FullName;

                    // KFreon: Add to archives list
                    if (GameVersion == 2)
                        ME2Directory.Archives.Add(cacheInfo.FullName);
                    else if (GameVersion == 3)
                        ME3Directory.Archives.Add(cacheInfo.FullName);

                    return;
                }
                else if (cacheInfo.Length + buffLength + ArcDataSize < 0x80000000)
                {
                    List<string> parts1 = new List<string>(bioPath.Split('\\'));
                    parts1.Remove("");
                    if (parts1[parts1.Count - 1] == cookedName)
                        MoveCaches(bioPath, CustCache + i + ".tfc");
                    else
                        MoveCaches(bioPath + "\\" + cookedName, CustCache + i + ".tfc");
                    properties["TextureFileCacheName"].Value.StringValue = CustCache + i;
                    arcName = CustCache + i;
                    FullArcPath = cacheInfo.FullName;
                    return;
                }
                i++;
            }
        }

        public void MoveCaches(string cookedPath, string NewCache)
        {
            if (GameVersion == 1)
                throw new NotImplementedException("This operation is only valid for ME2 and ME3 as ME1 doesn't use caches like this.");

            //Fix the GUID
            using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Open, FileAccess.Read))
            {
                SaltPropertyReader.Property GUIDProp = properties["TFCFileGuid"];

                for (int i = 0; i < (GameVersion == 3 ? 16 : 4); i++)
                {
                    SaltPropertyReader.PropertyValue tempVal = GUIDProp.Value.Array[i];
                    tempVal.IntValue = GameVersion == 3 ? newCache.ReadByte() : newCache.ReadInt32FromStream();
                    GUIDProp.Value.Array[i] = tempVal;
                }
            }


            //Move across any existing textures
            using (FileStream oldCache = new FileStream(FullArcPath, FileMode.Open, FileAccess.Read))
            {
                using (FileStream newCache = new FileStream(Path.Combine(cookedPath, NewCache), FileMode.Append, FileAccess.Write))
                {
                    for (int i = 0; i < imgList.Count; i++)
                    {
                        ImageInfo img = imgList[i];

                        switch (img.storageType)
                        {
                            case PCCStorageType.arcCpr:
                                byte[] buff = new byte[img.CprSize];
                                oldCache.Seek(img.Offset, SeekOrigin.Begin);
                                Buffer.BlockCopy(oldCache.ReadBytes(img.CprSize), 0, buff, 0, img.CprSize);
                                img.Offset = (int)newCache.Position;
                                newCache.WriteBytes(buff);
                                break;
                            case PCCStorageType.arcUnc:
                                buff = new byte[img.UncSize];
                                oldCache.Seek(img.Offset, SeekOrigin.Begin);
                                Buffer.BlockCopy(oldCache.ReadBytes(img.CprSize), 0, buff, 0, img.CprSize);
                                img.Offset = (int)newCache.Position;
                                newCache.WriteBytes(buff);
                                break;
                            case PCCStorageType.pccSto:
                                break;
                            case PCCStorageType.empty:
                                break;
                            default:
                                throw new NotImplementedException("Storage type not supported yet");
                        }
                        imgList[i] = img;
                    }
                }
            }
        }
        #endregion Texture Cache Stuff
        #endregion Methods

        public uint GenerateHash(ImageInfo info, out byte[] imgData)
        {
            uint hash = 0;

            // KFreon: Gets raw image data from files
            imgData = ExtractImage(false, imgSize: info.ImgSize);
            if (imgData == null)
                return 0;

            CRC32 crcgen = new CRC32();
            if (info.storageType == PCCStorageType.pccSto)
            {
                if (texFormat != ImageEngineFormat.DDS_ATI2_3Dc)
                    hash = ~crcgen.BlockChecksum(imgData);
                else
                    hash = ~crcgen.BlockChecksum(imgData, 0, info.UncSize / 2);
            }
            else
            {
                if (imgData != null)
                {
                    if (texFormat != ImageEngineFormat.DDS_ATI2_3Dc)
                        hash = ~crcgen.BlockChecksum(imgData);
                    else
                        hash = ~crcgen.BlockChecksum(imgData, 0, info.UncSize / 2);
                }
            }

            // KFreon: Process into DDS format for use later
            imgData = ProcessIntoDDS(info.ImgSize, imgData);

            return hash;
        }

        internal int GetExpID(string pccName)
        {
            return PCCs.Where(pcc => pcc.File == pccName).Select(t => t.ExpID).First();
        }

        private byte[] ProcessIntoDDS(ImageSize size, byte[] data)
        {
            var header = CSharpImageLibrary.General.DDSGeneral.Build_DDS_Header(Mips, (int)size.height, (int)size.width, texFormat);
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                    CSharpImageLibrary.General.DDSGeneral.Write_DDS_Header(header, bw);

                ms.Write(data, 0, data.Length);

                return ms.ToArray();
            }
        }
    }
}
