using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace QFind
{
    public static class Win32
    {
        public const int MAX_PATH = 260;
        public const int ERROR_FILE_NOT_FOUND = 2;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public const int FILE_ATTRIBUTE_READONLY = 0x00000001;
        public const int FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        public const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        public const int FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public int dwLowDateTime;
            public int dwHighDateTime;

            public DateTime ToDateTimeUtc() => DateTime.FromFileTimeUtc((long)dwHighDateTime << 32 | (uint)dwLowDateTime);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            public int dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public int nFileSizeHigh;
            public int nFileSizeLow;
            public int dwReserved0;
            public int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        public enum FINDEX_INFO_LEVELS
        {
            FindExInfoStandard,
            FindExInfoBasic,
            FindExInfoMaxInfoLevel
        }

        public enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch,
            FindExSearchLimitToDirectories,
            FindExSearchLimitToDevices
        }

        public const int FIND_FIRST_EX_CASE_SENSITIVE = 1;
        public const int FIND_FIRST_EX_LARGE_FETCH = 2;

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "FindFirstFileExW")]
        public static extern IntPtr FindFirstFileEx(
            [MarshalAs(UnmanagedType.LPWStr)]
            string lpFileName,
            FINDEX_INFO_LEVELS fInfoLevelId,
            out WIN32_FIND_DATA lpFindFileData,
            FINDEX_SEARCH_OPS fSearchOp,
            IntPtr lpSearchFilter,
            int dwAdditionalFlags);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "FindNextFileW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindNextFile(IntPtr hFindFile, ref WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindClose(IntPtr hFindFile);
    }
}
