using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.UI.ViewModels;

namespace WPF_ME3Explorer
{
    public abstract class AbstractFileEntry : ViewModelBase
    {
        internal static Action Updater = null;

        bool? isExcluded = false;
        public virtual bool? IsExcluded
        {
            get
            {
                return isExcluded;
            }
            set
            {
                SetProperty(ref isExcluded, value);
                if (!TexplorerViewModel.DisableFTSUpdating)
                    Updater();
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
            }
        }

        string path = null;
        public string FilePath
        {
            get
            {
                return path;
            }
            set
            {
                SetProperty(ref path, value);
            }
        }
    }
}
