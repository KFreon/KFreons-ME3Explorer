﻿using System;
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
using Microsoft.WindowsAPICodePack.Dialogs;
using WPF_ME3Explorer.PCCObjectsAndBits;

namespace WPF_ME3Explorer.UI.ViewModels
{
    public class TPFToolsViewModel : MEViewModelBase<TPFTexInfo>
    {
        List<ZipReader> Zippys = new List<ZipReader>();

        #region Properties
        public List<string> AcceptedImageExtensions = null;
        public List<string> AcceptedImageDescriptions = null;

        #region TPFSave Properties
        bool isTPFBuilding = false;
        public bool IsTPFBuilding
        {
            get
            {
                return isTPFBuilding;
            }
            set
            {
                SetProperty(ref isTPFBuilding, value);
            }
        }

        string tpfSave_SavePath = null;
        public string TPFSave_SavePath
        {
            get
            {
                return tpfSave_SavePath;
            }
            set
            {
                SetProperty(ref tpfSave_SavePath, value);
            }
        }

        int tpfSave_TexCount = 0;
        public int TPFSave_TexCount
        {
            get
            {
                return tpfSave_TexCount;
            }
            set
            {
                SetProperty(ref tpfSave_TexCount, value);
            }
        }

        string tpfSave_Author = null;
        public string TPFSave_Author
        {
            get
            {
                return tpfSave_Author;
            }
            set
            {
                SetProperty(ref tpfSave_Author, value);
            }
        }

        string tpfSave_Comment = null;
        public string TPFSave_Comment
        {
            get
            {
                return tpfSave_Comment;
            }
            set
            {
                SetProperty(ref tpfSave_Comment, value);
            }
        }
        #endregion TPFSave Properties



        public bool SaveTPFEnabled
        {
            get
            {
                return Textures?.Any(t => t.Hash != 0) == true;
            }
        }

        public bool AllAnalysed
        {
            get
            {
                return Textures.Count != 0 && Textures.All(tex => !tex.IsDef && tex.FoundInTree || tex.IsDef);
            }
        }

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

        public override string TextureSearch
        {
            get
            {
                return base.TextureSearch;
            }

            set
            {
                base.TextureSearch = value;
                Search(value);
                MainDisplayView.Refresh();
            }
        }

        public ICollectionView MainDisplayView { get; set; }
        #endregion Properties

        public bool? TexturesCheckAll
        {
            get
            {
                if (Textures == null)
                    return false;

                int num = Textures.Where(tex => !tex.IsDef && tex.IsChecked).Count();
                if (num == 0)
                    return false;
                else if (num == Textures.Where(tex => !tex.IsDef).Count())
                    return true;

                return null;
            }
            set
            {
                Textures.Where(tex => !tex.IsDef).AsParallel().ForAll(tex => tex.IsChecked = value == true);
            }
        }

        #region Commands
        #region SaveTPF Commands
        CommandHandler saveToTPFCommand = null;
        public CommandHandler SaveToTPFCommand
        {
            get
            {
                if (saveToTPFCommand == null)
                    saveToTPFCommand = new CommandHandler(() =>
                    {
                        IsTPFBuilding = true;

                        // Populate save properties
                        TPFSave_TexCount = Textures.Where(tex => tex.IsChecked).Count();
                        TPFSave_Author = "Commander Shepard's Hair-do";
                        TPFSave_Comment = ""; // Can't be null

                        TPFSave_SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Saved TPF.tpf");
                    });

                return saveToTPFCommand;
            }
        }

        CommandHandler tpfSave_CancelCommand = null;
        public CommandHandler TPFSave_CancelCommand
        {
            get
            {
                if (tpfSave_CancelCommand == null)
                    tpfSave_CancelCommand = new CommandHandler(() =>
                    {
                        IsTPFBuilding = false;
                        Status = "TPF Build cancelled!";
                    });

                return tpfSave_CancelCommand;
            }
        }

