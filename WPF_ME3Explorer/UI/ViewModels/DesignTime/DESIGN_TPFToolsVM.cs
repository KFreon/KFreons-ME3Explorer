using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer.UI.ViewModels.DesignTime
{
    public class DESIGN_TPFToolsVM : TPFToolsViewModel
    {
        public DESIGN_TPFToolsVM() : base()
        {
            var texes = ProcessLoadingFiles(new List<string>() { @"R:\Latest HD ME3 Textures\done\Anderson\done\MASSEFFECT3.EXE_0x77B54F3A_Anderson_diff.dds" }, 1);
            Textures.AddRange(texes);
            Textures[0].EnumerateDetails();
        }
    }
}
