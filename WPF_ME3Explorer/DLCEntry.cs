using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;
using WPF_ME3Explorer.UI.ViewModels;

namespace WPF_ME3Explorer
{
    public class DLCEntry : AbstractFileEntry
    {
        public MTRangedObservableCollection<GameFileEntry> Files { get; set; } = new MTRangedObservableCollection<GameFileEntry>();

        public override bool? IsExcluded
        {
            get
            {
                return base.IsExcluded;
            }
            set
            {
                var current = TexplorerViewModel.DisableFTSUpdating;  // Ensures that external setting of this value is not messed with.
                TexplorerViewModel.DisableFTSUpdating = true;
                bool? fileSetting = value == true ? null : (bool?)false;
                foreach (var entry in Files)
                    entry.IsExcluded = fileSetting;  // File setting is null when indirectly excluded. e.g. here.

                base.IsExcluded = value == true;  // Don't want to be able to set to null by clicking.

                TexplorerViewModel.DisableFTSUpdating = current;
                Updater();
            }
        }

        public DLCEntry(string name, List<string> files, MEDirectories.MEDirectories gameDirecs)
        {
            Name = name;
            if (files == null)
                return;

            foreach (string file in files)
            {
                GameFileEntry entry = new GameFileEntry(file, gameDirecs);

                // Wire up trigger to change IsEnabled for the DLC when the files' IsEnabled is changed.
                entry.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(IsExcluded))
                    {
                        var current = TexplorerViewModel.DisableFTSUpdating;
                        TexplorerViewModel.DisableFTSUpdating = true;

                        switch (IsExcluded)
                        {
                            // First two, set the intermediate stage. DLC is already set to the extremes, so can only go to null from here.
                            case true:
                                base.IsExcluded = null;  // Set on base as we don't want the side effects of changing all Files' IsExcluded.
                                break;
                            case false:
                                base.IsExcluded = null;
                                break;
                            case null:
                                // Need to decide if a change is required.

                                var tempFile = (GameFileEntry)sender;
                                if (tempFile.IsExcluded == true && Files.All(t => t.IsExcluded == true))  // Check if need to set DLC.IsExcluded = true.
                                    base.IsExcluded = true;
                                else if (tempFile.IsExcluded == false && !Files.Any(t => t.IsExcluded == true))
                                    base.IsExcluded = false;
                                break;
                        }

                        TexplorerViewModel.DisableFTSUpdating = current;
                    }
                };
                Files.Add(entry);
            }
        }
    }
}
