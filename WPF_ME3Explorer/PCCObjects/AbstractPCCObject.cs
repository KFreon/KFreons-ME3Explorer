using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using AmaroK86.MassEffect3.ZlibBlock;
using CSharpImageLibrary;
using Gibbed.IO;
using UsefulThings;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.PCCObjects;
using WPF_ME3Explorer.PCCObjects.ImportEntries;
using WPF_ME3Explorer.Textures;
using BitConverter = WPF_ME3Explorer.BitConverter;

namespace WPF_ME3Explorer.PCCObjects
{
    /// <summary>
    /// Base class for PCCObject handling.
    /// </summary>
    public abstract class AbstractPCCObject : IDisposable
    {
        public struct NameEntry
        {
            public string name;
            public int Unk;
            public int flags;
        }

        public class Block
        {
            public int uncOffset;
            public int uncSize;
            public int cprOffset;
            public int cprSize;
            public bool bRead = false;
        }

        #region Creation
        /// <summary>
        /// Creates a PCCObject from a file.
        /// </summary>
        /// <param name="file">PCC file to create object from.</param>
        /// <param name="gameversion">Game version.</param>
        /// <param name="pathbiogame">Path to BIOGame folder</param>
        /// <returns>IPCCObject from file.</returns>
        public static AbstractPCCObject Create(string file, int gameversion, string pathbiogame)
        {
            AbstractPCCObject pcc;

            // KFreon: Use different methods for each game.
            if (gameversion == 1)
                pcc = new ME1PCCObject(file);
            else if (gameversion == 2)
                pcc = new ME2PCCObject(file);
            else if (gameversion == 3)
                pcc = new ME3PCCObject(file);
            else
            {
                throw new ArgumentOutOfRangeException("GameVersion parameter must be between 1 and 3.");
            }

            pcc.PathBIOGame = pathbiogame;
            return pcc;
        }


        /// <summary>
        /// Creates PCCObject from stream.
        /// </summary>
        /// <param name="file">Name of original file.</param>
        /// <param name="stream">Stream of data.</param>
        /// <param name="gameversion">Target game.</param>
        /// <param name="pathbiogame">Path to BIOGame folder</param>
        /// <returns>IPCCObject from data.</returns>
        public static AbstractPCCObject Create(string file, MemoryStream stream, int gameversion, string pathbiogame)
        {
            AbstractPCCObject pcc;
            if (gameversion == 1)
                pcc = new ME1PCCObject(file, stream);
            else if (gameversion == 2)
                pcc = new ME2PCCObject(file, stream);
            else if (gameversion == 3)
                pcc = new ME3PCCObject(file, stream);
            else
            {
                throw new ArgumentOutOfRangeException("GameVersion must be between 1 and 3.");
            }

            pcc.PathBIOGame = pathbiogame;
            return pcc;
        }

        public static AbstractPCCObject Create_AttemptGuessGame(string file, MEDirectories.MEDirectories MEExDirecs)
        {
            AbstractPCCObject pcc = null;

            for (int i = 1; i < 4; i++)
            {
                try
                {
                    pcc = Create(file, i, MEExDirecs.GetDifferentPathBIOGame(i));
                }
                catch (ArgumentOutOfRangeException e) when (e.Message.Contains("GameVersion"))
                {
                    DebugOutput.PrintLn($"Guessed game: {i} for: {file}. Wrong guess though.");
                    continue;
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                }
            }

            return pcc;
        }

        public static AbstractPCCObject Create_AttemptGuessGame(string file, MemoryStream stream, MEDirectories.MEDirectories MEExDirecs)
        {
            AbstractPCCObject pcc = null;

            for (int i = 1; i < 4; i++)
            {
                try
                {
                    pcc = Create(file, stream, i, MEExDirecs.GetDifferentPathBIOGame(i));
                }
                catch (ArgumentOutOfRangeException e) when (e.Message.Contains("GameVersion"))
                {
                    DebugOutput.PrintLn($"Guessed game: {i} for: {file}. Wrong guess though.");
                    continue;
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                }
            }

            return pcc;
        }
        #endregion Creation


        public bool isDLC
        {
            get
            {
                return pccFileName.Contains("\\DLC\\");  // KFreon: Only occurs when DLC folder is present in path - unless user has the game in a folder called DLC...
            }
        }

