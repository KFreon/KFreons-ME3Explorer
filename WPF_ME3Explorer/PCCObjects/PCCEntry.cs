using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.PCCObjects
{
    public class PCCEntry : ViewModelBase
    {
        public Action<PCCEntry> PCCEntryChangeAction { get; set; }
        public bool IsPCCStored { get; set; }

        bool exists = false;
        public bool Exists
        {
            get
            {
                return exists;
            }
            set
            {
                SetProperty(ref exists, value);
            }
        }


        // KFreon: Object for storing values and settings related to a PCC filename and corresponding expID
        bool _using = true;
        public bool Using     // KFreon: Indicates whether entry is considered in main operations
        {
            get
            {
                return _using;
            }
            set
            {
                SetProperty(ref _using, value);
                if (PCCEntryChangeAction != null)
                    PCCEntryChangeAction(this);
            }
        }

        string file = null;
        public string File   // KFreon: PCC filename
        {
            get
            {
                return file;
            }
            set
            {
                SetProperty(ref file, value);
                if (PCCEntryChangeAction != null)
                    PCCEntryChangeAction(this);
            }
        }
        public string Display  // KFreon: String to display in GUI
        {
            get
            {
                return ToString();
            }
        }


        int expid = -1;
        public int ExpID    // KFreon: ExpID
        {
            get
            {
                return expid;
            }
            set
            {
                SetProperty(ref expid, value);
                if (PCCEntryChangeAction != null)
                    PCCEntryChangeAction(this);
            }
        }

        string expidstring = null;
        public string ExpIDString
        {
            get
            {
                if (expidstring == null && ExpID > 0)
                    expidstring = ExpID.ToString();

                return expidstring;
            }
        }

        public PCCEntry(Action<PCCEntry> pccEntryChangeAction = null)
        {
            Using = true;
            PCCEntryChangeAction = pccEntryChangeAction;
        }

        public PCCEntry(string pccName, int expID, Action<PCCEntry> pccEntryChangeAction = null)
            : this(pccEntryChangeAction)
        {
            File = pccName;
            Exists = System.IO.File.Exists(File);
            ExpID = expID;
        }


        public static List<PCCEntry> PopulatePCCEntries(List<string> pccs, List<int> ExpIDs, Action<PCCEntry> pccEntryChangeAction = null)
        {
            List<PCCEntry> Entries = new List<PCCEntry>();
            for (int i = 0; i < (pccs.Count > ExpIDs.Count ? pccs.Count : ExpIDs.Count); i++)
            {
                PCCEntry entry = new PCCEntry(pccEntryChangeAction);
                entry.File = pccs.Count <= i ? null : pccs[i];
                entry.ExpID = ExpIDs.Count <= i ? -1 : ExpIDs[i];
                if (entry.File == null || entry.ExpID == -1)
                    entry.Using = false;
                Entries.Add(entry);
            }
            return Entries;
        }

        public override string ToString()
        {
            return File + "  @ " + ExpID;
        }


        public override bool Equals(object obj)
        {
            if (Object.ReferenceEquals(obj, null))
                return false;

            if (obj.GetType() != typeof(PCCEntry))
                return false;

            return Equals((PCCEntry)obj);
        }

        public bool Equals(PCCEntry pcc)
        {
            if (Object.ReferenceEquals(pcc, null))
                return false;

            if (pcc.GetType() != typeof(PCCEntry))
                return false;

            return File == pcc.File && ExpID == pcc.ExpID;
        }

        public static bool operator ==(PCCEntry pcc1, PCCEntry pcc2)
        {
            if (Object.ReferenceEquals(pcc1, null))
                return false;

            if (pcc1.GetType() != typeof(PCCEntry))
                return false;

            return pcc1.Equals(pcc2);
        }

        public static bool operator !=(PCCEntry pcc1, PCCEntry pcc2)
        {
            if (Object.ReferenceEquals(pcc1, null))
                return false;

            if (pcc1.GetType() != typeof(PCCEntry))
                return false;

            return !pcc1.Equals(pcc2);
        }
    }
}