        internal async Task BulkExtract(string folderName)
        {
            Busy = true;
            Progress = 0;

            // Get TPFs
            var files = Directory.EnumerateFiles(folderName);
            var tpfs = files.Where(file => file.EndsWith("tpf", StringComparison.OrdinalIgnoreCase) || file.EndsWith("metpf", StringComparison.OrdinalIgnoreCase)).ToList();

            MaxProgress = tpfs.Count;

            foreach (var tpf in tpfs)
            {
                Status = $"Extracting: {Path.GetFileName(tpf)}";

                var zippy = await ZipReader.LoadAsync(tpf, true);

                // Create output directory
                string extractPath = Path.Combine(folderName, Path.GetFileNameWithoutExtension(tpf));
                Directory.CreateDirectory(extractPath);

                // Get hashes
                List<string> Hashes_Names = ToolsetTextureEngine.GetHashesAndNamesFromTPF(zippy);

                // Ensure hashes in filenames and extract
                await Task.Run(() =>
                {
                    for (int i = 0; i < zippy.Entries.Count; i++)
                    {
                        var entry = zippy.Entries[i];
                        var hashname = Hashes_Names[i];
                        var sepInd = hashname.IndexOf('|');
                        var hash = hashname.Substring(0, sepInd);
                        string filename = ToolsetTextureEngine.EnsureHashInFilename(entry.Filename, hash);

                        string savePath = Path.Combine(extractPath, filename);
                        if (File.Exists(savePath))
                            continue;

                        entry.Extract(false, savePath);
                    }
                });
                
                Progress++;
            }


            Busy = false;
            Status = "All TPFs extracted!";
            Progress = MaxProgress;
        }

        CommandHandler tpfSave_SaveCommand = null;
        public CommandHandler TPFSave_SaveCommand
        {
            get
            {
                if (tpfSave_SaveCommand == null)
                    tpfSave_SaveCommand = new CommandHandler(async () =>
                    {
                        await Task.Run(() => SaveToTPF(TPFSave_SavePath, Textures.Where(tex => tex.IsChecked).ToArray()));
                        IsTPFBuilding = false;
                    });

                return tpfSave_SaveCommand;
            }
        }
        #endregion SaveTPF Commands

        static readonly object Install_CachedLocker = new object();

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
                            texes = Textures.Where(tex => tex.IsChecked).ToArray();
                        else if (param is IEnumerable<TPFTexInfo>)
                            texes = ((IEnumerable<TPFTexInfo>)(param)).ToArray();
                        else
                            texes = new TPFTexInfo[] { (TPFTexInfo)(param) };



                        List<Texture2D> cachedTex2Ds = new List<Texture2D>();

                        // loop over PCCs, creating and caching textures.
                        var pccTexGroups =
                                from tex in texes
                                from pcc in tex.PCCs
                                where pcc.IsChecked
                                group tex by pcc.Name;

                        var action = new Action<IGrouping<string, TPFTexInfo>>(texGroup =>
                        {
                            if (cts.IsCancellationRequested)
                                return;

                            string pcc = texGroup.Key;
                            PCCObject pccobj = new PCCObject(pcc, GameVersion);

                            foreach (var tex in texGroup)
                            {
                                // Get texture.
                                Texture2D tex2D = null;
                                lock (Install_CachedLocker)
                                {
                                    tex2D = cachedTex2Ds.FirstOrDefault(t => t.texName == tex.TexName && t.Hash == tex.Hash);

                                    // Need to create texture.
                                    if (tex2D == null)
                                    {
                                        tex2D = new Texture2D(pccobj, tex.PCCs.First(t => t.Name == pcc).ExpID, GameDirecs);
                                        cachedTex2Ds.Add(tex2D);

                                        using (ImageEngineImage img = new ImageEngineImage(tex.Extract()))
                                            tex2D.OneImageToRuleThemAll(img);
                                    }
                                }

                                
                                // Install things
                                foreach (var entry in tex.PCCs.Where(t => t.Name == pcc))
                                    ToolsetTextureEngine.SaveTex2DToPCC(pccobj, tex2D, GameDirecs, entry.ExpID);
                            }

                            // Save PCC
                            pccobj.SaveToFile(pcc);
                        });

