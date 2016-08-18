using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer.PCCObjectsAndBits
{
    public class ImportEntry
    {
        public static int byteSize = 28;
        public byte[] data = new byte[byteSize];
        internal PCCObject pccRef;
        public int link { get; set; }
        public string Name
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int PackageFileID { get { return BitConverter.ToInt32(data, 0); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 0, sizeof(int)); } }
        public int ClassNameID { get { return BitConverter.ToInt32(data, 8); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 8, sizeof(int)); } }
        public int PackageNameID { get { return BitConverter.ToInt32(data, 16) - 1; } private set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, data, 16, sizeof(int)); } }
        public int ObjectNameID { get { return BitConverter.ToInt32(data, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 20, sizeof(int)); } }
        public int LinkID { get { return BitConverter.ToInt32(data, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 16, sizeof(int)); } }
        public long ObjectFlags { get { return BitConverter.ToInt32(data, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 24, sizeof(int)); } }

        public string ClassName
        {
            get { return pccRef.Names[ClassNameID]; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string PackageFile { get { return pccRef.Names[PackageFileID] + ".pcc"; } }
        public string ObjectName
        {
            get { return pccRef.Names[ObjectNameID]; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string PackageName { get { int val = PackageNameID; if (val >= 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "Package"; } }
        public string PackageFullName
        {
            get
            {
                string result = PackageName;
                int idxNewPackName = PackageNameID;

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

        public ImportEntry(PCCObject pccFile, byte[] importData)
        {
            pccRef = pccFile;
            //data = (byte[])importData.Clone();  // COULD BE PROBLEM HERE
            data = importData;
        }

        public ImportEntry(PCCObject pccFile, Stream importData)
        {
            pccRef = pccFile;
            data = new byte[ImportEntry.byteSize];
            importData.Read(data, 0, data.Length);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                this.data = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~ImportEntry()
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
