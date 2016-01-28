using System;
using System.IO;
using WPF_ME3Explorer.PCCObjects;
using WPF_ME3Explorer.PCCObjects.ExportEntries;
using UsefulThings;
using Gibbed.IO;

namespace WPF_ME3Explorer.PCCObjects
{
    public abstract class AbstractExportEntry
    {
        #region Creation
        /// <summary>
        /// Creates an instance of ExportEntry for game specified.
        /// </summary>
        /// <param name="gameVersion">Game to create Entry for. Valid 1-3</param>
        /// <param name="pcc">PCC to get Entry from/for.</param>
        public static AbstractExportEntry Create(int GameVersion, AbstractPCCObject pcc)
        {
            AbstractExportEntry exp = null;
            switch (GameVersion)
            {
                case 1:
                    exp = new ME1ExportEntry((ME1PCCObject)pcc);
                    break;
                case 2:
                    exp = new ME2ExportEntry((ME2PCCObject)pcc);
                    break;
                case 3:
                    exp = new ME3ExportEntry((ME3PCCObject)pcc);
                    break;
            }
            return exp;
        }


        /// <summary>
        /// Creates an instance of ExportEntry for game specified.
        /// </summary>
        /// <param name="game">Game to create Entry for. Valid 1-3</param>
        /// <param name="pcc">PCC to get Entry from/for.</param>
        /// <param name="importData">Import data (ME3 only)</param>
        /// <param name="exportOffset">Export offset (ME3 only)</param>
        public static AbstractExportEntry Create(int game, AbstractPCCObject pcc, byte[] importData, int exportOffset)
        {
            AbstractExportEntry entry = null;
            switch (game)
            {
                case 1:
                    entry = new ME1ExportEntry((ME1PCCObject)pcc);
                    break;
                case 2:
                    entry = new ME2ExportEntry((ME2PCCObject)pcc);
                    break;
                case 3:
                    entry = new ME3ExportEntry((ME3PCCObject)pcc, importData, exportOffset);
                    break;
            }
            return entry;
        }
        #endregion Creation


        protected AbstractExportEntry(AbstractPCCObject pcc)
        {
            pccRef = pcc;
        }


        #region Properties
        public byte[] info { get; set; } //Properties, not raw data
        public int ClassNameID { get { return BitConverter.ToInt32(info, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 0, sizeof(int)); } }
        public int LinkID { get { return BitConverter.ToInt32(info, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 8, sizeof(int)); } }
        public virtual int PackageNameID { get; set; }
        public virtual string PackageName { get; set; }
        public int ObjectNameID { get { return BitConverter.ToInt32(info, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 12, sizeof(int)); } }
        public virtual string ObjectName { get; set; }
        public virtual string PackageFullName { get; set; }
        public virtual string ClassName { get; set; }
        public byte[] flag
        {
            get
            {
                byte[] val = new byte[4];
                Buffer.BlockCopy(info, 28, val, 0, 4);
                return val;
            }
        }
        public long flagint
        {
            get
            {
                byte[] val = new byte[4];
                Buffer.BlockCopy(info, 28, val, 0, 4);
                return BitConverter.ToInt32(val, 0);
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public virtual AbstractPCCObject pccRef { get; set; }
        public int DataSize { get { return BitConverter.ToInt32(info, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 32, sizeof(int)); } }
        public virtual uint DataOffset { get { return BitConverter.ToUInt32(info, 36); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 36, sizeof(uint)); } }
        public virtual byte[] Data
        {
            get { byte[] val = new byte[DataSize]; pccRef.ListStream.Seek(DataOffset, SeekOrigin.Begin); val = pccRef.ListStream.ReadBytes(DataSize); return val; }
            set
            {
                if (value.Length > DataSize)
                {
                    pccRef.ListStream.Seek(0, SeekOrigin.End);
                    DataOffset = (uint)pccRef.ListStream.Position;
                    pccRef.ListStream.WriteBytes(value);
                    pccRef.LastExport = this;
                }
                else
                {
                    pccRef.ListStream.Seek(DataOffset, SeekOrigin.Begin);
                    pccRef.ListStream.WriteBytes(value);
                }
                if (value.Length != DataSize)
                {
                    DataSize = value.Length;
                    pccRef.ListStream.Seek(InfoOffset, SeekOrigin.Begin);
                    pccRef.ListStream.WriteBytes(info);
                }
            }
        }
        public bool hasChanged { get; set; }
        public int InfoOffset { get; set; }
        public long ObjectFlags { get; set; }
        #endregion Properties


        /// <summary>
        /// Returns true if current instance is a valid texture class.
        /// </summary>
        public bool ValidTextureClass()
        {
            return ClassName == "Texture2D" || ClassName == "LightMapTexture2D" || ClassName == "TextureFlipBook";
        }
    }
}