                        if (ImageEngine.EnableThreading)
                            await Task.Run(() => Parallel.ForEach(pccTexGroups, new ParallelOptions { MaxDegreeOfParallelism = ImageEngine.NumThreads }, action));
                        else
                            foreach (var texGroup in pccTexGroups)
                                action(texGroup);

                        Progress = MaxProgress;
                        Status = "All textures installed!";
                        Busy = false;
                    });

                return installCommand;
            }
        }

        CommandHandler extractCheckedCommand = null;
        public CommandHandler ExtractCheckedCommand
        {
            get
            {
                if (extractCheckedCommand == null)
                    extractCheckedCommand = new CommandHandler(() =>
                    {
                        CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                        dialog.Title = "Select destination for extracted textures";
                        dialog.IsFolderPicker = true;
                        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            Busy = true;
                            Status = "Extracting textures...";
                            var texes = Textures.Where(tex => tex.IsChecked).ToList();
                            MaxProgress = texes.Count;
                            Progress = 0;

                            Task.Run(() =>
                            {
                                foreach (var item in texes)
                                {
                                    item.Extract(Path.Combine(dialog.FileName, item.DefaultSaveName));
                                    Progress++;
                                }

                                Busy = false;
                                Status = "Textures extracted!";
                                Progress = MaxProgress;
                            });
                        }
                    });

                return extractCheckedCommand;
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
                        sfd.Title = "Select destination for extracted texture";
                        sfd.Filter = "Files|*" + Path.GetExtension(tex.DefaultSaveName);   // TODO: All supported
                        sfd.FileName = tex.DefaultSaveName;
                        if (sfd.ShowDialog() != true)
                            return;


                        Busy = true;
                        ProgressIndeterminate = true;
                        Status = $"Extracting {tex.Name}";

                        string savePath = ToolsetTextureEngine.EnsureHashInFilename(sfd.FileName, tex.Hash);
                        tex.Extract(savePath);

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
                        ofd.Title = "Select image to replace";
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

                        await Task.Run(async () => await tex.GetDetails());

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
                    clearAllCommand = new CommandHandler(() =>
                    {
                        Textures.Clear();
                        OnPropertyChanged(nameof(SaveTPFEnabled));
                        Status = "Ready";
                    });

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

            AcceptedImageDescriptions = ToolsetTextureEngine.AcceptedImageDescriptions;
            AcceptedImageExtensions = ToolsetTextureEngine.AcceptedImageExtensions;

            AcceptedImageExtensions.Add(".tpf");
            AcceptedImageExtensions.Add(".metpf");

            AcceptedImageDescriptions.Add("Texmod Package");
            AcceptedImageDescriptions.Add("Old Toolset Texture Package");

            MainDisplayView = CollectionViewSource.GetDefaultView(Textures);
            MainDisplayView.Filter = item => !((TPFTexInfo)item).IsHidden;

            GameDirecs.GameVersion = Properties.Settings.Default.TPFToolsGameVersion;
            OnPropertyChanged(nameof(GameVersion));

            TPFTexInfo.InstallCommand = installCommand;
            TPFTexInfo.ReplaceCommand = ReplaceCommand;
            TPFTexInfo.ExtractCommand = ExtractCommand;

            SetupCurrentTree();
        }

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
            if (Analysed)
            {
                UnAnalyse();
                AnalyseVsTree();
            }

            Properties.Settings.Default.TPFToolsGameVersion = game;
            Properties.Settings.Default.Save();
        }

        internal async Task LoadFiles(string[] fileNames)
        {
            Busy = true;
            Progress = 0;

            // Deal with folders, if present. (Thanks jokoho48 - https://github.com/ME3Explorer/ME3Explorer/pull/466)
            List<string> tempFileNames = new List<string>();
            foreach (string path in fileNames)
            {
                if (path.isDirectory())
                    tempFileNames.AddRange(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
                else
                    tempFileNames.Add(path);
            }

            MaxProgress = tempFileNames.Count;
            int prevTexCount = Textures.Count;

            int maxParallelism = NumThreads;

            // Get Details method
            var PopulateTexDetails = new Action<TPFTexInfo, byte[]>(async (tex, data) =>
            {
                await tex.GetDetails(data);

                // Wire up HashChanged to SaveTPFEnabled
                tex.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(tex.HashChanged))
                        OnPropertyChanged(nameof(SaveTPFEnabled));
                    else if (args.PropertyName == nameof(tex.IsChecked))
                        OnPropertyChanged(nameof(TexturesCheckAll));
                };


                // Check for duplicates in loaded files.
                lock (Textures)
                {
                    var dup = Textures.FirstOrDefault(t => t.FileHash == tex.FileHash);
                    if (dup == null)
                        Textures.Add(tex);
                    else
                        dup.FileDuplicates.Add(tex);
                }
                
                Progress++;
            });

            /// Setup pipeline
            BufferBlock<string> fileBuffer = new BufferBlock<string>();
            TransformBlock<string, TPFTexInfo> singleTexMaker = new TransformBlock<string, TPFTexInfo>(file =>
            {
                Status = $"Loading {Path.GetFileName(file)}...";
                return BuildSingleTex(file);
            });


            // TPF side - tpf's can be stored in memory, and be quite large so need to dispose ASAP, meaning we can't wait to dispose until all work is done. Need to do TPF's one by one, disposing once done with them.
            var tpfMaker = new ActionBlock<string>(async tpf =>
            {
                Status = $"Loading textures from {Path.GetFileName(tpf)}...";
                var start = Environment.TickCount;
                List<TPFTexInfo> texes = await LoadTPF(tpf);
                var afterloading = Environment.TickCount;
                ZipReader zippy = Zippys.Last();

                // Extract and get details
                await Task.Run(() =>
                {
                    TPFTexInfo def = null;
                    Parallel.ForEach(texes, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism - 1 }, tex =>
                    {
                        if (tex.IsDef)
                            def = tex;
                        else
                        {
                            var data = tex.Extract();
                            PopulateTexDetails(tex, data);
                        }
                    });

                    Textures.Add(def);
                });
                var end = Environment.TickCount;
                Console.WriteLine($"Loading: {TimeSpan.FromMilliseconds(afterloading - start)}, texturing: {TimeSpan.FromMilliseconds(end - afterloading)}");

                // Dipose of datastream if necessary, and indicate further operations should use the disk.
                zippy.FileData = null;
            }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1, MaxDegreeOfParallelism = 1 });


            // Single texture side
            var texExtractor = new TransformBlock<TPFTexInfo, Tuple<TPFTexInfo, byte[]>>(tex => new Tuple<TPFTexInfo, byte[]>(tex, tex.Extract()), new ExecutionDataflowBlockOptions { BoundedCapacity = maxParallelism, MaxDegreeOfParallelism = maxParallelism }); 
            var thumbBuilder = new ActionBlock<Tuple<TPFTexInfo, byte[]>>(tuple => PopulateTexDetails(tuple.Item1, tuple.Item2), new ExecutionDataflowBlockOptions { BoundedCapacity = maxParallelism, MaxDegreeOfParallelism = maxParallelism });

            // Connect pipeline
            fileBuffer.LinkTo(tpfMaker, new DataflowLinkOptions { PropagateCompletion = true }, file => AcceptedImageExtensions.Where(ext => ext.Contains("tpf")).Contains(Path.GetExtension(file), StringComparison.InvariantCultureIgnoreCase));

            fileBuffer.LinkTo(singleTexMaker, new DataflowLinkOptions { PropagateCompletion = true }, file => AcceptedImageExtensions.Where(ext => !ext.Contains("tpf")).Contains(Path.GetExtension(file), StringComparison.InvariantCultureIgnoreCase));
            singleTexMaker.LinkTo(texExtractor, new DataflowLinkOptions { PropagateCompletion = true });
            texExtractor.LinkTo(thumbBuilder, new DataflowLinkOptions { PropagateCompletion = true });

            // Start pipeline
            foreach (var file in tempFileNames)
                fileBuffer.Post(file);

            fileBuffer.Complete();

            // Due to pipeline topology one side can and will finish first, which then Completes everything else despite the other side still working.
            await Task.WhenAll(tpfMaker.Completion, singleTexMaker.Completion);  // Wait for input blocks to complete, since they're separate.
            await thumbBuilder.Completion;   // Now wait for the single exit block.

            OnPropertyChanged(nameof(SaveTPFEnabled));
            OnPropertyChanged(nameof(TexturesCheckAll));

            Status = $"Loaded {Textures.Count - prevTexCount} from {tempFileNames.Count} files.";
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
            List<string> Name_Hashes = ToolsetTextureEngine.GetHashesAndNamesFromTPF(zippy);

            foreach (var entry in zippy.Entries)
            {
                // Find and set hash
                string name_hash = Name_Hashes.Find(name => name.Contains(entry.Filename, StringComparison.OrdinalIgnoreCase));
                var tex = BuildSingleTex(entry.Filename, name_hash, entry);
                tempTexes.Add(tex);
            }

            
            /*Console.WriteLine($"CD Offset: {zippy.EOFStrct.CDOffset}");
            Console.WriteLine($"CD Size: {zippy.EOFStrct.CDSize}");*/

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
                {
                    tex.Analysed = true;
                    continue;
                }

                var treeTexs = CurrentTree.Textures.Where(tmptreeTex => tmptreeTex.Hash == tex.Hash).ToList();

                if (treeTexs?.Count == 0)
                {
                    tex.Analysed = true;
                    tex.Error = "Not Found in Tree";
                    continue;
                }


                // Look at tree duplicates
                if (treeTexs.Count > 1)
                {
                    // Create entries and link to original
                    foreach (var treet in treeTexs)
                    {
                        // Only need texname for display - can't selected in any other way, link PCCs for installation too.
                        TPFTexInfo dup = new TPFTexInfo(GameDirecs)
                        {
                            TexName = treet.TexName,
                            
                            // Get thumb
                            Thumb = treet.Thumb
                        };

                        dup.PCCs.AddRange(treet.PCCs);
                    }
                }

                var treeTex = treeTexs[0];
                tex.TreeFormat = treeTex.Format;
                tex.TreeMips = treeTex.Mips;
                tex.TexName = treeTex.TexName;
                tex.PCCs.Clear();
                tex.PCCs.AddRange(treeTex.PCCs);

                // Get some details about texture's current state in game
                using (PCCObject pcc = new PCCObject(tex.PCCs[0].Name, GameVersion))
                {
                    using (Texture2D tex2D = new Texture2D(pcc, tex.PCCs[0].ExpID, GameDirecs))
                    {
                        tex.InGameWidth = tex2D.ImageList[0].ImageSize.Width;
                        tex.InGameHeight = tex2D.ImageList[0].ImageSize.Height;
                    }
                }

                tex.Analysed = true;
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
            SelectedTexture = SelectedTexture;
            Analysed = true;

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
            var temp = new List<TPFTexInfo>(texes);
            foreach (var tex in temp)
            {
                var dups = tex.HashDuplicates.Where(t => t.FileHash != 0);
                Textures.AddRange(dups);

                // Add to working list too
                texes.AddRange(dups);
            }

            foreach (var tex in texes)
            {
                if (tex.IsDef)  // Defs don't need any settings since they don't take part in any of these operations.
                    continue;

                tex.TreeFormat = ImageEngineFormat.Unknown;
                tex.TreeMips = 0;
                tex.TexName = null;
                tex.PCCs.Clear();
                tex.Analysed = false;
                tex.Error = null;

                tex.HashDuplicates.Clear();
            }

            OnPropertyChanged(nameof(AllAnalysed));
            Analysed = false;
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
                data.Add((byte)'\0');
                return data.ToArray();
            };
            texInfos.Add(new Tuple<string, Func<byte[]>>("texmod.def", logData));

            ZipWriter.Repack(destination, texInfos, TPFSave_Author, TPFSave_Comment);
        }
    }
}
