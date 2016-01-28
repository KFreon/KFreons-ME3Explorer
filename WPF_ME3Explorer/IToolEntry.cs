using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.PCCObjects;

namespace WPF_ME3Explorer
{
    public interface IToolEntry
    {
        string EntryName { get; set; }
        MTRangedObservableCollection<PCCEntry> PCCs { get; set; }
        bool IsSelected { get; set; }
    }
}
