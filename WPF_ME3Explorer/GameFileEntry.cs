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
        public GameFileEntry(string path, MEDirectories.MEDirectories gameDirecs) : base()
        {
            Name = path.Remove(0, gameDirecs.BasePathLength);
            FilePath = path;
        }
    }
}
