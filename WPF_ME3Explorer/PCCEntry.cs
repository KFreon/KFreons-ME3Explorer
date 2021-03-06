﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace WPF_ME3Explorer
{
    public class PCCEntry : ViewModelBase
    {
        public bool CheckBoxListenerAttached = false;

        int basePathLength = 0;

        public int Tree_ScannedPCCIndex = -1;

        bool isChecked = true;
        public bool IsChecked
        {
            get
            {
                return isChecked;
            }
            set
            {
                SetProperty(ref isChecked, value);
            }
        }


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
                OnPropertyChanged(nameof(DisplayName));
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

        public PCCEntry(string name, int expid, MEDirectories.MEDirectories gameDirecs, int treeInd = -1)
        {
            Name = name;
            ExpID = expid;
            basePathLength = gameDirecs.BasePathLength;
            Tree_ScannedPCCIndex = treeInd;
        }

        public override string ToString()
        {
            return $"{Name.Remove(0, basePathLength)} @ {ExpID}";
        }

        public string DisplayName
        {
            get
            {
                return Name.Remove(0, basePathLength);
            }
        }
    }
}
