using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CSharpImageLibrary;
using UsefulThings;

namespace WPF_ME3Explorer.Textures
{
    public class Thumbnail
    {
        public long Offset { get; set; }
        public int Length { get; set; }
        string cachePath = null;
        internal MemoryStream ChangedThumb = null;  // Used when texture is changed.

        public Thumbnail() { }  

        public Thumbnail(string cache)
        {
            cachePath = cache;
        }

        public Thumbnail(byte[] data, string cache) : this(cache)
        {

        }

        public Thumbnail(ImageEngineImage img, string cache) : this(cache)
        {

        }

        public Thumbnail(int offset, int length, string cache) : this(cache)
        {
            Offset = offset;
            Length = length;
        }

        public BitmapSource GetImage()
        {
            MemoryStream ms = new MemoryStream();
            if (ChangedThumb == null)
            {
                if (!File.Exists(cachePath))
                    return null;

                using (FileStream fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read))  // Allow simultaneous reads
                {
                    fs.Seek(Offset, SeekOrigin.Begin);
                    ms.ReadFrom(fs, Length);
                }
            }
            else
                ms = ChangedThumb;

            return UsefulThings.WPF.Images.CreateWPFBitmap(ms, DisposeStream: ChangedThumb == null);  // Dispose of stream only if it's not the ChangedThumb being used
        }
    }
}
