using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace WPF_ME3Explorer
{
    public abstract class AbstractFileEntry : ViewModelBase
    {
        internal static Action Updater = null;

        bool isChecked = false;
        public virtual bool IsChecked
        {
            get
            {
                return isChecked;
            }
            set
            {
                SetProperty(ref isChecked, value);
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
