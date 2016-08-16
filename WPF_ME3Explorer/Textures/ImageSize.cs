using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer.Textures
{
    public class ImageSize : IComparable
    {
        public readonly uint Width;
        public readonly uint Height;

        public ImageSize(uint width, uint height)
        {
            if (!UsefulThings.General.IsPowerOfTwo(width))
                new FormatException("Invalid width value, must be power of 2");
            if (!UsefulThings.General.IsPowerOfTwo(width))
                new FormatException("Invalid height value, must be power of 2");
            if (width == 0)
                width = 1;
            if (height == 0)
                height = 1;
            this.Width = width;
            this.Height = height;
        }

        public int CompareTo(object obj)
        {
            if (obj is ImageSize)
            {
                ImageSize temp = (ImageSize)obj;
                if ((temp.Width * temp.Height) == (this.Width * this.Height))
                    return 0;
                if ((temp.Width * temp.Height) > (this.Width * this.Height))
                    return -1;
                else
                    return 1;
            }
            throw new ArgumentException();
        }

        public override string ToString()
        {
            return this.Width + "x" + this.Height;
        }

        public override bool Equals(System.Object obj)
        {
            // If parameter is null return false.
            if (obj == null)
                return false;

            // If parameter cannot be cast to Point return false.
            ImageSize p = obj as ImageSize;
            if ((System.Object)p == null)
                return false;

            // Return true if the fields match:
            return (this.Width == p.Width) && (this.Height == p.Height);
        }

        public bool Equals(ImageSize p)
        {
            // If parameter is null return false:
            if (p == null)
                return false;

            // Return true if the fields match:
            return (this.Width == p.Width) && (this.Height == p.Height);
        }

        public override int GetHashCode()
        {
            return (int)(Width ^ Height);
        }

        public static bool operator ==(ImageSize a, ImageSize b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
                return true;

            // If one is null, but not both, return false.
            if (a == null || b == null)
                return false;

            // Return true if the fields match:
            return a.Width == b.Width && a.Height == b.Height;
        }

        public static bool operator !=(ImageSize a, ImageSize b)
        {
            return !(a == b);
        }

        public static ImageSize operator /(ImageSize a, int b)
        {
            return new ImageSize((uint)(a.Width / b), (uint)(a.Height / b));
        }

        public static ImageSize operator *(ImageSize a, int b)
        {
            return new ImageSize((uint)(a.Width * b), (uint)(a.Height * b));
        }

        public static ImageSize stringToSize(string input)
        {
            string[] parsed = input.Split('x');
            if (parsed.Length != 2)
                throw new FormatException();
            uint width = Convert.ToUInt32(parsed[0]);
            uint height = Convert.ToUInt32(parsed[1]);
            return new ImageSize(width, height);
        }
    }
}
