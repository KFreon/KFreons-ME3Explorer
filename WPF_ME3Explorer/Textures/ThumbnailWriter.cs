using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings;

namespace WPF_ME3Explorer.Textures
{
    public class ThumbnailWriter : IDisposable
    {
        public static string CachePath { get; private set; }
        public MEDirectories.MEDirectories GameDirecs = null;
        FileStream writer = null;
        static readonly object locker = new object();

        public ThumbnailWriter(MEDirectories.MEDirectories gameDirec)
        {
            GameDirecs = gameDirec;
            CachePath = GameDirecs.ThumbnailCachePath;
        }

        public Thumbnail Add(MemoryStream thumb)
        {
            Thumbnail thumbnail = new Thumbnail(GameDirecs.ThumbnailCachePath);
            lock (locker)
            {
                thumbnail.Offset = (int)writer.Position;
                thumb.WriteTo(writer);
                thumbnail.Length = (int)thumb.Length;
            }

            return thumbnail;
        }

        public void BeginAdding()
        {
            lock (locker)
            {
                Directory.CreateDirectory(CachePath.GetDirParent());
                writer = new FileStream(CachePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            }
        }

        public void FinishAdding()
        {
            lock (locker)
                writer.Dispose();
        }

        public void Dispose()
        {
            FinishAdding();
        }
    }
}
