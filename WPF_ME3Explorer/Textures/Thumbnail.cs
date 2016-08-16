using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CSharpImageLibrary;

namespace WPF_ME3Explorer.Textures
{
    public class Thumbnail
    {
        public long Offset { get; set; }
        public int Length { get; set; }
        string cachePath = null;

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
            if (!File.Exists(cachePath))
                return null;

            byte[] data = new byte[Length];
            using (FileStream fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read))  // Allow simultaneous reads
            {
                fs.Seek(Offset, SeekOrigin.Begin);
                fs.Read(data, 0, Length);
            }

            return UsefulThings.WPF.Images.CreateWPFBitmap(data);
        }
    }
}
