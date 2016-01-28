using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AmaroK86.MassEffect3.ZlibBlock;
using WPF_ME3Explorer.PCCObjects;
using UsefulThings;
using WPF_ME3Explorer.PCCObjects.ExportEntries;
using WPF_ME3Explorer.PCCObjects.ImportEntries;
using Gibbed.IO;

namespace WPF_ME3Explorer.PCCObjects
{
    public class ME3PCCObject : AbstractPCCObject
    {
        const int headerSize = 0x8E;

        public ME3PCCObject(string path)
            : base(path)
        {
            GameVersion = 3;
            LoadFromStream(tempStream);
            tempStream.Dispose();
        }

        public ME3PCCObject(string path, MemoryStream stream)
            : base(path)
        {
            GameVersion = 3;
            LoadFromStream(stream);
        }

        protected override void LoadFromStream(MemoryStream tempStream)
        {
            tempStream.Seek(0, SeekOrigin.Begin);
            DataStream = UsefulThings.RecyclableMemoryManager.GetStream();
            tempStream.WriteTo(DataStream);
            Names = new List<string>();
            Imports = new List<AbstractImportEntry>();
            Exports = new List<AbstractExportEntry>();

            header = tempStream.ReadBytes(headerSize);
            if (magic != ZBlock.magic &&
                    magic.Swap() != ZBlock.magic)
                throw new FormatException(pccFileName + " is not a pcc file");

            if (lowVers != 684 && highVers != 194)
                throw new FormatException("unsupported version");

            if (bCompressed)
            {
                // seeks the blocks info position
                tempStream.Seek(Offsets + 60, SeekOrigin.Begin);
                int generator = tempStream.ReadInt32FromStream();
                tempStream.Seek((generator * 12) + 20, SeekOrigin.Current);

                int blockCount = tempStream.ReadInt32FromStream();
                blockList = new List<Block>();

                // creating the Block list
                for (int i = 0; i < blockCount; i++)
                {
                    Block temp = new Block();
                    temp.uncOffset = tempStream.ReadInt32FromStream();
                    temp.uncSize = tempStream.ReadInt32FromStream();
                    temp.cprOffset = tempStream.ReadInt32FromStream();
                    temp.cprSize = tempStream.ReadInt32FromStream();
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
                using (MemoryStream he = UsefulThings.RecyclableMemoryManager.GetStream(header))
                {
                    he.Seek(0, SeekOrigin.Begin);
                    he.ReadInt32FromStream();
                    he.ReadInt32FromStream();
                    dataStart = he.ReadInt32FromStream();
                }


                //Decompress ALL blocks
                ListStream = UsefulThings.RecyclableMemoryManager.GetStream();
                for (int i = 0; i < blockCount; i++)
                {
                    tempStream.Seek(blockList[i].cprOffset, SeekOrigin.Begin);
                    ListStream.Seek(blockList[i].uncOffset, SeekOrigin.Begin);
                    ListStream.WriteBytes(ZBlock.Decompress(tempStream, blockList[i].cprSize));
                }

                bCompressed = false;
            }
            else
            {
                ListStream = UsefulThings.RecyclableMemoryManager.GetStream();
                ListStream.WriteBytes(tempStream.ToArray());
            }
            tempStream.Dispose();

            //Fill name list
            ListStream.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                int strLength = ListStream.ReadInt32FromStream();
                Names.Add(ListStream.ReadStringFromStream());
            }

            // fill import list
            ListStream.Seek(ImportOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[ME3ImportEntry.byteSize];
            for (int i = 0; i < ImportCount; i++)
            {
                Imports.Add(AbstractImportEntry.Create(3, this, ListStream));
            }

            //fill export list
            ListStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                uint expInfoOffset = (uint)ListStream.Position;

                ListStream.Seek(44, SeekOrigin.Current);
                int count = ListStream.ReadInt32FromStream();
                ListStream.Seek(-48, SeekOrigin.Current);

                int expInfoSize = 68 + (count * 4);
                buffer = new byte[expInfoSize];

                ListStream.Read(buffer, 0, buffer.Length);
                Exports.Add(new ME3ExportEntry(this, buffer, (int)expInfoOffset));
            }
        }

        public override void SaveToFile(string path)
        {
            //Refresh header and namelist
            ListStream.Seek(expDataEndOffset, SeekOrigin.Begin);
            NameOffset = (int)ListStream.Position;
            NameCount = Names.Count;
            foreach (string name in Names)
            {
                ListStream.WriteInt32ToStream(-(name.Length + 1));
                ListStream.WriteStringToStream(name);
            }

            ListStream.Seek(0, SeekOrigin.Begin);
            ListStream.WriteBytes(header);

            while (true)
            {
                int tries = 0;
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        byte[] test = ListStream.ToArray();
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
            ListStream.Dispose();
            Exports.Clear();
            Imports.Clear();
            Names.Clear();
            Exports.Clear();
            Imports.Clear();
            Names.Clear();
        }
    }
}
