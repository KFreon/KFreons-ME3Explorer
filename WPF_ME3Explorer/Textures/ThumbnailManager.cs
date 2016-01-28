using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using UsefulThings;
using CSharpImageLibrary;
using WPF_ME3Explorer.Debugging;
using CSharpImageLibrary.General;
using Gibbed.IO;

namespace WPF_ME3Explorer.Textures
{
    public class ThumbnailManager : IDisposable
    {
        public class ThumbnailEntry
        {
            public long Offset = 0;
            public int Length = 0;
            public string Name = null;

            public ThumbnailEntry(string name, long offset, int length)
            {
                Name = name;
                Offset = offset;
                Length = length;
            }

            public ThumbnailEntry(string name, string offset, string length)
            {
                Name = name;

                if (!long.TryParse(offset, out Offset))
                    throw new ArgumentException($"Offset wasn't a number. Got: {offset}");

                if (!int.TryParse(length, out Length))
                    throw new ArgumentException($"Length wasn't a number. Got: {length}");
            }

            public BitmapImage GetThumbnail(FileStream stream)
            {
                stream.Seek(Offset, SeekOrigin.Begin);
                byte[] imgData = stream.ReadBytes(Length);
                return UsefulThings.WPF.Images.CreateWPFBitmap(imgData);
            }
        }

        public List<ThumbnailEntry> Entries { get; set; }
        FileStream ThumbStream { get; set; }
        static object Locker = new object();

        MEDirectories.MEDirectories MEExDirecs = null;

        public string ArchivePath
        {
            get
            {
                return Path.Combine(MEExDirecs.ExecFolder, "ThumbnailCaches\\", "ME" + MEExDirecs.GameVersion.ToString() + "ThumbnailCache.KFCache");
            }
        }

        public string ArchiveIndex
        {
            get
            {
                return ArchivePath + ".index";
            }
        }

        public ThumbnailManager(MEDirectories.MEDirectories meex)
        {
            Entries = new List<ThumbnailEntry>();
            MEExDirecs = meex;
            Setup();
        }

        private void Open()
        {
            try
            {
                if (ThumbStream == null)
                    ThumbStream = new FileStream(ArchivePath, FileMode.OpenOrCreate);

            }
            catch (Exception e)
            {
            }

            ParseEntries();
        }

        public bool Setup()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ArchivePath));
            }
            catch (Exception e)
            {
                return false;
            }

            Open();
            return true;
        }

        private bool ParseEntries()
        {
            if (!File.Exists(ArchiveIndex))
            {
                var stream = File.Create(ArchiveIndex);
                stream.Dispose();
                return true;
            }

            if (Entries != null && Entries.Count > 0)
                return true;


            string[] lines = File.ReadAllLines(ArchiveIndex);

            try
            {
                foreach (var line in lines)
                {
                    string[] parts = line.Split('|');
                    Entries.Add(new ThumbnailEntry(parts[0], parts[1], parts[2]));
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        public void Add(string thumbname, byte[] imgData)
        {
            lock (Locker)
            {
                if (Contains(thumbname))
                    return;

                long offset = ThumbStream.Position;
                ThumbStream.Write(imgData, 0, imgData.Length);
                Entries.Add(new ThumbnailEntry(thumbname, offset, imgData.Length));
            }
        }

        public void Add(string thumbname, Stream stream)
        {
            lock (Locker)
            {
                long offset = ThumbStream.Position;
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(ThumbStream);
                Entries.Add(new ThumbnailEntry(thumbname, offset, (int)stream.Length));
            }
        }

        public bool Contains(string thumbname)
        {
            lock (Locker)
            {
                return Entries.Any(t => t.Name == thumbname);
            }
        }

        public BitmapImage GetThumbnail(string thumbname)
        {
            lock (Locker)
            {
                var entry = Entries.FirstOrDefault(t => t.Name == thumbname);
                if (entry == null)
                    return null;

                return entry.GetThumbnail(ThumbStream);
            }
        }

        public void Clear()
        {
            lock (Locker)
            {
                Entries.Clear();
                if (ThumbStream != null)
                    ThumbStream.Dispose();

                ThumbStream = null;

                if (File.Exists(ArchiveIndex))
                    File.Delete(ArchiveIndex);

                if (File.Exists(ArchivePath))
                    File.Delete(ArchivePath);
            }
        }

        public void Close()
        {
            lock (Locker)
            {
                using (StreamWriter sw = new StreamWriter(ArchiveIndex))
                {
                    foreach (var entry in Entries)
                    {
                        string line = entry.Name + '|' + entry.Offset + '|' + entry.Length;
                        sw.WriteLine(line);
                    }
                }
                ThumbStream.Dispose();
                ThumbStream = null;
            }
        }

        public bool GenerateThumbnail(byte[] imageData, string thumbname)
        {
            int size = 128;

            // KFreon: If entry exists already, ignore it.
            if (Contains(thumbname))
                return true;

            DebugOutput.PrintLn("Generating Thumbnail: " + thumbname);

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    ImageEngine.GenerateThumbnailToStream(stream, size);
                    Add(thumbname, stream);
                }
                return true;
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Failed to generate thumbnail: {0}", "ThumbnailManager", e, thumbname);
                return false;
            }

        }



        public void Dispose()
        {
            if (ThumbStream != null)
                ThumbStream.Dispose();

            ThumbStream = null;
        }
    }
}
