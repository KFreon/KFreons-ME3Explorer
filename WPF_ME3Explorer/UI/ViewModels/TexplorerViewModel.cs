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

        }
    }
}
