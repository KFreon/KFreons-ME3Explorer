using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings;
using UsefulThings.WPF;

namespace WPF_ME3Explorer.Textures
{
    public class TreeTexFolders
    {
        public Predicate<object> Filter { get; set; }
        public int TexCount { get; set; }
        public string FolderName { get; set; }

        string path = null;
        public string FolderPath
        {
            get
            {
                if (path == null)
                {
                    if (Parent != null)
                        path = Parent.FolderPath + "." + FolderName;
                    else
                        path = FolderName;
                }

                return path;
            }
        }
        public TreeTexFolders Parent { get; set; }
        public MTRangedObservableCollection<TreeTexFolders> Children { get; set; }


        public TreeTexFolders(string name, TreeTexFolders parent)
        {
            Filter = tex => ((TreeTexInfo)tex).FullPackage.Contains(FolderPath);
        }

        public void CreatePath(string[] nodes)
        {
            foreach (var child in Children)
            {
                if (child.FolderName == nodes[0])
                {
                    child.CreatePath(nodes.GetRange(1));
                    return;
                }
            }

            // Folder has no matching children so make a new one and create path
            TreeTexFolders folder = new TreeTexFolders(nodes[0], this);

            if (nodes.Length > 1)
                folder.CreatePath(nodes.GetRange(1));

            Children.Add(folder);
        }
    }
}
