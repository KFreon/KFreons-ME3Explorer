using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WPF_ME3Explorer
{
    internal static class FileIOHelper
    {
        // Credit: https://stackoverflow.com/questions/26321366/fastest-way-to-get-directory-data-in-net
        // https://stackoverflow.com/users/982639/alexandru


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr FindFirstFileW(string lpFileName, out WIN32_FIND_DATAW lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATAW lpFindFileData);

        [DllImport("kernel32.dll")]
        static extern bool FindClose(IntPtr hFindFile);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATAW
        {
            public FileAttributes dwFileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public int nFileSizeHigh;
            public int nFileSizeLow;
            public int dwReserved0;
            public int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public static bool FindNextFilePInvokeRecursive(string path, out List<string> files, out List<string> directories)
        {
            List<string> fileList = new List<string>();
            List<string> directoryList = new List<string>();
            WIN32_FIND_DATAW findData;
            IntPtr findHandle = INVALID_HANDLE_VALUE;
            List<Tuple<string, DateTime>> info = new List<Tuple<string, DateTime>>();
            try
            {
                findHandle = FindFirstFileW(path + @"\*", out findData);
                if (findHandle != INVALID_HANDLE_VALUE)
                {
                    do
                    {
                        // Skip current directory and parent directory symbols that are returned.
                        if (findData.cFileName != "." && findData.cFileName != "..")
                        {
                            string fullPath = path + @"\" + findData.cFileName;
                            // Check if this is a directory and not a symbolic link since symbolic links could lead to repeated files and folders as well as infinite loops.
                            if (findData.dwFileAttributes.HasFlag(FileAttributes.Directory) && !findData.dwFileAttributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                directoryList.Add(fullPath);
                                List<string> subDirectoryFileList = new List<string>();
                                List<string> subDirectoryDirectoryList = new List<string>();
                                if (FindNextFilePInvokeRecursive(fullPath, out subDirectoryFileList, out subDirectoryDirectoryList))
                                {
                                    fileList.AddRange(subDirectoryFileList);
                                    directoryList.AddRange(subDirectoryDirectoryList);
                                }
                            }
                            else if (!findData.dwFileAttributes.HasFlag(FileAttributes.Directory))
                                fileList.Add(fullPath);
                        }
                    }
                    while (FindNextFile(findHandle, out findData));
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Caught exception while trying to enumerate a directory. {0}", exception.ToString());
                if (findHandle != INVALID_HANDLE_VALUE) FindClose(findHandle);
                files = null;
                directories = null;
                return false;
            }
            if (findHandle != INVALID_HANDLE_VALUE) FindClose(findHandle);
            files = fileList;
            directories = directoryList;
            return true;
        }

        public static bool FindNextFilePInvokeRecursiveParalleled(string path, out List<string> files, out List<string> directories)
        {
            List<string> fileList = new List<string>();
            object fileListLock = new object();
            List<string> directoryList = new List<string>();
            object directoryListLock = new object();
            WIN32_FIND_DATAW findData;
            IntPtr findHandle = INVALID_HANDLE_VALUE;
            List<Tuple<string, DateTime>> info = new List<Tuple<string, DateTime>>();
            try
            {
                path = path.EndsWith(@"\") ? path : path + @"\";
                findHandle = FindFirstFileW(path + @"*", out findData);
                if (findHandle != INVALID_HANDLE_VALUE)
                {
                    do
                    {
                        // Skip current directory and parent directory symbols that are returned.
                        if (findData.cFileName != "." && findData.cFileName != "..")
                        {
                            string fullPath = path + findData.cFileName;
                            // Check if this is a directory and not a symbolic link since symbolic links could lead to repeated files and folders as well as infinite loops.
                            if (findData.dwFileAttributes.HasFlag(FileAttributes.Directory) && !findData.dwFileAttributes.HasFlag(FileAttributes.ReparsePoint))
                                directoryList.Add(fullPath);
                            else if (!findData.dwFileAttributes.HasFlag(FileAttributes.Directory))
                                fileList.Add(fullPath);
                        }
                    }
                    while (FindNextFile(findHandle, out findData));
                    directoryList.AsParallel().ForAll(x =>
                    {
                        List<string> subDirectoryFileList = new List<string>();
                        List<string> subDirectoryDirectoryList = new List<string>();
                        if (FindNextFilePInvokeRecursive(x, out subDirectoryFileList, out subDirectoryDirectoryList))
                        {
                            lock (fileListLock)
                            {
                                fileList.AddRange(subDirectoryFileList);
                            }
                            lock (directoryListLock)
                            {
                                directoryList.AddRange(subDirectoryDirectoryList);
                            }
                        }
                    });
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Caught exception while trying to enumerate a directory. {0}", exception.ToString());
                if (findHandle != INVALID_HANDLE_VALUE) FindClose(findHandle);
                files = null;
                directories = null;
                return false;
            }
            if (findHandle != INVALID_HANDLE_VALUE) FindClose(findHandle);
            files = fileList;
            directories = directoryList;
            return true;
        }
    }
}
