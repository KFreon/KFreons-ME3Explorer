using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.MEDirectories
{
    public class DLCFileEntry : FileEntry
    {
        public MTRangedObservableCollection<FileEntry> Files { get; set; } = new MTRangedObservableCollection<FileEntry>();

        public bool IsVisible
        {
            get
            {
                return !(Files?.Any(t => !t.inTree) ?? false);
            }
        }

        public override bool Using
        {
            get
            {
                return base.Using;
            }

            set
            {
                foreach (var file in Files)
                    file.Using = value;
                
                base.Using = value;
            }
        }

        public ICollectionView FilesView { get; set; }


        long size = 0;
        public override long Size
        {
            get
            {
                return size;
            }

            set
            {
                SetProperty(ref size, value);
            }
        }

        public long SFARSize
        {
            get
            {
                string sfarPath = Files.Where(file => file.FullPath.EndsWith(".sfar")).FirstOrDefault().FullPath;
                if (string.IsNullOrEmpty(sfarPath))
                    return -1;

                FileInfo info = new FileInfo(sfarPath);
                return info.Length;
            }
        }

        public DLCFileEntry(string name, Func<bool> getNewOnlyCheckbox) : base(name)
        {
            Name = name;
            FilesView = CollectionViewSource.GetDefaultView(Files);
            FilesView.Filter = file =>
            {
                if (!getNewOnlyCheckbox())
                    return true;

                return !((FileEntry)file).inTree;
            };
        }

        public void CalculateSize()
        {
            long size = 0;
            foreach (FileEntry file in Files)
                size += file.Size;

            Size = size;
        }
    }
}
