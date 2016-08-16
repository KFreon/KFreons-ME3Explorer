using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer
{
    public class GameFileEntry : AbstractFileEntry
    {
        bool filterOut = false;
        public bool FilterOut
        {
            get
            {
                return filterOut;
            }
            set
            {
                SetProperty(ref filterOut, value);
            }
        }

        public GameFileEntry(string path) : base()
        {
            Name = path.Remove(0, BasePathLength);
            FilePath = path;
        }
    }
}
