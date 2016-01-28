using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPF_ME3Explorer.PCCObjects;

namespace WPF_ME3Explorer.PCCObjects
{
    public class ME2PCCObject : AbstractPCCObject
    {
        public ME2PCCObject(string path)
            : base(path)
        {
            GameVersion = 2;
            LoadFromStream(tempStream);
            tempStream.Dispose();
        }

        public ME2PCCObject(string path, MemoryStream stream)
            : base(path)
        {
            GameVersion = 2;
            LoadFromStream(stream);
        }
    }
}
