using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmaroK86.ImageFormat;

namespace WPF_ME3Explorer.Textures
{
    public class ImageInfo
    {
        #region Properties
        public ImageSize ImgSize
        {
            get; set;
        }

        public int Offset { get; set; }

        public int GameVersion { get; set; }

        PCCStorageType stor = PCCStorageType.empty;
        public PCCStorageType storageType
        {
            get
            {
                return stor;
            }
            set
            {
                if (GameVersion == 3 && (int)value == 3)
                    stor = PCCStorageType.arcCpr;
                else
                    stor = value;
            }
        }

        public int UncSize { get; set; }

        public int CprSize { get; set; }
        #endregion Properties

        public ImageInfo()
        {
        }

        public ImageInfo(ImageSize size, int offset, int gameversion, PCCStorageType storage, int uncsize, int cprsize)
        {
            ImgSize = size;
            Offset = offset;
            GameVersion = gameversion;
            storageType = storage;
            UncSize = uncsize;
            CprSize = cprsize;
        }
    }
}
