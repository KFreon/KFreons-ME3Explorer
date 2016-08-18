using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.Textures
{
    public class PCCEntry : ViewModelBase
    {
        string name = null;
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                SetProperty(ref name, value);
            }
        }

        int expID = -1;
        public int ExpID
        {
            get
            {
                return expID;
            }
            set
            {
                SetProperty(ref expID, value);
            }
        }

        public PCCEntry(string name, int expid)
        {
            Name = name;
            ExpID = expid;
        }

        public override string ToString()
        {
            return $"{Name} @ {ExpID}";
        }
    }
}
