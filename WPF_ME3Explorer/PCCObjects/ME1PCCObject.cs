using System;
using System.Collections.Generic;
using System.IO;
using WPF_ME3Explorer.PCCObjects;
using UsefulThings;
using WPF_ME3Explorer.Debugging;
using Gibbed.IO;

namespace WPF_ME3Explorer.PCCObjects
{
    public class ME1PCCObject : AbstractPCCObject
    {
        public ME1PCCObject(string path)
            : base(path)
        {
            GameVersion = 1;
            LoadFromStream(tempStream);
            tempStream.Dispose();
        }

        public ME1PCCObject(string path, MemoryStream stream)
            : base(path)
        {
            GameVersion = 1;
            LoadFromStream(stream);
        }

        public override void SaveToFile(string path)
        {
            DebugOutput.PrintLn("Writing pcc to: " + path + "\nRefreshing header to stream...");
            ListStream.Seek(0, SeekOrigin.Begin);
            ListStream.WriteBytes(header);
            DebugOutput.PrintLn("Opening filestream and writing to disk...");
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                ListStream.WriteTo(fs);
            }
        }
    }
}
