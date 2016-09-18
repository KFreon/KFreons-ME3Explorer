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
using Microsoft.Win32;

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
        CommandHandler installCommand = null;
        public CommandHandler InstallCommand
        {
            get
            {
                if (installCommand == null)
                    installCommand = new CommandHandler(async param =>
                    {
                        Busy = true;
                        Status = "Installing textures...";

                        TPFTexInfo[] texes = null;
                        if (param == null)
                            texes = Textures.ToArray();
                        else if (param is IEnumerable<TPFTexInfo>)
                            texes = ((IEnumerable<TPFTexInfo>)(param)).ToArray();
                        else
                            texes = new TPFTexInfo[] { (TPFTexInfo)(param) };
                        
                        await Task.Run(async () => await ToolsetTextureEngine.InstallTextures(NumThreads, this, GameDirecs, cts, texes));

                        Progress = MaxProgress;
                        Status = "All textures installed!";
                        Busy = false;
                    });

                return installCommand;
            }
        }

        CommandHandler extractCommand = null;
        public CommandHandler ExtractCommand
        {
            get
            {
                if (extractCommand == null)
                    extractCommand = new CommandHandler(param =>
                    {
                        var tex = param as TPFTexInfo;
                        if (tex == null)
                            return;

                        SaveFileDialog sfd = new SaveFileDialog();
                        sfd.Filter = Path.GetExtension(tex.DefaultSaveName);   // TODO: All supported
                        sfd.FileName = tex.DefaultSaveName;
                        if (sfd.ShowDialog() != true)
                            return;


                        Busy = true;
                        ProgressIndeterminate = true;
                        Status = $"Extracting {tex.Name}";

                        tex.Extract(sfd.FileName);

                        Status = $"Extracted {tex.Name}!";
                        ProgressIndeterminate = false;
                        Busy = false;
                    });

                return extractCommand;
            }
        }

        CommandHandler replaceCommand = null;
        public CommandHandler ReplaceCommand
        {
            get
            {
                if (replaceCommand == null)
                    replaceCommand = new CommandHandler(async param =>
                    {
                        var tex = param as TPFTexInfo;

                        OpenFileDialog ofd = new OpenFileDialog();
                        ofd.Filter = Path.GetExtension(tex.DefaultSaveName);
                        if (ofd.ShowDialog() != true)
                            return;

                        Busy = true;
                        ProgressIndeterminate = true;
                        Status = $"Replacing {tex.Name}";

                        tex.FileName = Path.GetFileName(ofd.FileName);
                        tex.FilePath = Path.GetDirectoryName(ofd.FileName);
                        if (tex.Hash == 0)
                            tex.Hash = FindHashInString(tex.FileName, "0x");

                        await Task.Run(() => tex.GetDetails());

                        ProgressIndeterminate = false;
                        Status = $"Replaced texture with {tex.Name}";
                        Busy = false;
                    });

                return replaceCommand;
            }
        }

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
            TransformManyBlock<string, TPFTexInfo> tpfTexMaker = new TransformManyBlock<string, TPFTexInfo>(async tpf =>
            {
                Status = $"Loading textures from {Path.GetFileName(tpf)}...";
                return await LoadTPF(tpf);
            }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1, MaxDegreeOfParallelism = 1 });

            // Want disk work to be done on a single thread.
            var texExtractor = new TransformBlock<TPFTexInfo, Tuple<TPFTexInfo, byte[]>>(tex => new Tuple<TPFTexInfo, byte[]>(tex, tex.Extract()), new ExecutionDataflowBlockOptions { BoundedCapacity = 2, MaxDegreeOfParallelism = 2 });  // Disk bound 
            var thumbBuilder = new ActionBlock<Tuple<TPFTexInfo, byte[]>>(tuple =>
            {
                tuple.Item1.GetDetails(tuple.Item2);


                // Check for duplicates in loaded files.
                var dup = Textures.FirstOrDefault(t => t.FileHash == tuple.Item1.FileHash);
                if (dup == null)
                    Textures.Add(tuple.Item1);
                else
                    dup.FileDuplicates.Add(tuple.Item1);
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

        async Task<List<TPFTexInfo>> LoadTPF(string tpf)
        {
            // More than 4gb RAM available, load TPFs into memory
            bool loadIntoMemory = ToolsetInfo.AvailableRam > 4.0 * 1024 * 1024 * 1024;   
            
            ZipReader zippy = await ZipReader.LoadAsync(tpf, loadIntoMemory);
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

                var treeTexs = CurrentTree.Textures.Where(tmptreeTex => tmptreeTex.Hash == tex.Hash);
                TreeTexInfo treeTex = null;

                if (treeTexs?.Count() == 0)
                    continue; // Not found in tree

                // Look at tree duplicates
                if (treeTexs.Count() == 1)
                {
                    treeTex = treeTexs.First();
                    
                    // Create entries and link to original
                    foreach (var treet in treeTexs)
                    {
                        // Only need texname for display - can't selected in any other way, link PCCs for installation too.
                        TPFTexInfo dup = new TPFTexInfo(GameDirecs);
                        dup.TexName = treet.TexName;
                        dup.PCCs.AddRange(treet.PCCs);

                        // Get thumb
                        dup.Thumb = treet.Thumb;
                    }
                }

                tex.TreeFormat = treeTex.Format;
                tex.TreeMips = treeTex.Mips;
                tex.TexName = treeTex.TexName;
                tex.PCCs = treeTex.PCCs;
            }

            // Look for file duplicates - File could still occur here as previously it was data based, but here it's TREE HASH based (i.e. not actual hash of current data)
            var groups = Textures.GroupBy(t => t.Hash);
            foreach (var group in groups)
            {
                var first = group.First();

                foreach (var tex in group)
                {
                    if (tex == first)  // Skip first - Should be the only thing that happens as duplicates at this stage should be rare as a redheads' soul.
                        continue;

                    first.HashDuplicates.Add(tex);
                    Textures.Remove(tex);  // Remove from original list
                }
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

            // Get hash duplicates that were originally files back out.
            // All other kinds of duplicates weren't originally loaded files.
            texes.ForEach(tex =>
            {
                var dups = tex.HashDuplicates.Where(t => t.FileHash != 0);
                Textures.AddRange(dups);

                // Add to working list too
                texes.AddRange(dups);
            });

            foreach (var tex in texes)
            {
                if (!tex.FoundInTree || tex.IsDef)
                    continue;

                tex.TreeFormat = ImageEngineFormat.Unknown;
                tex.TreeMips = 0;
                tex.TexName = null;
                tex.PCCs.Clear();
                tex.Analysed = false;

                tex.HashDuplicates.Clear();
            }

            OnPropertyChanged(nameof(AllAnalysed));
            PreviouslyAnalysed = false;
        }

        void SaveToTPF(string filename, params TPFTexInfo[] textures)
        {
            List<TPFTexInfo> texes = new List<TPFTexInfo>();
            if (textures?.Count() == 0)
                texes.AddRange(Textures.Where(t => !t.IsDef && t.Hash != 0));  // Valid game textures only
            else
                texes.AddRange(textures.Where(t => !t.IsDef && t.Hash != 0));

            Busy = true;
            Progress = 0;
            MaxProgress = texes.Count;
            Status = $"Saving {MaxProgress} textures to TPF at {filename}";

            BuildTPF(filename, texes.DistinctBy(t => t.Hash));  // Don't want duplicate hashes - Won't be any if analysed, but can build TPF's without tree analysis.

            Busy = false;
            Progress = MaxProgress;
            Status = $"Saved {filename}!";
        }

        void BuildTPF(string destination, IEnumerable<TPFTexInfo> texes)
        {
            List<Tuple<string, Func<byte[]>>> texInfos = new List<Tuple<string, Func<byte[]>>>();  // Func<byte[]> delays data fetch until required, so don't have huge data in memory at once.
            foreach (var tex in texes)
                texInfos.Add(new Tuple<string, Func<byte[]>>(tex.DefaultSaveName, tex.Extract));

            // Build log
            Func<byte[]> logData = () =>
            {
                List<byte> data = new List<byte>();

                foreach(var tex in texes)
                {
                    string hash = ToolsetTextureEngine.FormatTexmodHashAsString(tex.Hash);
                    data.AddRange(Encoding.ASCII.GetBytes($"{hash}|{tex.DefaultSaveName}{Environment.NewLine}"));
                }

                return data.ToArray();
            };
            texInfos.Add(new Tuple<string, Func<byte[]>>("texmod.def", logData));

            ZipWriter.Repack(destination, texInfos);
        }
    }
}
