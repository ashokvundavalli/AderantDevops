﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace Aderant.Build {
    internal static class NativeMethods {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateHardLink(string newFileName, string existingFileName, IntPtr securityAttributes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool DeleteFile(string fileName);

        [StructLayout(LayoutKind.Sequential)]
        public struct BY_HANDLE_FILE_INFORMATION {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ThrowOnUnmappableChar = true)]
        static extern SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(SafeHandle hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr FindFirstFileNameW(
            string lpFileName,
            uint dwFlags,
            ref uint stringLength,
            StringBuilder fileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool FindNextFileNameW(
            IntPtr hFindStream,
            ref uint stringLength,
            StringBuilder fileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FindClose(IntPtr fFindHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ThrowOnUnmappableChar = true)]
        static extern bool GetVolumePathName(string lpszFileName, [Out] StringBuilder lpszVolumePathName, uint cchBufferLength);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ThrowOnUnmappableChar = true)]
        static extern bool PathAppend([In, Out] StringBuilder pszPath, string pszMore);

        public static int GetFileLinkCount(string filepath) {
            int result = 0;
            SafeFileHandle handle = CreateFile(filepath, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, FileAttributes.Archive, IntPtr.Zero);
            BY_HANDLE_FILE_INFORMATION fileInfo = new BY_HANDLE_FILE_INFORMATION();
            if (GetFileInformationByHandle(handle, out fileInfo))
                result = (int) fileInfo.NumberOfLinks;
            CloseHandle(handle);
            return result;
        }

        public static string[] GetFileSiblingHardLinks(string filepath) {
            List<string> result = new List<string>();
            uint stringLength = 256;
            StringBuilder sb = new StringBuilder(256);
            GetVolumePathName(filepath, sb, stringLength);
            string volume = sb.ToString();
            sb.Length = 0;
            stringLength = 256;
            IntPtr findHandle = FindFirstFileNameW(filepath, 0, ref stringLength, sb);
            if (findHandle.ToInt64() != -1) {
                do {
                    StringBuilder pathSb = new StringBuilder(volume, 256);
                    PathAppend(pathSb, sb.ToString());
                    result.Add(pathSb.ToString());
                    sb.Length = 0;
                    stringLength = 256;
                } while (FindNextFileNameW(findHandle, ref stringLength, sb));
                FindClose(findHandle);
                return result.ToArray();
            }
            return null;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);

        internal enum SymbolicLink : uint {
            SYMBOLIC_LINK_FLAG_FILE = 0,
            SYMBOLIC_LINK_FLAG_DIRECTORY = 1
        }

    }
}
