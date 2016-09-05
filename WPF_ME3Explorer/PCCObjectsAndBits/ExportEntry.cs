using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings;

namespace WPF_ME3Explorer.PCCObjectsAndBits
{
    public class ExportEntry : ICloneable
    {
        public byte[] info { get; set; } // holds data about export header, not the export data.
        public PCCObject pccRef { get; set; }
        public uint InfoOffset { get; private set; }
        public uint offset { get; set; }

        public int ClassNameID { get { return BitConverter.ToInt32(info, 0); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 0, sizeof(int)); } }
        public int ClassParentID { get { return BitConverter.ToInt32(info, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 4, sizeof(int)); } }
        public int LinkID { get { return BitConverter.ToInt32(info, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 8, sizeof(int)); } }
        public int PackageNameID { get { return BitConverter.ToInt32(info, 8) - 1; } set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, info, 8, sizeof(int)); } }
        public int ObjectNameID { get { return BitConverter.ToInt32(info, 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 12, sizeof(int)); } }
        public int indexValue { get { return BitConverter.ToInt32(info, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 16, sizeof(int)); } }
        public int ArchTypeNameID { get { return BitConverter.ToInt32(info, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 20, sizeof(int)); } }

        public string ArchtypeName
        {
            get { int val = ArchTypeNameID; if (val < 0) return pccRef.Names[pccRef.Imports[val * -1 - 1].ObjectNameID]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "None"; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public long ObjectFlags { get { return BitConverter.ToInt64(info, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 64, sizeof(long)); } }

        public string ObjectName { get { return pccRef.Names[ObjectNameID]; } }
        public string ClassName { get { int val = ClassNameID; if (val < 0) return pccRef.Names[pccRef.Imports[val * -1 - 1].ObjectNameID]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "Class"; } }
        public string ClassParent { get { int val = ClassParentID; if (val < 0) return pccRef.Names[pccRef.Imports[val * -1 - 1].ObjectNameID]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "Class"; } }
        public string PackageName
        {
            get
            {
                int val = PackageNameID;
                if (val >= 0)
                    return pccRef.Names[pccRef.Exports[val].ObjectNameID];
                else
                    return "Package";
            }
        }
        public string PackageFullName
        {
            get
            {
                string result = PackageName;
                int idxNewPackName = PackageNameID;  // ME1 uses LinkID instead of PackageNameID?

                while (idxNewPackName >= 0)
                {
                    string newPackageName = pccRef.Exports[idxNewPackName].PackageName;
                    if (newPackageName != "Package")
                        result = newPackageName + "." + result;
                    idxNewPackName = pccRef.Exports[idxNewPackName].PackageNameID;
                }
                return result;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public byte[] flag
        {
            get
            {
                byte[] val = new byte[4];
                Buffer.BlockCopy(info, 28, val, 0, 4);
                return val;
            }
        }

        public int flagInt
        {
            get
            {
                return BitConverter.ToInt32(flag, 0);
            }
        }

        public string FullPath
        {
            get
            {
                string s = "";
                if (PackageFullName != "Class" && PackageFullName != "Package")
                    s += PackageFullName + ".";
                s += ObjectName;
                return s;
            }
        }

        public int DataSize { get { return BitConverter.ToInt32(info, 32); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 32, sizeof(int)); } }
        public uint DataOffset
        {
            get
            {
                return BitConverter.ToUInt32(info, 36);
            }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 36, sizeof(uint));
            }
        }

        public bool hasChanged { get; set; }

        public byte[] Data { get { return GetData(); } set { SetData(value); hasChanged = true; } }

        private byte[] GetData()
        {
            MemoryStream ms = new MemoryStream(pccRef.listsStream.GetBuffer());
            {
                ms.Seek((long)DataOffset, SeekOrigin.Begin);
                return ms.ReadBytes(DataSize);
            }
        }

        private void SetData(byte[] newData)
        {
            pccRef.listsStream.Seek(pccRef.expDataEndOffset, SeekOrigin.Begin);
            DataOffset = (uint)pccRef.listsStream.Position;
            DataSize = newData.Length;
            pccRef.listsStream.WriteBytes(newData);

            RefreshInfo();
        }

        private void RefreshInfo()
        {
            pccRef.listsStream.Seek(InfoOffset, SeekOrigin.Begin);
            pccRef.listsStream.WriteBytes(info);
        }

        public ExportEntry(PCCObject pccFile, byte[] importData, uint exportOffset)
        {
            pccRef = pccFile;
            //info = (byte[])importData.Clone();  // COULD BE PROBLEM HERE
            info = importData;
            InfoOffset = exportOffset;
            hasChanged = false;
        }

        public ExportEntry()
        {

        }

        object ICloneable.Clone()
        {
            return this.Clone();
        }

        public ExportEntry Clone()
        {
            ExportEntry newExport = (ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
            // now creates new copies of referenced objects
            newExport.info = (byte[])this.info.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            return newExport;
        }


        public bool ValidTextureClass()
        {
            return PCCObject.ValidTexClass(ClassName);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                this.info = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~ExportEntry()
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
    }
}
