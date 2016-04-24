using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.MEDirectories
{
    public class FileEntry : ViewModelBase
    {
        public bool inTree
        {
            get; set;
        }

        bool isUsing = true;
        public virtual bool Using
        {
            get
            {
                return isUsing;
            }
            set
            {
                SetProperty(ref isUsing, value);
            }
        }

        string name = null;
        public virtual string Name
        {
            get
            {
                return Path.GetFileNameWithoutExtension(FullPath);
            }
            set
            {
                SetProperty(ref name, value);  // KFreon: Only for allowing a setter on derived classes
            }
        }

        public DateTime DateModified
        {
            get
            {
                return new FileInfo(FullPath)?.LastWriteTime ?? new DateTime(0);
            }
        }

        long size = 0;
        public virtual long Size
        {
            get
            {
                return (new FileInfo(FullPath)?.Length ?? 0);
            }
            set
            {
                SetProperty(ref size, value);  // KFreon: Only for allowing a setter on derived classes
                OnPropertyChanged(nameof(SizeString));
            }
        }

        public string SizeString
        {
            get
            {
                return UsefulThings.General.GetFileSizeAsString(Size);
            }
        }

        public string FullPath { get; set; }

        public FileEntry(string path)
        {
            FullPath = path;
        }
    }
}
