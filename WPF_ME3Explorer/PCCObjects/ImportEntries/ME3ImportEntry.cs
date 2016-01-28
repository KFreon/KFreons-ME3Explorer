using System;
using System.IO;

namespace WPF_ME3Explorer.PCCObjects.ImportEntries
{
    public class ME3ImportEntry : AbstractImportEntry
    {
        public static int byteSize = 28;
        public byte[] data = new byte[byteSize];

        public int idxPackageFile { get { return BitConverter.ToInt32(data, 0); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 0, sizeof(int)); } }
        public int idxClassName { get { return BitConverter.ToInt32(data, 8); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 8, sizeof(int)); } }
        public int idxPackageName { get { return BitConverter.ToInt32(data, 16) - 1; } private set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, data, 16, sizeof(int)); } }
        public override int ObjectNameID { get { return BitConverter.ToInt32(data, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 20, sizeof(int)); } }
        public int idxLink { get { return BitConverter.ToInt32(data, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 16, sizeof(int)); } }
        public long ObjectFlags { get { return BitConverter.ToInt32(data, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, 24, sizeof(int)); } }

        public string ClassName
        {
            get { return pccRef.Names[idxClassName]; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string PackageFile { get { return pccRef.Names[idxPackageFile] + ".pcc"; } }
        public string ObjectName
        {
            get { return pccRef.Names[ObjectNameID]; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string PackageName { get { int val = idxPackageName; if (val >= 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "Package"; } }
        public string PackageFullName
        {
            get
            {
                string result = PackageName;
                int idxNewPackName = idxPackageName;

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

        public ME3ImportEntry(ME3PCCObject pccFile, byte[] importData) : base(pccFile)
        {
            data = (byte[])importData.Clone();
        }

        public ME3ImportEntry(ME3PCCObject pccFile)
            : base(pccFile)
        {

        }

        public ME3ImportEntry(ME3PCCObject pccFile, Stream importData) : base(pccFile)
        {
            data = new byte[ME3ImportEntry.byteSize];
            importData.Read(data, 0, data.Length);
        }
    }
}
