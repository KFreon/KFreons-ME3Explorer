using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class TexplorerViewModel : MEViewModelBase<TreeTexInfo>
    {
        public TexplorerViewModel() : base(Properties.Settings.Default.TexplorerGameVersion)
        {
            ItemsView.Filter = null;   // TODO: Read below

            // So here's the deal. Nodes on the left are going to be filters only.
            // The main view is going to be bound to the ItemsView as the other tools are, then clicking on the folder nodes sets a different filter to the ItemsView
            // The counts and stuff can still be set into those nodes somehow. 
            // How are the nodes generated? Same as old I guess, just the textures themselves don't get added?
        }
    }
}
