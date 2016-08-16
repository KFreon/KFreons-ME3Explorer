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
        public byte[] header = new byte[headerSize];
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

        int idxOffsets { get { if ((flags & 8) != 0) return 24 + nameSize; else return 20 + nameSize; } }
        int NameCount { get { return BitConverter.ToInt32(header, idxOffsets); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int)); } }
        int NameOffset { get { return BitConverter.ToInt32(header, idxOffsets + 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int)); } }
        int ExportCount { get { return BitConverter.ToInt32(header, idxOffsets + 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int)); } }
        int ExportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int)); } }
        int ImportCount { get { return BitConverter.ToInt32(header, idxOffsets + 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int)); } }
        int ImportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int)); } }

        int expInfoEndOffset { get { return BitConverter.ToInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(int)); } }

        // LIKELY A PROBLEM HERE for ME1
        int expDataBegOffset { get { return BitConverter.ToInt32(header, idxOffsets + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(int)); Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
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


        List<Block> blockList = null;

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
        List<Block> blocklist = null;
        public int NumChunks;
        public MemoryStream listsStream;
        public MemoryStream DataStream;



        /// <summary>
        ///     PCCObject class constructor. It also load namelist, importlist and exportinfo (not exportdata) from pcc file
        /// </summary>
        /// <param name="pccFilePath">full path + file name of desired pcc file.</param>
        public PCCObject(string filePath, int gameVersion) : this(gameVersion)
        {
            pccFileName = Path.GetFullPath(filePath);

            MemoryStream tempStream = new MemoryStream();
            if (!File.Exists(pccFileName))
                throw new FileNotFoundException("File not found: " + pccFileName);

            int trycout = 0;
            while (trycout < 50)
            {
                try
                {
                    using (FileStream fs = new FileStream(pccFileName, FileMode.Open, FileAccess.Read))
                    {
                        FileInfo tempInfo = new FileInfo(pccFileName);
                        tempStream.ReadFrom(fs, tempInfo.Length);
                        if (tempStream.Length != tempInfo.Length)
                        {
                            throw new FileLoadException("File not fully read in. Try again later");
                        }
                    }
                    break;
                }
                catch (Exception e)
                {
                    // KFreon: File inaccessible or someting
                    Console.WriteLine(e.Message);
                    DebugOutput.PrintLn("File inaccessible: " + filePath + ".  Attempt: " + trycout);
                    trycout++;
                    System.Threading.Thread.Sleep(100);
                }
            }

            PCCObjectHelper(tempStream, filePath);
        }

        void PCCObjectHelper(MemoryStream tempStream, string filePath)
        {
            tempStream.Seek(0, SeekOrigin.Begin);
            DataStream = new MemoryStream();
            tempStream.WriteTo(DataStream);
            Names = new List<string>();
            Imports = new List<ImportEntry>();
            Exports = new List<ExportEntry>();

            header = tempStream.ReadBytes(headerSize);
            if (magic != ZBlock.magic &&
                    magic.Swap() != ZBlock.magic)
                throw new FormatException(filePath + " is not a pcc file");

            // COULD BE A PROBLEM WITH ME1
            if (lowVers != 684 && highVers != 194)
                throw new FormatException("unsupported version");

            if (compressed)
            {
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

                compressed = false;
            }
            else
            {
                listsStream = new MemoryStream();
                listsStream.WriteBytes(tempStream.ToArray());
            }
            tempStream.Dispose();

            //Fill name list
            listsStream.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                int strLength = listsStream.ReadInt32();
                byte[] data = new byte[strLength * -2];
                listsStream.Read(data, 0, data.Length);
                string name = Encoding.Unicode.GetString(data, 0, data.Length).TrimEnd('\0');

                Names.Add(name);               
            }

            // fill import list
            listsStream.Seek(ImportOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[ImportEntry.byteSize];
            for (int i = 0; i < ImportCount; i++)
                Imports.Add(new ImportEntry(this, listsStream));

            //fill export list
            listsStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                uint expInfoOffset = (uint)listsStream.Position;

                listsStream.Seek(44, SeekOrigin.Current);
                int count = listsStream.ReadInt32();
                listsStream.Seek(-48, SeekOrigin.Current);

                int expInfoSize = 68 + (count * 4);
                buffer = new byte[expInfoSize];

                listsStream.Read(buffer, 0, buffer.Length);
                Exports.Add(new ExportEntry(this, buffer, expInfoOffset));
            }
        }

        // KFreon: Alternate intialiser to allow loading from existing stream
        public PCCObject(string filePath, MemoryStream stream, int gameVersion) : this(gameVersion)
        {
            pccFileName = filePath;
            PCCObjectHelper(stream, filePath);
        }

        public PCCObject(int gameVersion)
        {
            GameVersion = gameVersion;
        }


        public void saveToFile(string newFileName = null, bool WriteToMemoryStream = false)
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
                    {
                        byte[] test = listsStream.ToArray();
                        fs.WriteBytes(test);
                        test = null;
                    }
                    break;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                    tries++;
                    if (tries > 100)
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

                    if (DataStream != null)
                        try
                        {
                            DataStream.Dispose();
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
