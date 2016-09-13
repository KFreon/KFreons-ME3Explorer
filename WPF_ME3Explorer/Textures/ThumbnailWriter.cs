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
        public static bool IsWriting { get; set; } = false;

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

        public Thumbnail Add(MemoryStream thumb, long offset)
        {
            Thumbnail newThumb = null;
            lock (locker)
            {
                var currentPosition = writer.Position;
                writer.Seek(offset, SeekOrigin.Begin);

                newThumb = Add(thumb);

                // Seek back to original Position
                writer.Seek(currentPosition, SeekOrigin.Begin);
            }

            return newThumb;
        }

        public void BeginAdding()
        {
            lock (locker)
            {
                Directory.CreateDirectory(CachePath.GetDirParent());
                writer = new FileStream(CachePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                IsWriting = true;
            }
        }

        public void FinishAdding()
        {
            lock (locker)
                writer.Dispose();

            IsWriting = false;
        }

        public void Dispose()
        {
            FinishAdding();
        }

        internal Thumbnail ReplaceOrAdd(MemoryStream stream, Thumbnail old)
        {
            // Decide if new thumb can fit where old thumb was
            if (stream.Length <= old.Length)
                return Add(stream, old.Offset);
            else
                return Add(stream);  // Append otherwise
        }
    }
}
