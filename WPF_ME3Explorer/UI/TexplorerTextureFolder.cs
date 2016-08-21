using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI
{
    public class TexplorerTextureFolder : ViewModelBase, IComparable
    {
        bool isOpen = false;
        public bool IsOpen
        {
            get
            {
                return isOpen;
            }
            set
            {
                SetProperty(ref isOpen, value);
            }
        }

        bool isSelect = false;
        public bool IsSelect
        {
            get
            {
                return isSelect;
            }
            set
            {
                SetProperty(ref isSelect, value);
            }
        }

        public string Filter { get; set; } = null;

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

        public MTRangedObservableCollection<TexplorerTextureFolder> Folders { get; set; } = new MTRangedObservableCollection<TexplorerTextureFolder>();
        public MTRangedObservableCollection<TreeTexInfo> Textures { get; set; } = new MTRangedObservableCollection<TreeTexInfo>();

        public TexplorerTextureFolder(string folderName, string filter)
        {
            Name = folderName;
            Filter = filter;
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException("Argument cannot be null.");

            return Name.CompareTo(((TexplorerTextureFolder)obj).Name);
        }
    }
}
