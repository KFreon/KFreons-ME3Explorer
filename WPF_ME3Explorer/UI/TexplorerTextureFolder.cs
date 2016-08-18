using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.UI
{
    public class TexplorerTextureFolder : ViewModelBase
    {
        public static ICollectionView view = null;

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
                view.Refresh();
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
                view.Refresh();
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

        public TexplorerTextureFolder(string folderName, string filter)
        {
            Name = folderName;
            Filter = filter;
        }
    }
}
