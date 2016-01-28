using System;
using System.IO;
using System.Linq;
using WPF_ME3Explorer.PCCObjects;
using UsefulThings;
using Gibbed.IO;

namespace WPF_ME3Explorer.PCCObjects.ExportEntries
{
    public class ME1ExportEntry : AbstractExportEntry
    {
        public override string PackageName
        {
            get
            {
                string temppack = PackageFullName;
                if (temppack == "." || String.IsNullOrEmpty(PackageFullName))
                    return "";
                temppack = temppack.Remove(temppack.Length - 1);

                int numBits = temppack.Count(t => t == '.');

                if (numBits > 1)
                    return temppack.Substring(temppack.LastIndexOf('.'));
                else
                    return temppack.Substring(0, temppack.IndexOf('.'));
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override byte[] Data
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
                    MoveNames();
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


        public ME1ExportEntry(ME1PCCObject pcc) : base(pcc)
        {
        }


        private void MoveNames()
        {
            pccRef.NameOffset = (int)pccRef.ListStream.Position;
            foreach (string name in pccRef.Names)
            {
                if (name != null)
                {
                    pccRef.ListStream.WriteInt32ToStream(name.Length + 1);
                    pccRef.ListStream.WriteStringToStream(name);
                    pccRef.ListStream.WriteByte(0);
                    pccRef.ListStream.WriteInt32ToStream(0);
                    pccRef.ListStream.WriteInt32ToStream(458768);
                }
            }
        }
    }
}
