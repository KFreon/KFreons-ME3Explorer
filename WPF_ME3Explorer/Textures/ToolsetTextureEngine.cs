using CSharpImageLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPF_ME3Explorer.Debugging;
using WPF_ME3Explorer.PCCObjectsAndBits;

namespace WPF_ME3Explorer.Textures
{
    public static class ToolsetTextureEngine
    {
        /// <summary>
        /// Returns hash as a string in the 0xhash format.
        /// </summary>
        /// <param name="hash">Hash as a uint.</param>
        /// <returns>Hash as a string.</returns>
        public static string FormatTexmodHashAsString(uint hash)
        {
            return "0x" + System.Convert.ToString(hash, 16).PadLeft(8, '0').ToUpper();
        }

        /// <summary>
        /// Returns a uint of a hash in string format. 
        /// </summary>
        /// <param name="line">String containing hash in texmod log format of name|0xhash.</param>
        /// <returns>Hash as a uint.</returns>
        public static uint FormatTexmodHashAsUint(string line)
        {
            uint hash = 0;
            uint.TryParse(line.Split('|')[0].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier, null, out hash);
            return hash;
        }

        public static MemoryStream OverlayAndPickDetailed(MemoryStream sourceStream)
        {
            // testing 
            return sourceStream;



            BitmapSource source = UsefulThings.WPF.Images.CreateWPFBitmap(sourceStream);
            WriteableBitmap dest = new WriteableBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, System.Windows.Media.PixelFormats.Bgra32, source.Palette);

            // KFreon: Write onto black
            var overlayed = Overlay(dest, source);


            // KFreon: Choose the most detailed image between one with alpha merged and one without.
            JpegBitmapEncoder enc = new JpegBitmapEncoder();
            enc.QualityLevel = 90;
            enc.Frames.Add(BitmapFrame.Create(overlayed));

            MemoryStream mstest = new MemoryStream();
            enc.Save(mstest);

            MemoryStream jpg = new MemoryStream();
            using (ImageEngineImage img = new ImageEngineImage(sourceStream))
                img.Save(jpg, ImageEngineFormat.JPG, MipHandling.KeepTopOnly);

            if (jpg.Length > mstest.Length)
            {
                mstest.Dispose();
                return jpg;
            }
            else
            {
                jpg.Dispose();
                return mstest;
            }
        }

        /// <summary>
        /// Overlays one image on top of another.
        /// Both images MUST be the same size.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="overlay"></param>
        /// <returns></returns>
        static BitmapSource Overlay(BitmapSource source, BitmapSource overlay)
        {
            if (source.PixelWidth != overlay.PixelWidth || source.PixelHeight != overlay.PixelHeight)
                throw new InvalidDataException("Source and overlay must be the same dimensions.");

            var drawing = new DrawingVisual();
            var context = drawing.RenderOpen();
            context.DrawImage(source, new System.Windows.Rect(0, 0, source.PixelWidth, source.PixelHeight));
            context.DrawImage(overlay, new System.Windows.Rect(0, 0, overlay.PixelWidth, overlay.PixelHeight));

            context.Close();
            var overlayed = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
            overlayed.Render(drawing);

            return overlayed;
        }


        internal static bool ChangeTexture(TreeTexInfo tex, string newTextureFileName)
        {
            // Get Texture2D
            Texture2D tex2D = GetTexture2D(tex);

            byte[] imgData = File.ReadAllBytes(newTextureFileName);

            // Update Texture2D
            using (ImageEngineImage img = new ImageEngineImage(imgData))
                tex2D.OneImageToRuleThemAll(img);

            // Ensure tex2D is part of the TreeTexInfo for later use.
            tex.AssociatedTexture = tex2D;
            tex.HasChanged = true;

            return true;
        }

        static Texture2D GetTexture2D(TreeTexInfo tex)
        {
            if (tex.PCCS?.Count < 1)
                throw new IndexOutOfRangeException($"Tex: {tex.TexName} has no PCC's.");

            if (tex.GameVersion < 1 || tex.GameVersion > 3)
                throw new IndexOutOfRangeException($"Tex: {tex.TexName}'s game version is out of range. Value: {tex.GameVersion}.");

            // Read new texture file
            Texture2D tex2D = null;
            PCCObject pcc = null;

            string pccPath = tex.PCCS[0].Name;
            int expID = tex.PCCS[0].ExpID;

            // Texture object has already been created - likely due to texture being updated previously in current session
            if (tex.AssociatedTexture == null)
                tex2D = tex.AssociatedTexture;
            else
            {
                // Create PCCObject
                if (!File.Exists(pccPath))
                    throw new FileNotFoundException($"PCC not found at: {pccPath}.");

                pcc = new PCCObject(tex.PCCS[0].Name, tex.GameVersion);

                if (expID >= pcc.Exports.Count)
                    throw new IndexOutOfRangeException($"Given export ID ({expID}) is out of range. PCC Export Count: {pcc.Exports.Count}.");

                ExportEntry export = pcc.Exports[expID];
                if (!export.ValidTextureClass())
                    throw new InvalidDataException($"Export {expID} in {pccPath} is not a texture. Class: {export.ClassName}, Object Name:{export.ObjectName}.");

                // Create Texture2D
                tex2D = new Texture2D(pcc, expID, new MEDirectories.MEDirectories(tex.GameVersion), tex.Hash);
            }

            pcc.Dispose(); // Texture2D doesn't need this anymore

            return tex2D;
        }

        internal static void ExtractTexture(TreeTexInfo tex, string filename)
        {
            // Get Texture2D
            Texture2D tex2D = GetTexture2D(tex);

            // Extract texture
            tex2D.ExtractMaxImage(filename);

            // Cleanup if required
            if (tex.AssociatedTexture != tex2D)
                tex2D.Dispose();
        }

        internal static void ME1_LowResFix(TreeTexInfo tex)
        {
            // Get Texture2D
            Texture2D tex2D = GetTexture2D(tex);

            // Apply fix
            tex2D.LowResFix();

            // Cleanup if required
            if (tex.AssociatedTexture != tex2D)
                tex2D.Dispose();
        }
    }
}
