using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32.SafeHandles;
using Task = System.Threading.Tasks.Task;

namespace Aderant.Build {

    public class FileSystem {
        /// <summary>
        /// Calculates the destination directory for a file
        /// </summary>
        /// <param name="fileToCopy">The file.</param>
        /// <returns></returns>
        internal delegate string FileDestinationSelector(FileInfo[] filesInDirectory, FileInfo fileToCopy);

        /// <summary>
        /// The default file system controller
        /// </summary>
        internal static FileSystem Default = new FileSystem();

        private static DirectoryOperations directory = new DirectoryOperations();
        private static FileOperations file = new FileOperations();

        /// <summary>
        /// Gets or sets the directory operations.
        /// </summary>
        /// <value>
        /// The directory.
        /// </value>
        public virtual DirectoryOperations Directory {
            get { return directory; }
            protected internal set { directory = value; }
        }

        /// <summary>
        /// Gets or sets the file operations.
        /// </summary>
        /// <value>
        /// The file.
        /// </value>
        public virtual FileOperations File {
            get { return file; }
            protected internal set { file = value; }
        }

        internal static async Task DirectoryCopyAsync(string source, string destination, bool recursive, bool useHardLinks) {
            await DirectoryCopyInternal(source, destination, null, recursive, useHardLinks);
        }

        internal static async Task DirectoryCopyAsync(string source, string destination, FileDestinationSelector selector, bool recursive, bool useHardLinks) {
            await DirectoryCopyInternal(source, destination, selector, recursive, useHardLinks);
        }

        private static async Task DirectoryCopyInternal(string source, string destination, FileDestinationSelector selector, bool recursive, bool useHardLinks) {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(source);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists) {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + source);
            }

            // If the destination directory doesn't exist, create it. 
            if (!System.IO.Directory.Exists(destination)) {
                System.IO.Directory.CreateDirectory(destination);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                var destinationFile = Path.Combine(destination, file.Name);

                // Ask the override hook if we should copy this file and where it should go
                if (selector != null) {
                    destinationFile = selector(files, new FileInfo(destinationFile));
                }

                if (destinationFile == null) {
                    continue;
                }

                // The user could have given us a new path so ensure the directory exists
                string targetDirectory = Path.GetDirectoryName(destinationFile);
                if (!System.IO.Directory.Exists(targetDirectory)) {
                    System.IO.Directory.CreateDirectory(targetDirectory);
                }

                //Need to delete any existing file first otherwise the create hard link won't bother doing anything.
                DeleteHardLink(destinationFile);
                if (useHardLinks) {
                    NativeMethods.CreateHardLink(destinationFile, file.FullName, IntPtr.Zero);
                } else {
                    using (FileStream sourceStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        using (FileStream destinationStream = System.IO.File.Create(destinationFile)) {
                            using (destinationStream) {
                                await sourceStream.CopyToAsync(destinationStream);
                                ClearReadOnly(destinationFile);
                            }
                        }
                    }
                }
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (recursive) {
                foreach (DirectoryInfo subdir in dirs) {
                    string temppath = Path.Combine(destination, subdir.Name);

                    await DirectoryCopyInternal(subdir.FullName, temppath, selector, recursive, useHardLinks);
                }
            }
        }

        private static void DeleteHardLink(string destinationFile) {
            if (System.IO.File.Exists(destinationFile)) {
                var success = NativeMethods.DeleteFile(destinationFile);
                if (!success) {
                    // sometimes throws a "Handle is not valid" error - don't know why (need to investigate)
                    //var fileLinkCount = NativeMethods.GetFileLinkCount(destinationFile);
                    //if (fileLinkCount > 1) {
                        var fileLinks = NativeMethods.GetFileSiblingHardLinks(destinationFile);
                        var randomOtherLink = fileLinks.FirstOrDefault(f => f.ToLowerInvariant() != destinationFile.ToLowerInvariant());
                        if (randomOtherLink != null) {
                            ClearReadOnly(destinationFile);
                            success = NativeMethods.DeleteFile(destinationFile);
                            SetReadOnly(randomOtherLink);
                        }
                    //}
                }
                if (!success) {
                    throw new IOException(string.Format("Could not delete hard link {0}", destinationFile));
                }
            }
        }

        public static void ClearReadOnly(string file) {
            FileAttributes attributes = System.IO.File.GetAttributes(file);
            System.IO.File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
        }

        public static void SetReadOnly(string file) {
            FileAttributes attributes = System.IO.File.GetAttributes(file);
            System.IO.File.SetAttributes(file, attributes | FileAttributes.ReadOnly);
        }
    }

    public class FileOperations {

        public virtual bool Exists(string file) {
            return File.Exists(file);
        }

        public virtual string ReadAllText(string file) {
            return File.ReadAllText(file);
        }

        public virtual void WriteAllText(string path, string contents) {
            File.WriteAllText(path, contents, Encoding.UTF8);
        }
    }

    public class DirectoryOperations {
        public virtual string[] GetDirectories(string path) {
            return System.IO.Directory.GetDirectories(path);
        }

        public virtual string[] GetFileSystemEntries(string path) {
            return System.IO.Directory.GetFileSystemEntries(path);
        }

        public virtual string[] GetFileSystemEntries(string path, string searchPattern) {
            return System.IO.Directory.GetFileSystemEntries(path, searchPattern);
        }

        public virtual string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption) {
            return System.IO.Directory.GetFileSystemEntries(path, searchPattern, searchOption);
        }

        public virtual bool Exists(string path) {
            return System.IO.Directory.Exists(path);
        }
    }

    internal static class NativeMethods {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateHardLink(string newFileName, string existingFileName, IntPtr securityAttributes);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool DeleteFile(string fileName);

        [StructLayout(LayoutKind.Sequential)]
        public struct BY_HANDLE_FILE_INFORMATION {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
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

        [DllImport("kernel32.dll")]
        static extern bool GetVolumePathName(string lpszFileName, [Out] StringBuilder lpszVolumePathName, uint cchBufferLength);

        [DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
        static extern bool PathAppend([In, Out] StringBuilder pszPath, string pszMore);

        public static int GetFileLinkCount(string filepath) {
            int result = 0;
            SafeFileHandle handle = CreateFile(filepath, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, FileAttributes.Archive, IntPtr.Zero);
            BY_HANDLE_FILE_INFORMATION fileInfo = new BY_HANDLE_FILE_INFORMATION();
            if (GetFileInformationByHandle(handle, out fileInfo))
                result = (int)fileInfo.NumberOfLinks;
            CloseHandle(handle);
            return result;
        }

        public static string[] GetFileSiblingHardLinks(string filepath) {
            List<string> result = new List<string>();
            uint stringLength = 256;
            StringBuilder sb = new StringBuilder(256);
            GetVolumePathName(filepath, sb, stringLength);
            string volume = sb.ToString();
            sb.Length = 0; stringLength = 256;
            IntPtr findHandle = FindFirstFileNameW(filepath, 0, ref stringLength, sb);
            if (findHandle.ToInt64() != -1) {
                do {
                    StringBuilder pathSb = new StringBuilder(volume, 256);
                    PathAppend(pathSb, sb.ToString());
                    result.Add(pathSb.ToString());
                    sb.Length = 0; stringLength = 256;
                } while (FindNextFileNameW(findHandle, ref stringLength, sb));
                FindClose(findHandle);
                return result.ToArray();
            }
            return null;
        }
    }
}