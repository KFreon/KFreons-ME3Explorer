using System;
using System.IO;
using WPF_ME3Explorer.PCCObjects;
using BitConverter = WPF_ME3Explorer.BitConverter;
using UsefulThings;
using Gibbed.IO;

namespace WPF_ME3Explorer.PCCObjects.ExportEntries
{
    public class ME3ExportEntry : AbstractExportEntry, ICloneable
    {
        public uint offset { get; set; }
        public int idxClassParent { get { return BitConverter.ToInt32(info, 4); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 4, sizeof(int)); } }
        public override int PackageNameID { get { return BitConverter.ToInt32(info, 8) - 1; } set { Buffer.BlockCopy(BitConverter.GetBytes(value + 1), 0, info, 8, sizeof(int)); } }
        public int indexValue { get { return BitConverter.ToInt32(info, 16); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 16, sizeof(int)); } }
        public int idxArchtypeName { get { return BitConverter.ToInt32(info, 20); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 20, sizeof(int)); } }
        public string ArchtypeName
        {
            get { int val = idxArchtypeName; if (val < 0) return pccRef.Names[pccRef.Imports[val * -1 - 1].ObjectNameID]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "None"; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public long ObjectFlags { get { return BitConverter.ToInt64(info, 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 64, sizeof(long)); } }

        public override string ObjectName { get { return pccRef.Names[ObjectNameID]; } set { throw new NotImplementedException(); } }
        public override string ClassName { get { int val = ClassNameID; if (val < 0) return pccRef.Names[pccRef.Imports[val * -1 - 1].ObjectNameID]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "Class"; } set { throw new NotImplementedException(); } }
        public string ClassParent { get { int val = idxClassParent; if (val < 0) return pccRef.Names[pccRef.Imports[val * -1 - 1].ObjectNameID]; else if (val > 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "Class"; } }
        public override string PackageName
        {
            get { int val = PackageNameID; if (val >= 0) return pccRef.Names[pccRef.Exports[val].ObjectNameID]; else return "Package"; }
            set
            {
                throw new NotImplementedException();
            }
        }
        public override string PackageFullName
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

        public string GetFullPath
        {
            get
            {
                string s = "";
                if (PackageFullName != "Class" && PackageFullName != "Package")
                    s += PackageFullName + ".";
                s += ObjectName;
                return s;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        internal uint dataoff = 0;
        public int DataOffsetTmp { get; set; }
        public override uint DataOffset
        {
            get
            {
                /*if (WPF_ME3Explorer.Misc.Methods.FindInStack("CloneDialog"))
                    return dataoff;
                else*/
                return BitConverter.ToUInt32(info, 36);
            }
            set
            {
                /*if (WPF_ME3Explorer.Misc.Methods.FindInStack("CloneDialog"))
                    dataoff = value;
                else*/
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, info, 36, sizeof(uint));
            }
        }
        byte[] _data = null;
        public override byte[] Data { get { return GetData(); } set { SetData(value); hasChanged = true; } }

        private byte[] GetData()
        {
            if (_data == null)
            {
                pccRef.ListStream.Seek((long)DataOffset, SeekOrigin.Begin);
                return pccRef.ListStream.ReadBytes(DataSize);
            }
            else
                return _data;
        }

        private void SetData(byte[] newData)
        {
            pccRef.ListStream.Seek(pccRef.expDataEndOffset, SeekOrigin.Begin);
            DataOffset = (uint)pccRef.ListStream.Position;
            DataSize = newData.Length;
            pccRef.ListStream.WriteBytes(newData);

            RefreshInfo();
        }

        private void RefreshInfo()
        {
            pccRef.ListStream.Seek(InfoOffset, SeekOrigin.Begin);
            pccRef.ListStream.WriteBytes(info);
        }

        public ME3ExportEntry(ME3PCCObject pccFile, byte[] importData, int exportOffset) : base(pccFile)
        {
            info = (byte[])importData.Clone();
            InfoOffset = exportOffset;
            hasChanged = false;
        }

        public ME3ExportEntry(ME3PCCObject pcc)
            : base(pcc)
        {

        }

        object ICloneable.Clone()
        {
            ME3ExportEntry newExport = (ME3ExportEntry)this.MemberwiseClone(); // copy all reference-types vars
            // now creates new copies of referenced objects
            newExport.info = (byte[])this.info.Clone();
            newExport.Data = (byte[])this.Data.Clone();
            return newExport;
        }
    }
}
