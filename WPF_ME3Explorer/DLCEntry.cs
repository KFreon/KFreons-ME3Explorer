using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer
{
    public class DLCEntry : AbstractFileEntry
    {
        public List<GameFileEntry> Files { get; set; } = new List<GameFileEntry>();

        public override bool IsChecked
        {
            get
            {
                return base.IsChecked;
            }
            set
            {
                Files.ForEach(file => file.FilterOut = value);
                base.IsChecked = value;
            }
        }

        public DLCEntry(string name, List<string> files, MEDirectories.MEDirectories gameDirecs)
        {
            Name = name;
            if (files == null)
                return;

            foreach (string file in files)
            {
                GameFileEntry entry = new GameFileEntry(file, gameDirecs);
                Files.Add(entry);
            }
        }
    }
}
