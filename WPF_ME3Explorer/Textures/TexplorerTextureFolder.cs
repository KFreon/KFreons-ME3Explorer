using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.Textures
{
    public class TexplorerTextureFolder : ViewModelBase, IComparable, ITreeSeekable
    {
        public TexplorerTextureFolder ParentFolder = null;

        internal static Action<TreeTexInfo[]> RegenerateThumbsDelegate { get; set; }


        CommandHandler regenerateSubThumbsCommand = null;
        public CommandHandler RegenerateSubThumbsCommand
        {
            get
            {
                if (regenerateSubThumbsCommand == null)
                {
                    if (RegenerateThumbsDelegate == null)
                        return null;

                    regenerateThumbsCommand = new CommandHandler(new Action(async () => 
                    await Task.Run(
                        () => RegenerateThumbsDelegate(TexturesInclSubs.ToArray())
                    )));
                }

                return regenerateSubThumbsCommand;
            }
        }

        CommandHandler regenerateThumbsCommand = null;
        public CommandHandler RegenerateThumbsCommand
        {
            get
            {
                if (regenerateThumbsCommand == null)
                {
                    if (RegenerateThumbsDelegate == null)
                        return null;

                    regenerateThumbsCommand = new CommandHandler(new Action(async () => 
                    await Task.Run(
                        () => RegenerateThumbsDelegate(Textures.ToArray())
                    )));
                }

                return regenerateThumbsCommand;
            }
        }

        List<TreeTexInfo> texturesInclSubs = null;
        public List<TreeTexInfo> TexturesInclSubs
        {
            get
            {
                if (texturesInclSubs == null)
                {
                    texturesInclSubs = new List<TreeTexInfo>(Textures);

                    if (Folders?.Count > 0)
                        foreach (var folder in Folders)
                            texturesInclSubs.AddRange(folder.TexturesInclSubs);
                }

                return texturesInclSubs;
            }
        }

        int texcount = 0;
        public int TotalTextureCount
        {
            get
            {
                if (texcount == 0)
                {
                    if (Textures?.Count > 0)
                        texcount += Textures.Count;

                    if (Folders?.Count > 0)
                        foreach (var folder in Folders)
                            texcount += folder.TotalTextureCount;
                }

                return texcount;
            }
        }

        bool isSelect = false;
        public bool IsSelected
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

        bool isExpanded = false;
        public bool IsExpanded
        {
            get
            {
                return isExpanded;
            }

            set
            {
                SetProperty(ref isExpanded, value);
            }
        }

        public IEnumerable<ITreeSeekable> Children
        {
            get
            {
                return Folders;
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public ITreeSeekable Parent
        {
            get
            {
                return ParentFolder;
            }

            set
            {
                throw new NotImplementedException();
            }
        }


        public TexplorerTextureFolder(string folderName, string filter, TexplorerTextureFolder parent)
        {
            ParentFolder = parent;
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
