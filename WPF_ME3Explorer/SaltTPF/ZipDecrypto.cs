using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaltTPF
{
    static class ZipDecrypto
    {
        public static byte[] tpfkey = {0x73, 0x2A, 0x63, 0x7D, 0x5F, 0x0A, 0xA6, 0xBD,
				0x7D, 0x65, 0x7E, 0x67, 0x61, 0x2A, 0x7F, 0x7F,
				0x74, 0x61, 0x67, 0x5B, 0x60, 0x70, 0x45, 0x74,
				0x5C, 0x22, 0x74, 0x5D, 0x6E, 0x6A, 0x73, 0x41,
				0x77, 0x6E, 0x46, 0x47, 0x77, 0x49, 0x0C, 0x4B,
				0x46, 0x6F };

        private static byte MagicByte(UInt32[] Keys)
        {
            UInt16 t = (UInt16)((UInt16)(Keys[2] & 0xFFFF) | 2);
            return (byte)((t * (t ^ 1)) >> 8);
        }

        public static byte[] DecryptData(ZipReader.ZipEntry entry, byte[] block, int start, int count)
        {
            if (block == null || block.Length < count || count < 12)
                throw new ArgumentException("Invalid arguments for decryption");

            UInt32[] Keys = InitCipher(tpfkey);

            // Decrypt crypt header
            DecryptBlock(block, start, 12, Keys); // 12 = crypt header size

            // KFreon: Testing header
            //Console.WriteLine($"crypt header crc: {block[start + 10]}, {block[start + 11]}");

            // KFreon: Doesn't seem to require this
            /*if (block[11] != (byte)((entry.CRC >> 24) & 0xff) && (entry.BitFlag & 0x8) != 0x8)
                Console.WriteLine("Incorrect password");*/

            DecryptBlock(block, start + 12, count - 12, Keys);  // Decrypt main block after crypt header
            return block;
        }

        private static UInt32[] InitCipher(byte[] password)
        {
            UInt32[] Keys = new UInt32[] { 305419896, 591751049, 878082192 };
            for (int i = 0; i < password.Length; i++)
            {
                Keys = UpdateKeys(password[i], Keys);
            }
            return Keys;
        }

        private static UInt32[] UpdateKeys(byte byteval, UInt32[] Keys)
        {
            Keys[0] = (UInt32)CRC32.ComputeCrc32(Keys[0], byteval);
            Keys[1] = Keys[1] + (byte)Keys[0];
            Keys[1] = Keys[1] * 0x08088405 + 1;
            Keys[2] = (UInt32)CRC32.ComputeCrc32(Keys[2], (byte)(Keys[1] >> 24));
            return Keys;
        }

        private static void DecryptBlock(byte[] block, int offset, int count, UInt32[] Keys)
        {
            for (int i = offset; i < offset + count; i++)
            {
                block[i] = (byte)(block[i] ^ MagicByte(Keys));
                Keys = UpdateKeys(block[i], Keys);
            }
        }
    }
}
