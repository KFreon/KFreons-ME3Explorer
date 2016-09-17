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
using System.Threading.Tasks.Dataflow;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class TPFToolsViewModel : MEViewModelBase<TPFTexInfo>
    {
        public static string[] AcceptedExtensions = { ".dds", ".jpg", ".jpeg", ".bmp", ".png", ".tga", ".tpf", ".metpf" };

        public override void Search(string searchText)
        {
            base.Search(searchText);

            if (String.IsNullOrEmpty(searchText))
                foreach (var tex in Textures)
                    tex.IsHidden = false;
        }

        public override void ChangeSelectedTree(int game)
        {
            base.ChangeSelectedTree(game);

            // Re-analyse if required
            if (PreviouslyAnalysed)
            {
                UnAnalyse();
                AnalyseVsTree();
            }

            Properties.Settings.Default.TPFToolsGameVersion = game;
            Properties.Settings.Default.Save();
        }

        public bool AllAnalysed
        {
            get
            {
                return Textures.Count != 0 && Textures.All(tex => !tex.IsDef && tex.FoundInTree || tex.IsDef);
            }
        }

        bool PreviouslyAnalysed { get; set; }

        public override string TextureSearch
        {
            get
            {
                return base.TextureSearch;
            }

            set
            {
                base.TextureSearch = value;
                MainDisplayView.Refresh();
            }
        }

        List<ZipReader> Zippys = new List<ZipReader>();
        public ICollectionView MainDisplayView { get; set; }

        #region Commands
        CommandHandler clearAllCommand = null;
        public CommandHandler ClearAllCommand
        {
            get
            {
                if (clearAllCommand == null)
                    clearAllCommand = new CommandHandler(() => Textures.Clear());

                return clearAllCommand;
            }
        }

        CommandHandler analyseCommand = null;
        public CommandHandler AnalyseCommand
        {
            get
            {
                if (analyseCommand == null)
                    analyseCommand = new CommandHandler(() =>
                    {
                        AnalyseVsTree();
                    });

                return analyseCommand;
            }
        }
        #endregion Commands

        public TPFToolsViewModel() : base()
        {
            DebugOutput.StartDebugger("TPFTools");

            MainDisplayView = CollectionViewSource.GetDefaultView(Textures);
            MainDisplayView.Filter = item => !((TPFTexInfo)item).IsHidden;

            GameDirecs.GameVersion = Properties.Settings.Default.TPFToolsGameVersion;
            OnPropertyChanged(nameof(GameVersion));



            BeginTreeLoading();
        }

        internal async Task LoadFiles(string[] fileNames)
        {
            Busy = true;
            Progress = 0;
            MaxProgress = fileNames.Length;
            int prevTexCount = Textures.Count;

            /// Setup pipeline
            BufferBlock<string> fileBuffer = new BufferBlock<string>();
            TransformBlock<string, TPFTexInfo> singleTexMaker = new TransformBlock<string, TPFTexInfo>(file =>
            {
                Status = $"Loading {Path.GetFileName(file)}...";
                return BuildSingleTex(file);
            });
            TransformManyBlock<string, TPFTexInfo> tpfTexMaker = new TransformManyBlock<string, TPFTexInfo>(tpf =>
            {
                Status = $"Loading textures from {Path.GetFileName(tpf)}...";
                return LoadTPF(tpf);
            });

            // Want disk work to be done on a single thread.
            var texExtractor = new TransformBlock<TPFTexInfo, Tuple<TPFTexInfo, byte[]>>(tex => new Tuple<TPFTexInfo, byte[]>(tex, tex.Extract()), new ExecutionDataflowBlockOptions { BoundedCapacity = 2, MaxDegreeOfParallelism = 2 });  // Disk bound 
            var thumbBuilder = new ActionBlock<Tuple<TPFTexInfo, byte[]>>(tuple =>
            {
                tuple.Item1.GetDetails(tuple.Item2);
                Textures.Add(tuple.Item1);
                Progress++;
            }, new ExecutionDataflowBlockOptions { BoundedCapacity = NumThreads, MaxDegreeOfParallelism = NumThreads });

            // Connect pipeline
            fileBuffer.LinkTo(singleTexMaker, new DataflowLinkOptions { PropagateCompletion = true }, file => AcceptedExtensions.Where(ext => !ext.Contains("tpf")).Contains(Path.GetExtension(file)));
            fileBuffer.LinkTo(tpfTexMaker, new DataflowLinkOptions { PropagateCompletion = true }, file => AcceptedExtensions.Where(ext => ext.Contains("tpf")).Contains(Path.GetExtension(file)));
            singleTexMaker.LinkTo(texExtractor);
            tpfTexMaker.LinkTo(texExtractor);
            texExtractor.LinkTo(thumbBuilder, new DataflowLinkOptions { PropagateCompletion = true });

            // Start pipeline
            foreach (var file in fileNames)
                fileBuffer.Post(file);

            fileBuffer.Complete();

            // Due to pipeline topology one side can and will finish first, which then Completes everything else despite the other side still working.
            Task.WhenAll(fileBuffer.Completion, singleTexMaker.Completion, tpfTexMaker.Completion).ContinueWith(t => texExtractor.Complete());  

            await thumbBuilder.Completion;
            Status = $"Loaded {Textures.Count - prevTexCount} from {fileNames.Length} files.";
            Progress = MaxProgress;
            Busy = false;
        }

        TPFTexInfo BuildSingleTex(string file, string def_HashString = null, ZipReader.ZipEntryFull entry = null)
        {
            TPFTexInfo tex = new TPFTexInfo(file, entry, GameDirecs);

            if (!tex.IsDef)
            {
                uint defHash = !String.IsNullOrEmpty(def_HashString) ? FindHashInString(def_HashString, "|") : 0;  // Look in def
                uint nameHash = FindHashInString(tex.FileName, "0x");  // Look in filename
                tex.Hash = defHash == 0 ? nameHash : defHash;  // Prefer defHash
                if (tex.Hash == 0)
                    DebugOutput.PrintLn($"Unable to find hash for texture: {tex.FileName}.");
            }

            return tex;
        }

        List<TPFTexInfo> LoadTPF(string tpf)
        {
            ZipReader zippy = new ZipReader(tpf);
            MaxProgress += zippy.Entries.Count;
            List<TPFTexInfo> tempTexes = new List<TPFTexInfo>();

            // Load Hashes from texmod.def (last entry)
            byte[] data = zippy.Entries.Last().Extract(true);
            char[] chars = Array.ConvertAll(data, item => (char)item);

            // Fix formatting, fix case, remove duplpicates, remove empty entries.
            List<string> Name_Hashes = new string(chars).Replace("\r", "").Replace("_0X", "_0x").Split('\n').Distinct().Where(s => s != "\0").ToList();

            foreach (var entry in zippy.Entries)
            {
                // Find and set hash
                string name_hash = Name_Hashes.Find(name => name.Contains(entry.Filename, StringComparison.OrdinalIgnoreCase));
                var tex = BuildSingleTex(entry.Filename, name_hash, entry);
                tempTexes.Add(tex);
            }

            Zippys.Add(zippy);
            return tempTexes;
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

        void AnalyseVsTree()
        {
            foreach (var tex in Textures)
            {
                if (tex.IsDef)
                    continue;

                var treeTex = CurrentTree.Textures.FirstOrDefault(tmptreeTex => tmptreeTex.Hash == tex.Hash);
                if (treeTex == null)
                    continue;  // Not found in tree

                tex.TreeFormat = treeTex.Format;
                tex.TreeMips = treeTex.Mips;
                tex.TexName = treeTex.TexName;
                tex.PCCs = treeTex.PCCs;
            }
            OnPropertyChanged(nameof(AllAnalysed));
            PreviouslyAnalysed = true;

            Status = $"Analysis Complete! {(!AllAnalysed ? $"{Textures.Where(t => !t.IsDef && !t.FoundInTree).Count()} textures were not found in tree." : "")}";
        }

        void UnAnalyse(params TPFTexInfo[] given)
        {
            List<TPFTexInfo> texes = new List<TPFTexInfo>();
            if (given.Length == 0)
                texes.AddRange(Textures);
            else
                texes.AddRange(given);

            foreach (var tex in texes)
            {
                if (!tex.FoundInTree || tex.IsDef)
                    continue;

                tex.TreeFormat = ImageEngineFormat.Unknown;
                tex.TreeMips = 0;
                tex.TexName = null;
                tex.PCCs.Clear();
                tex.Analysed = false;
            }

            OnPropertyChanged(nameof(AllAnalysed));
            PreviouslyAnalysed = false;
        }
    }
}
