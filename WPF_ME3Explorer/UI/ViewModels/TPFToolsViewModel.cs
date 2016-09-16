using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using SaltTPF;
using UsefulThings;
using UsefulThings.WPF;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.Textures;
using CSharpImageLibrary;
using System.ComponentModel;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class TPFToolsViewModel : MEViewModelBase<TPFTexInfo>
    {
        bool analysed = false;
        public bool Analysed
        {
            get
            {
                return analysed;
            }
            set
            {
                SetProperty(ref analysed, value);
            }
        }
        
        List<ZipReader> Zippys = new List<ZipReader>();
        public ICollectionView MainDisplayView { get; set; }

        public TPFToolsViewModel() : base()
        {
            DebugOutput.StartDebugger("TPFTools");

            MainDisplayView = CollectionViewSource.GetDefaultView(Textures);
            MainDisplayView.Filter = item => !((TPFTexInfo)item).IsHidden;

            GameDirecs.GameVersion = Properties.Settings.Default.TexplorerGameVersion;
            OnPropertyChanged(nameof(GameVersion));

            BeginTreeLoading();
        }

        internal void LoadFiles(string[] fileNames)
        {
            Busy = true;
            Progress = 0;
            MaxProgress = fileNames.Length;
            int prevTexCount = Textures.Count;

            foreach (var file in fileNames)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                switch (ext)
                {
                    case ".tpf":
                    case ".metpf":
                        LoadTPF(file);
                        Status = $"Loading textures from {Path.GetFileName(file)}...";
                        break;
                    case ".jpg":
                    case ".jpeg":
                    case ".bmp":
                    case ".png":
                    case ".tga":
                    case ".dds":
                        LoadSingle(file);
                        Status = $"Loading {Path.GetFileName(file)}...";
                        break;
                }

                Progress++;
            }


            MainDisplayView.Refresh();
            Status = $"Loaded {Textures.Count - prevTexCount} from {fileNames.Length} files.";
            Progress = MaxProgress;
            Busy = false;
        }

        void LoadSingle(string file, string def_HashString = null, ZipReader.ZipEntryFull entry = null)
        {
            TPFTexInfo tex = new TPFTexInfo(file, entry, GameDirecs);

            if (!tex.IsDef)
            {
                uint defHash = !String.IsNullOrEmpty(def_HashString) ? FindHashInString(def_HashString, "|") : 0;  // Look in def
                uint nameHash = FindHashInString(tex.FileName, "0x");  // Look in filename
                tex.Hash = defHash == 0 ? nameHash : defHash;  // Prefer defHash
                if (tex.Hash == 0)
                    DebugOutput.PrintLn($"Unable to find hash for texture: {tex.FileName}.");

                // Get Details
                tex.GetDetails();
            }

            Textures.Add(tex);
        }

        void LoadTPF(string tpf)
        {
            ZipReader zippy = new ZipReader(tpf);

            // Load Hashes from texmod.def (last entry)
            byte[] data = zippy.Entries.Last().Extract(true);
            char[] chars = Array.ConvertAll(data, item => (char)item);

            // Fix formatting, fix case, remove duplpicates, remove empty entries.
            List<string> Name_Hashes = new string(chars).Replace("\r", "").Replace("_0X", "_0x").Split('\n').Distinct().Where(s => s != "\0").ToList();

            foreach (var entry in zippy.Entries)
            {
                // Find and set hash
                string name_hash = Name_Hashes.Find(name => name.Contains(entry.Filename, StringComparison.OrdinalIgnoreCase));
                LoadSingle(entry.Filename, name_hash, entry);
            }

            Zippys.Add(zippy);
        }

        uint FindHashInString(string hashString, string indicator)
        {
            int ind = hashString.IndexOf(indicator, StringComparison.OrdinalIgnoreCase);
            if (ind != -1)
            {
                if (hashString.Substring(ind, 2) == "0x")
                    return ToolsetTextureEngine.FormatTexmodHashAsUint(hashString.Substring(ind, 10));
                else
                    return ToolsetTextureEngine.FormatTexmodHashAsUint(hashString.Substring(0, ind));
            }

            return 0;
        }
    }
}
