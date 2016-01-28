using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.Textures
{
    public class HierarchicalTreeTexes : ViewModelBase
    {
        public HierarchicalTreeTexes Parent { get; set; }

        bool isexpanded = false;
        public bool IsExpanded
        {
            get
            {
                return isexpanded;
            }
            set
            {
                isexpanded = value;
                OnPropertyChanged();
            }
        }

        bool isselected = false;
        public bool IsSelected
        {
            get
            {
                return isselected;
            }
            set
            {
                isselected = value;
                OnPropertyChanged();
            }
        }



        // KFreon: Sub "folders"
        public ObservableCollection<HierarchicalTreeTexes> TreeTexes { get; set; }

        // KFreon: Textures contained within the top level of THIS NODE ONLY
        public ObservableCollection<TreeTexInfo> Textures { get; set; }

        // KFreon: Count of ALL textures in this node incl subfolders
        int fullTexCount = -1;
        public int FullTexCount
        {
            get
            {
                // KFreon: If necessary, recursively set texture count
                if (fullTexCount == -1)
                {
                    fullTexCount = 0;
                    if (TreeTexes != null)
                        foreach (HierarchicalTreeTexes treetex in TreeTexes)
                            fullTexCount += treetex.FullTexCount;

                    if (Textures != null)
                        fullTexCount += Textures.Count;
                }

                return fullTexCount;
            }
        }

        public string Name { get; set; }

        public bool IsTexture
        {
            get
            {
                return TreeTexes.Count == 0;
            }
        }

        public HierarchicalTreeTexes(TreeTexInfo tex)
        {

        }

        public HierarchicalTreeTexes(string name)
        {
            TreeTexes = new ObservableCollection<HierarchicalTreeTexes>();
            Name = name;
            Textures = new ObservableCollection<TreeTexInfo>();
        }

        public HierarchicalTreeTexes(string name, ObservableCollection<HierarchicalTreeTexes> children)
            : this(name)
        {
            TreeTexes = children;
        }

    }
}
