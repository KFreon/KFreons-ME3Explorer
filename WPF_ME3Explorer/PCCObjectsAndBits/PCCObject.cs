using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmaroK86.MassEffect3.ZlibBlock;
using UsefulThings;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.PCCObjectsAndBits
{
    public class PCCObject : IDisposable
    {
        public string pccFileName { get; set; }

        static int headerSize = 0x8E;
        public byte[] header = null;
        byte[] extraNamesList = null;
        public int GameVersion { get; set; }

        private uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        private ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        private ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        private int nameSize { get { int val = BitConverter.ToInt32(header, 12); if (val < 0) return val * -2; else return val; } }
        public uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }

        public bool isModified { get { return Exports.Any(entry => entry.hasChanged == true); } }
        public bool DLCStored { get; set; }
        public bool bextraNamesList { get { return (flags & 0x10000000) != 0; } }
        public bool compressed
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

        int idxOffsets { get { if (GameVersion == 3 && (flags & 8) != 0) return 24 + nameSize; else return 20 + nameSize; } }
        int NameCount { get { return BitConverter.ToInt32(header, idxOffsets); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int)); } }
        int NameOffset { get { return BitConverter.ToInt32(header, idxOffsets + 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int)); } }
        int ExportCount { get { return BitConverter.ToInt32(header, idxOffsets + 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int)); } }
        int ExportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int)); } }
        int ImportCount { get { return BitConverter.ToInt32(header, idxOffsets + 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int)); } }
        int ImportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int)); } }
       
        int headerEnd;
        public bool Loaded = false;

        public long expDataEndOffset
        {
            get
            {

                uint max = Exports.Max(maxExport => maxExport.DataOffset);
                ExportEntry lastEntry = null;
                foreach (ExportEntry ex in Exports)
                {
                    if (ex.DataOffset == max)
                    {
                        lastEntry = ex;
                        break;
                    }

                }
                return (long)(lastEntry.DataOffset + lastEntry.DataSize);
            }
        }




        protected class Block
        {
            public int uncOffset;
            public int uncSize;
            public int cprOffset;
            public int cprSize;
            public bool bRead = false;
        }

        public List<string> Names { get; set; }

        public List<ImportEntry> Imports { get; set; }
        public List<ExportEntry> Exports { get; set; }
        public int NumChunks;
        public MemoryStream listsStream;



        /// <summary>
        /// PCCObject class constructor. It also load namelist, importlist and exportinfo (not exportdata) from pcc file.
        /// </summary>
        /// <param name="filePath">Path to file to read.</param>
        /// <param name="gameVersion">Version of Mass Effect PCC is from.</param>
        public PCCObject(string filePath, int gameVersion) : this(gameVersion)
        {
            LoadAsync(filePath).Wait();
        }

        /// <summary>
        /// Creates PCC Object from existing stream. 
        /// Filename is not used for reading, just labelling.
        /// DO NOT DISPOSE of stream before end of object life.
        /// </summary>
        /// <param name="filePath">Path to file represented in <paramref name="stream"/>.</param>
        /// <param name="stream">Stream containing entire pcc file to read from.</param>
        /// <param name="gameVersion">Version of Mass Effect PCC is from.</param>
        public PCCObject(string filePath, MemoryStream stream, int gameVersion) : this(gameVersion)
        {
            pccFileName = filePath;
            PCCObjectHelper(stream, filePath);
        }

        /// <summary>
        /// Creates empty PCCObject.
        /// </summary>
        /// <param name="gameVersion">Version of Mass Effect PCC is from.</param>
        public PCCObject(int gameVersion)
        {
            GameVersion = gameVersion;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="gameVersion"></param>
        /// <returns></returns>
        public static async Task<PCCObject> CreateAsync(string filePath, int gameVersion)
        {
            PCCObject pcc = new PCCObject(gameVersion);
            await pcc.LoadAsync(filePath);
            return pcc;
        }

        async Task LoadAsync(string filePath)
        {
            pccFileName = Path.GetFullPath(filePath);

            MemoryStream tempStream = new MemoryStream();
            if (!File.Exists(pccFileName))
                throw new FileNotFoundException("File not found: " + pccFileName);

            try
            {
                using (FileStream fs = new FileStream(pccFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                    await fs.CopyToAsync(tempStream).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // KFreon: File inaccessible or someting
                DebugOutput.PrintLn($"Failed to read PCC: {filePath}. Reason: {e.ToString()}.");
            }

            await Task.Run(() => PCCObjectHelper(tempStream, filePath));
        }

        void PCCObjectHelper(MemoryStream tempStream, string filePath)
        {
            tempStream.Seek(0, SeekOrigin.Begin);
            Names = new List<string>();
            Imports = new List<ImportEntry>();
            Exports = new List<ExportEntry>();

            if (GameVersion != 3)
            {
                // Find ME1 and 2 header...Why so difficult ME1 and 2?
                tempStream.Seek(12, SeekOrigin.Begin);
                int tempNameSize = tempStream.ReadInt32();
                tempStream.Seek(64 + tempNameSize, SeekOrigin.Begin);
                int tempGenerator = tempStream.ReadInt32();
                tempStream.Seek(36 + tempGenerator * 12, SeekOrigin.Current);
                int tempPos = (int)tempStream.Position + (GameVersion == 2 ? 0 : 4);
                tempStream.Seek(0, SeekOrigin.Begin);
                header = tempStream.ReadBytes(tempPos);
                tempStream.Seek(0, SeekOrigin.Begin);
            }
            else
                header = tempStream.ReadBytes(headerSize);

            if (magic != ZBlock.magic &&
                    magic.Swap() != ZBlock.magic)
                throw new FormatException(filePath + " is not a pcc file");

            if (GameVersion == 3 && lowVers != 684 && highVers != 194)
                throw new FormatException("unsupported version");

            if (compressed)
            {
                if (GameVersion == 3)
                    ReadCompressedME3(tempStream);
                else
                    ReadCompressedME1And2(tempStream);

                compressed = false;
            }
            else
                listsStream = tempStream;

            //Fill name list
            listsStream.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                int strLength = listsStream.ReadInt32();
                string name = null;

                switch (GameVersion)
                {
                    case 1:
                        name = ReadME1Name(strLength);
                        break;
                    case 2:
                        name = ReadME2Name(strLength);
                        break;
                    case 3:
                        name = ReadME3Name(strLength);
                        break;
                }

                Names.Add(name);               
            }

            byte[] buffer = null;

            // fill import list
            listsStream.Seek(ImportOffset, SeekOrigin.Begin);
            buffer = new byte[ImportEntry.byteSize];
            for (int i = 0; i < ImportCount; i++)
                Imports.Add(new ImportEntry(this, listsStream));

            //fill export list
            listsStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                uint expInfoOffset = (uint)listsStream.Position;

                // Find export data size
                int count = 0;
                int expInfoSize = 0;
                if (GameVersion == 3)
                {
                    listsStream.Seek(44, SeekOrigin.Current);
                    count = listsStream.ReadInt32();
                    expInfoSize = 68 + (count * 4);
                }
                else
                {
                    listsStream.Seek(40, SeekOrigin.Current);
                    count = listsStream.ReadInt32();
                    listsStream.Seek(4 + count * 12, SeekOrigin.Current);
                    count = listsStream.ReadInt32();
                    listsStream.Seek(4 + count * 4, SeekOrigin.Current);
                    listsStream.Seek(16, SeekOrigin.Current);
                    expInfoSize = (int)(listsStream.Position - expInfoOffset);
                }
                
                // Read export data
                buffer = new byte[expInfoSize];
                listsStream.Seek(expInfoOffset, SeekOrigin.Begin);
                listsStream.Read(buffer, 0, buffer.Length);
                Exports.Add(new ExportEntry(this, buffer, expInfoOffset));
            }
        }

        string ReadME1Name(int len)
        {
            string s = "";
            if (len > 0)
            {
                byte[] data = new byte[len - 1];
                listsStream.Read(data, 0, data.Length);
                s = Encoding.ASCII.GetString(data, 0, data.Length).TrimEnd('\0');
                listsStream.Seek(9, SeekOrigin.Current);
            }
            else
            {
                len *= -1;
                for (int j = 0; j < len - 1; j++)
                {
                    s += (char)listsStream.ReadByte();
                    listsStream.ReadByte();
                }
                listsStream.Seek(10, SeekOrigin.Current);
            }

            return s;
        }

        string ReadME2Name(int len)
        {
            byte[] data = new byte[len - 1];
            listsStream.Read(data, 0, data.Length);
            string s = Encoding.ASCII.GetString(data, 0, data.Length).TrimEnd('\0');
            listsStream.Seek(5, SeekOrigin.Current);
            return s;
        }

        string ReadME3Name(int strLength)
        {
            byte[] data = new byte[strLength * -2];
            listsStream.Read(data, 0, data.Length);
            return Encoding.Unicode.GetString(data, 0, data.Length).TrimEnd('\0');
        }

        void ReadCompressedME1And2(MemoryStream tempStream)
        {
            DebugOutput.PrintLn("File is compressed");
            listsStream = SaltLZOHelper.DecompressPCC(tempStream, this);

            //Correct the header
            compressed = false;
            listsStream.Seek(0, SeekOrigin.Begin);
            listsStream.WriteBytes(header);

            // Set numblocks to zero
            listsStream.WriteInt32(0);
            
            //Write the magic number
            if (GameVersion == 1)
                listsStream.WriteBytes(new byte[] { 0xF2, 0x56, 0x1B, 0x4E });
            else
                listsStream.WriteInt32(1026281201);
            
            // Write 4 bytes of 0
            listsStream.WriteInt32(0);

            // Write 4 more for ME2
            if (GameVersion == 2)
                listsStream.WriteInt32(0);
        }

        void ReadCompressedME3(MemoryStream tempStream)
        {
            List<Block> blockList = null;

            // seeks the blocks info position
            tempStream.Seek(idxOffsets + 60, SeekOrigin.Begin);
            int generator = tempStream.ReadInt32();
            tempStream.Seek((generator * 12) + 20, SeekOrigin.Current);

            int blockCount = tempStream.ReadInt32();
            blockList = new List<Block>();

            // creating the Block list
            for (int i = 0; i < blockCount; i++)
            {
                Block temp = new Block();
                temp.uncOffset = tempStream.ReadInt32();
                temp.uncSize = tempStream.ReadInt32();
                temp.cprOffset = tempStream.ReadInt32();
                temp.cprSize = tempStream.ReadInt32();
                blockList.Add(temp);
            }

            // correcting the header, in case there's need to be saved
            Buffer.BlockCopy(BitConverter.GetBytes((int)0), 0, header, header.Length - 12, sizeof(int));
            tempStream.Read(header, header.Length - 8, 8);
            headerEnd = (int)tempStream.Position;

            // copying the extraNamesList
            int extraNamesLenght = blockList[0].cprOffset - headerEnd;
            if (extraNamesLenght > 0)
            {
                extraNamesList = new byte[extraNamesLenght];
                tempStream.Read(extraNamesList, 0, extraNamesLenght);
            }

            int dataStart = 0;
            using (MemoryStream he = new MemoryStream(header))
            {
                he.Seek(0, SeekOrigin.Begin);
                he.ReadInt32();
                he.ReadInt32();
                dataStart = he.ReadInt32();
            }

            //Decompress ALL blocks
            listsStream = new MemoryStream();
            for (int i = 0; i < blockCount; i++)
            {
                tempStream.Seek(blockList[i].cprOffset, SeekOrigin.Begin);
                listsStream.Seek(blockList[i].uncOffset, SeekOrigin.Begin);
                listsStream.WriteBytes(ZBlock.Decompress(tempStream, blockList[i].cprSize));
            }
        }


        /// <summary>
        /// Saves PCC to file or MemoryStream. CURRENTLY FILE ONLY.
        /// </summary>
        /// <param name="newFileName">File to save to. Null if saving to stream.</param>
        /// <param name="WriteToMemoryStream">True = writes to stream instead.</param>
        public void SaveToFile(string newFileName = null, bool WriteToMemoryStream = false)
        {
            //Refresh header and namelist
            listsStream.Seek(expDataEndOffset, SeekOrigin.Begin);
            NameOffset = (int)listsStream.Position;
            NameCount = Names.Count;
            foreach (string name in Names)
            {
                listsStream.WriteInt32(-(name.Length + 1));
                listsStream.WriteString(name + "\0");  // KFreon: Could be a problem. Original doubled length written by string for some reason.
            }

            listsStream.Seek(0, SeekOrigin.Begin);
            listsStream.WriteBytes(header);

            // KFreon: If want to write to memorystream instead of to file, exit here
            if (WriteToMemoryStream)
                return;

            // Heff: try to remove any read-only attribute if we have permission to:
            File.SetAttributes(newFileName, FileAttributes.Normal);

            while (true)
            {
                int tries = 0;
                try
                {
                    using (FileStream fs = new FileStream(newFileName, FileMode.Create, FileAccess.Write))
                        listsStream.WriteTo(fs);
                    break;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                    tries++;
                    if (tries > 5)
                    {
                        throw new IOException("The PCC can't be written to disk because of an IOException");
                    }
                }
            }
            listsStream.Dispose();
            Exports.Clear();
            Imports.Clear();
            Names.Clear();
            Exports = null;
            Imports = null;
            Names = null;
        }

        public string GetNameEntry(int index)
        {
            if (!IsName(index))
                return "";
            return Names[index];
        }

        public bool IsName(int index)
        {
            return (index >= 0 && index < Names.Count);
        }
        public bool IsImport(int index)
        {
            return (index >= 0 && index < Imports.Count);
        }
        public bool IsExport(int index)
        {
            return (index >= 0 && index < Exports.Count);
        }

        public int AddName(string name)
        {
            int nameID = FindName(name);

            if (nameID != -1)
                return nameID;

            Names.Add(name);
            return Names.Count - 1;
        }

        /// <summary>
        /// Checks whether a name exists in the PCC and returns its index
        /// If it doesn't exist returns -1
        /// </summary>
        /// <param name="nameToFind">The name of the string to find</param>
        /// <returns></returns>
        public int FindName(string nameToFind)
        {
            for (int i = 0; i < Names.Count; i++)
            {
                if (String.Compare(nameToFind, GetNameEntry(i)) == 0)
                    return i;
            }
            return -1;
        }

        public string GetClassName(int index)
        {
            string s = "";
            if (index > 0)
            {
                s = Names[Exports[index - 1].ObjectNameID];
            }
            if (index < 0)
            {
                s = Names[Imports[index * -1 - 1].ObjectNameID];
            }
            if (index == 0)
            {
                s = "Class";
            }
            return s;
        }


        public void AddExport(ExportEntry exportEntry)
        {
            if (exportEntry.pccRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.hasChanged = true;

            //changing data offset in order to append it at the end of the file
            ExportEntry lastExport = Exports.Find(export => export.DataOffset == Exports.Max(entry => entry.DataOffset));
            int lastOffset = (int)(lastExport.DataOffset + lastExport.Data.Length);
            exportEntry.DataOffset = (uint)lastOffset;

            Exports.Add(exportEntry);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Exports != null)
                    {
                        try
                        {
                            foreach (ExportEntry entry in Exports)
                                entry.Dispose();
                        }
                        catch { }
                    }

                    if (Imports != null)
                    {
                        try
                        {
                            foreach (ImportEntry entry in Imports)
                                entry.Dispose();
                        }
                        catch { }
                    }


                    if (listsStream != null)
                        try
                        {
                            listsStream.Dispose();
                        }
                        catch { }
                }

                this.extraNamesList = null;
                this.header = null;
                this.Names = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~PCCObject()
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
        #endregion

        /// <summary>
        /// Returns true if given ClassName is a valid texture class.
        /// </summary>
        /// <param name="ClassName">ClassName to validate.</param>
        /// <returns>True if valid texture class.</returns>
        public static bool ValidTexClass(string ClassName)
        {
            return ClassName == "Texture2D" || ClassName == "LightMapTexture2D" || ClassName == "TextureFlipBook";
        }
    }

    static class Exts
    {
        public static UInt32 Swap(this UInt32 value)
        {
            var swapped = ((0x000000FF) & (value >> 24) |
                           (0x0000FF00) & (value >> 8) |
                           (0x00FF0000) & (value << 8) |
                           (0xFF000000) & (value << 24));
            return swapped;
        }
    }
}