        #region Properties
        public long FileLength { get; set; }
        string PathBIOGame = null;
        public List<Block> blockList = new List<Block>();
        public int GameVersion { get; set; }
        public byte[] header { get; set; }
        public uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        public ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        public ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        public int ME12expDataBegOffset { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int nameSize { get { int val = BitConverter.ToInt32(header, 12); if (val < 0) return val * -2; else return val; } }
        public uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }
        public byte[] extraNamesList = null;
        public bool bExtraNamesList
        {
            get
            {
                return (flags & 0x10000000) != 0;
            }
        }
        public bool bCompressed
        {
            get { return (flags & 0x02000000) != 0; }
            set
            {
                if (value) // sets the compressed flag if bCompressed set equal to true
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | 0x02000000), 0, header, 16 + nameSize, sizeof(int));
                else // else set to false
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~0x02000000), 0, header, 16 + nameSize, sizeof(int));
            }
        }
        public int Offsets
        {
            get
            {
                if (GameVersion == 3 && (flags & 8) != 0)
                    //if (GameVersion == 3)
                    return 24 + nameSize;
                else
                    return 20 + nameSize;
            }
        }

        int gameOffsetVal
        {
            get
            {
                return GameVersion == 3 ? 4 : -4;
            }
        }

        public int NameCount { get { return BitConverter.ToInt32(header, Offsets); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, Offsets, sizeof(int)); } }
        public int NameOffset { get { return BitConverter.ToInt32(header, Offsets + 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, Offsets + 4, sizeof(int)); } }
        public int ExportCount { get { return BitConverter.ToInt32(header, Offsets + 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, Offsets + 8, sizeof(int)); } }
        public int ExportOffset { get { return BitConverter.ToInt32(header, Offsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, Offsets + 12, sizeof(int)); } }
        public int ImportCount { get { return BitConverter.ToInt32(header, Offsets + 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, Offsets + 16, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, Offsets + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, Offsets + 20, sizeof(int)); } }
        public int Generator { get { return BitConverter.ToInt32(header, nameSize + 64); } }
        public int Compression { get { return BitConverter.ToInt32(header, header.Length - 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, header.Length - 4, sizeof(int)); } }
        public int ExportDataEnd
        {
            get
            {
                return (int)(LastExport.DataOffset + LastExport.DataSize);
            }
        }
        public AbstractExportEntry LastExport { get; set; }

        int expInfoEndOffset { get { return BitConverter.ToInt32(header, Offsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, Offsets + 24, sizeof(int)); } }
        int ME3expDataBegOffset { get { return BitConverter.ToInt32(header, Offsets + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, Offsets + 28, sizeof(int)); Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        public int headerEnd { get; set; }
        public bool Loaded = false;
        bool bDLCStored { get; set; }
        SaltLZOHelper lzo { get; set; }
        public string fullpath { get; set; }
        public string pccFileName { get; set; }
        public MemoryStream ListStream { get; set; }
        public MemoryStream DataStream { get; set; }
        public List<AbstractExportEntry> Exports { get; set; }
        public List<AbstractImportEntry> Imports { get; set; }
        public List<string> Names { get; set; }
        public int NumChunks { get; set; }
        public long expDataEndOffset
        {
            get
            {
                uint max = Exports.Max(maxExport => maxExport.DataOffset);
                AbstractExportEntry lastEntry = null;
                foreach (AbstractExportEntry ex in Exports)
                {
                    if (ex.DataOffset == max)
                    {
                        lastEntry = ex;
                        break;
                    }

                }
                return (long)(lastEntry.DataOffset + lastEntry.DataSize);
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        protected MemoryStream tempStream { get; set; }
        #endregion Properties


        #region Constructors
        /// <summary>
        /// Creates instance of PCCObject given path to PCC file.
        /// </summary>
        /// <param name="path">Path to PCC file.</param>
        protected AbstractPCCObject(string path)
        {
            lzo = new SaltLZOHelper();
            fullpath = path;
            BitConverter.IsLittleEndian = true;

            DebugOutput.PrintLn("Load file as pcc: " + path);
            pccFileName = Path.GetFullPath(path);

            tempStream = UsefulThings.RecyclableMemoryManager.GetStream();
            if (!File.Exists(pccFileName))
                throw new FileNotFoundException("PCC file not found: " + pccFileName);

            int trycount = 0;
            while (trycount++ < 10)
            {
                try
                {
                    using (FileStream fs = new FileStream(pccFileName, FileMode.Open, FileAccess.Read))
                    {
                        FileInfo tempInfo = new FileInfo(pccFileName);
                        tempStream.WriteFromStream(fs, tempInfo.Length);
                        FileLength = tempInfo.Length;
                        if (tempStream.Length != tempInfo.Length)
                        {
                            throw new FileLoadException("File not fully read in. Try again later");
                        }
                        else
                            break;
                    }
                }
                catch (Exception e)
                {
                    // KFreon: File inaccessible or someting
                    DebugOutput.PrintLn("File inaccessible: {0}.  Attempt: {1}", path, trycount);
                    DebugOutput.PrintLn("Reason for failure: ", "AbstractPCCObject ctor", e);
                    System.Threading.Thread.Sleep(100);
                }
            }
        }


        /// <summary>
        /// Loads PCC from stream of file data.
        /// </summary>
        /// <param name="tempStream">Stream containing file data.</param>
        protected virtual void LoadFromStream(MemoryStream tempStream)
        {
            // KFreon: Seeking around to get various bits of info
            tempStream.Seek(12, SeekOrigin.Begin);
            int tempNameSize = tempStream.ReadInt32FromStream();
            tempStream.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerator = tempStream.ReadInt32FromStream();
            tempStream.Seek(36 + tempGenerator * 12, SeekOrigin.Current);
            int tempPos = (int)tempStream.Position + (GameVersion == 1 ? 4 : 0);

            tempStream.Seek(0, SeekOrigin.Begin);
            header = tempStream.ReadBytes(tempPos);
            tempStream.Seek(0, SeekOrigin.Begin);

            if (magic != ZBlock.magic && magic.Swap() != ZBlock.magic)
            {
                DebugOutput.PrintLn("Magic number incorrect: " + magic);
                throw new FormatException("This is not a pcc file. The magic number is incorrect.");
            }

            if (bCompressed)
            {
                DebugOutput.PrintLn("File is compressed");

                ListStream = lzo.DecompressPCC(tempStream, this);

                //Correct the header
                bCompressed = false;
                ListStream.Seek(0, SeekOrigin.Begin);
                ListStream.WriteBytes(header);

                // Set numblocks to zero
                ListStream.WriteInt32ToStream(0);

                //Write the magic number
                ListStream.WriteBytes(new byte[] { 0xF2, 0x56, 0x1B, 0x4E });

                // Write 4 bytes of 0

                ListStream.WriteInt32ToStream(0);

            }
            else
            {
                DebugOutput.PrintLn("File already decompressed. Reading decompressed data.");
                //listsStream = tempStream;
                ListStream = UsefulThings.RecyclableMemoryManager.GetStream();
                tempStream.WriteTo(ListStream);
            }
            tempStream.Dispose();

            // KFreon: Read bits of file
            ReadNames(ListStream);
            ReadImports(ListStream);
            ReadExports(ListStream);
            LoadExports();
        }
        #endregion Constructors


        #region Methods
        /// <summary>
        /// Returns data specified by offset and length, decompressing if required.
        /// </summary>
        /// <param name="offset">Offset in main file datastream to begin at.</param>
        /// <param name="length">Uncompressed length to read.</param>
        public byte[] Decompressor(uint offset, int length)
        {
            using (MemoryStream retval = UsefulThings.RecyclableMemoryManager.GetStream())
            {
                uint newoffset = 0;
                // KFreon: Find datablocks to decompress
                int DataStart = 0;
                int DataEnd = 0;
                int got = 0;
                for (int m = 0; m < blockList.Count; m++)
                {
                    if (got == 0 && blockList[m].uncOffset + blockList[m].uncSize > offset)
                    {
                        DataStart = m;
                        got++;
                    }

                    if (got == 1 && blockList[m].uncOffset + blockList[m].uncSize > offset + length)
                    {
                        DataEnd = m;
                        got++;
                    }

                    if (got == 2)
                        break;
                }

                if (DataEnd == 0 && DataStart != 0)
                    DataEnd = DataStart;

                // KFreon: Decompress blocks
                newoffset = offset - (uint)blockList[DataStart].uncOffset;
                for (int i = (int)DataStart; i <= DataEnd; i++)
                {
                    DataStream.Seek(blockList[i].cprOffset, SeekOrigin.Begin);
                    retval.WriteBytes(ZBlock.Decompress(DataStream, blockList[i].cprSize));
                }

                retval.Seek(newoffset, SeekOrigin.Begin);
                return retval.ReadBytes(length);
            }
        }


        /// <summary>
        /// Gets name from Names List "safely". Returns empty string if index invalid.
        /// </summary>
        /// <param name="Index">Index of Name to get</param>
        public virtual string GetName(int Index)
        {
            string s = "";
            if (isName(Index))
                s = Names[Index];
            return s;
        }


        /// <summary>
        /// Loads exports from file datastream.
        /// </summary>
        private void LoadExports()
        {
            DebugOutput.PrintLn("Prefetching Export Name Data...");
            for (int i = 0; i < ExportCount; i++)
            {
                Exports[i].hasChanged = false;
                Exports[i].ObjectName = Names[Exports[i].ObjectNameID];
            }
            for (int i = 0; i < ExportCount; i++)
            {
                Exports[i].PackageFullName = FollowLink(Exports[i].LinkID);
                if (String.IsNullOrEmpty(Exports[i].PackageFullName))
                    Exports[i].PackageFullName = "Base Package";
                else if (Exports[i].PackageFullName[Exports[i].PackageFullName.Length - 1] == '.')
                    Exports[i].PackageFullName = Exports[i].PackageFullName.Remove(Exports[i].PackageFullName.Length - 1);
            }
            for (int i = 0; i < ExportCount; i++)
                Exports[i].ClassName = GetClass(Exports[i].ClassNameID);
        }


        /// <summary>
        /// Loads Imports from datastream.
        /// </summary>
        /// <param name="fs">Stream containing file data.</param>
        private void ReadImports(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Imports...");
            Imports = new List<AbstractImportEntry>();
            fs.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                AbstractImportEntry import = AbstractImportEntry.Create(GameVersion, this);

                import.Package = Names[fs.ReadInt32FromStream()];
                fs.Seek(12, SeekOrigin.Current);
                import.link = fs.ReadInt32FromStream();
                import.Name = Names[fs.ReadInt32FromStream()];
                fs.Seek(-24, SeekOrigin.Current);
                import.raw = fs.ReadBytes(28);
                Imports.Add(import);
            }
        }

        /// <summary>
        /// Read Exports from file datastream.
        /// </summary>
        /// <param name="fs">Stream containing file data.</param>
        private void ReadExports(MemoryStream fs)
        {
            DebugOutput.PrintLn("Reading Exports...");
            fs.Seek(ExportOffset, SeekOrigin.Begin);
            Exports = new List<AbstractExportEntry>();

            for (int i = 0; i < ExportCount; i++)
            {
                long start = fs.Position;
                AbstractExportEntry exp = AbstractExportEntry.Create(GameVersion, this);

                exp.InfoOffset = (int)start;

                fs.Seek(40, SeekOrigin.Current);
                int count = fs.ReadInt32FromStream();
                fs.Seek(4 + count * 12, SeekOrigin.Current);
                count = fs.ReadInt32FromStream();
                fs.Seek(4 + count * 4, SeekOrigin.Current);
                fs.Seek(16, SeekOrigin.Current);
                long end = fs.Position;
                fs.Seek(start, SeekOrigin.Begin);
                exp.info = fs.ReadBytes((int)(end - start));
                Exports.Add(exp);
                fs.Seek(end, SeekOrigin.Begin);

                if (LastExport == null || exp.DataOffset > LastExport.DataOffset)
                    LastExport = exp;
            }
        }

        public virtual void ReadNames(MemoryStream fs)  // ME1 and 2
        {
            DebugOutput.PrintLn("Reading Names...");
            fs.Seek(NameOffset, SeekOrigin.Begin);
            Names = new List<string>();

            List<char> thigns = new List<char>();


            // TESTING
            /*byte[] things = fs.ReadBytes((int)(fs.Length - fs.Seek(0, SeekOrigin.Current)));
            foreach (var item in things)
                thigns.Add((char)item);*/


            fs.Seek(NameOffset, SeekOrigin.Begin);


            for (int i = 0; i < NameCount; i++)
            {


                /*if (i == 2350)
                {
                    List<char> tmp = new List<char>();
                    for (int k=(int)(fs.Seek(0, SeekOrigin.Current) - NameOffset);k<thigns.Count;k++)
                    {
                        tmp.Add(thigns[k]);
                    }
                    tmp.Add('d');
                }*/

                int len = fs.ReadInt32FromStream();

                string s = "";
                if (len > 0)
                {
                    //s = fs.ReadString((uint)(len - 1));
                    s = fs.ReadStringFromStream();


                    fs.Seek((GameVersion == 1 ? 8 : 4), SeekOrigin.Current);  // KFreon: Originally 9
                }
                else if (len < 0)
                {
                    len *= -2;
                    s = fs.ReadString((uint)(len - 3));
                    s = s.Replace("\0", "");
                    fs.Seek(11, SeekOrigin.Current);

                    /*s = fs.ReadStringFromStream();
                    fs.Seek(8, SeekOrigin.Current);*/
                }
                Names.Add(s);
            }
            int m = 9;
            m += 1;
        }

        /// <summary>
        /// Checks if Name at given index is actually a Name.
        /// </summary>
        /// <param name="Index">Index of Name to check</param>
        public virtual bool isName(int Index)
        {
            return (Index >= 0 && Index < NameCount);
        }


        /// <summary>
        /// Checks if Import at given index is actually an Import.
        /// </summary>
        /// <param name="Index">Index of Import to check.</param>
        public virtual bool isImport(int Index)
        {
            return (Index >= 0 && Index < ImportCount);
        }


        /// <summary>
        /// Checks if Export at given index is actually an Export.
        /// </summary>
        /// <param name="Index">Index of Export to check.</param>
        public virtual bool isExport(int Index)
        {
            return (Index >= 0 && Index < ExportCount);
        }


        /// <summary>
        /// Gets Class at Index. Returns 'Class' if not found.
        /// </summary>
        /// <param name="Index">Index of List to check. +ve Index = Exports, -ve Index = Imports.</param>
        public virtual string GetClass(int Index)
        {
            if (Index > 0 && isExport(Index - 1))
                return Exports[Index - 1].ObjectName;
            if (Index < 0 && isImport(Index * -1 - 1))
                return Imports[Index * -1 - 1].Name;
            return "Class";
        }


        /// <summary>
        /// Throwback to ME1/2 stuff. Follows Exports and Imports for some reason...
        /// </summary>
        /// <param name="Link">...Dunno.</param>
        public virtual string FollowLink(int Link)
        {
            string s = "";
            if (Link > 0 && isExport(Link - 1))
            {
                s = Exports[Link - 1].ObjectName + ".";
                s = FollowLink(Exports[Link - 1].LinkID) + s;
            }
            if (Link < 0 && isImport(Link * -1 - 1))
            {
                s = Imports[Link * -1 - 1].Name + ".";
                s = FollowLink(Imports[Link * -1 - 1].link) + s;
            }
            return s;
        }

        /// <summary>
        /// Adds new Name to Names List. Returns original NameCount.
        /// </summary>
        /// <param name="newName">Name to add.</param>
        public virtual int AddName(string newName)
        {
            if (newName == null)
                return -1;

            int nameID = 0;
            //First check if name already exists
            for (int i = 0; i < NameCount; i++)
            {
                if (Names[i] == newName)
                {
                    nameID = i;
                    return nameID;
                }
            }

            Names.Add(newName);
            NameCount++;
            return Names.Count - 1;
        }

        /// <summary>
        /// Finds Export from name. Returns expID if found, -1 otherwise.
        /// </summary>
        /// <param name="name">ObjectName of desired export.</param>
        public virtual int FindExp(string name)
        {
            for (int i = 0; i < ExportCount; i++)
            {
                if (String.Compare(Exports[i].ObjectName, name, true) == 0)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds Export from name and class. Returns expID if found, -1 otherwise.
        /// </summary>
        /// <param name="name">ObjectName of desired export.</param>
        /// <param name="className">Name of desired export class.</param>
        public virtual int FindExp(string name, string className)
        {
            for (int i = 0; i < ExportCount; i++)
            {
                if (String.Compare(Exports[i].ObjectName, name, true) == 0 && Exports[i].ClassName == className)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Creates Texture2D from specified Export given its ID.
        /// </summary>
        /// <param name="expID">Index of Export to create Texture2D for.</param>
        /// <param name="hash">Hash to give to returned Texture2D.</param>
        public METexture2D CreateTexture2D(int expID, uint hash = 0)
        {
            METexture2D temptex2D = new METexture2D(this, expID, PathBIOGame);
            if (hash != 0)
                temptex2D.Hash = hash;
            return temptex2D;
        }

        /// <summary>
        /// Saves instance to file.
        /// </summary>
        /// <param name="path">Destination path.</param>
        public virtual void SaveToFile(string path)
        {
            Console.WriteLine("Saving: {0}", path);
            DebugOutput.PrintLn("Saving pcc: {0}", path);
            ListStream.Seek(ExportDataEnd, SeekOrigin.Begin); // Write names
            NameOffset = (int)ListStream.Position;
            Console.WriteLine("Saving Name offset: {0}  Num names: {1}", NameOffset, Names.Count);
            NameCount = Names.Count;
            foreach (String name in Names)
            {
                if (name != null)
                {
                    //Console.WriteLine("Name: {0}  Length: {1}", name, name.Length);
                    ListStream.WriteInt32ToStream(name.Length + 1);   // +1 for ME2 at least
                    ListStream.WriteString(name);
                }
                else
                    ListStream.WriteInt32ToStream(1);
                ListStream.WriteByte(0);
                ListStream.WriteInt32ToStream(-14);

                if (GameVersion == 1)
                    ListStream.WriteInt32ToStream(5);  // KFreon: TESTING
            }

            DebugOutput.PrintLn("Writing pcc to: {0}", path);
            DebugOutput.PrintLn("Refreshing header to stream...");
            ListStream.Seek(0, SeekOrigin.Begin);
            ListStream.WriteBytes(header);
            DebugOutput.PrintLn("Opening filestream and writing to disk...");
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                ListStream.WriteTo(fs);
        }
        #endregion Methods

        /// <summary>
        /// Scans current instance for textures supported by Texplorer. Returns error if any occured, otherwise null.
        /// </summary>
        /// <param name="ExecFolder">Location of ME3Explorer \exec\ folder.</param>
        /// <param name="Tree">Texplorer Tree being built by scanning this PCC, amoung others.</param>
        internal List<Exception> Scan(string ExecFolder, TreeDB Tree, ThumbnailManager thumbsManager)
        {
            byte[] imgData = null;
            METexture2D tex2D = null;
            ImageInfo info = null;

            List<Exception> Errors = new List<Exception>();

            for (int expID = 0; expID < Exports.Count; expID++)  // KFreon: Use explicit Count in case header count is different
            {
                // KFreon: Proceed only if current Export is a texture
                if (Exports[expID].ValidTextureClass())
                {
                    try
                    {
                        tex2D = CreateTexture2D(expID);
                    }
                    catch (OverflowException overflow)
                    {
                        Debug.WriteLine("Overflow occured. This happens sometimes. Nothing to worry about: " + overflow.ToString());
                        continue;
                    }
                    catch (Exception e)
                    {
                        Errors.Add(e);
                        DebugOutput.PrintLn("Unknown error: ", "AbstractPCCObject Scan", e);
                        Debug.WriteLine("error: " + e);
                        continue;
                    }


                    // KFreon: Ignore if no images
                    if (tex2D.imgList.Count == 0)
                        continue;

                    DebugOutput.PrintLn("Scanning: {0} in {1}", tex2D.texName, pccFileName);


                    info = tex2D.GenerateImageInfo();

                    // KFreon: Calculate hash
                    uint hash = tex2D.GenerateHash(info, out imgData);
                    if (hash == 0)
                        continue;


                    // KFreon: Create Tree entry for texture
                    TreeTexInfo tex = new TreeTexInfo(tex2D, expID, hash, pccFileName, info, GameVersion, PathBIOGame, true, thumbsManager);
                    bool Added = Tree.AddTex(tex, Path.GetFileNameWithoutExtension(pccFileName).ToUpperInvariant());

                    if (Added)
                    {
                        if (!thumbsManager.GenerateThumbnail(imgData, tex.ThumbnailName))
                            DebugOutput.PrintLn("FAILED to create thumbnail for: " + tex2D.texName);
                    }
                }
            }

            return Errors;
        }

        public void UpdatePCCTextureEntry(METexture2D tex2D, int expID)
        {
            DebugOutput.PrintLn("Updating PCC Texture entry: {0}  at expID: {1}", tex2D.texName, expID);
            METexture2D tempTex2D = tex2D;//new METexture2D(tex2D, this, expID);
            AbstractExportEntry entry = Exports[expID];
            entry.Data = tempTex2D.ToArray(this, entry.DataOffset);
        }

        public void Dispose()
        {
            if (DataStream != null)
                DataStream.Dispose();

            if (blockList != null)
                blockList.Clear();

            if (ListStream != null)
                ListStream.Dispose();

            if (Exports != null)
                Exports.Clear();

            if (Imports != null)
                Imports.Clear();

            if (tempStream != null)
                tempStream.Dispose();
        }
    }
}
