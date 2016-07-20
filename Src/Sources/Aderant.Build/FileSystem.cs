using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
            Parallel.ForEach(files, (file, loopState) => {
                var destinationFile = Path.Combine(destination, file.Name);

                // Ask the override hook if we should copy this file and where it should go
                if (selector != null) {
                    destinationFile = selector(files, new FileInfo(destinationFile));
                }

                if (destinationFile == null) {
                    return;
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
                    FileCopy.CopyFileWriteThrough(file.FullName, destinationFile);
                }
            });

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

    internal class FileCopy {
        const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;
        public static int CopyFileWriteThrough(string inputfile, string outputfile) {
            int bufferSize = 4096;

            using (var infile = new FileStream(inputfile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileFlagNoBuffering | FileOptions.SequentialScan)) {
                
                using (var outfile = new FileStream(outputfile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough)) {
                    infile.CopyTo(outfile);
                }
            }

            return 1;
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

        public void Delete(string path) {
            File.Delete(path);
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
    }

    public class PhysicalFileSystem : IFileSystem2 {
        private readonly string root;
     
        public PhysicalFileSystem(string root) {
            if (String.IsNullOrEmpty(root)) {
                throw new ArgumentException("Argument cannot be null or empty", nameof(root));
            }
            this.root = root;
        }

        public string Root {
            get {
                return root;
            }
        }

        public virtual string GetFullPath(string path) {
            if (String.IsNullOrEmpty(path)) {
                return Root;
            }
            return Path.Combine(Root, path);
        }

        public virtual void AddFile(string path, Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            AddFileCore(path, targetStream => stream.CopyTo(targetStream));
        }

        public virtual void AddFile(string path, Action<Stream> writeToStream) {
            if (writeToStream == null) {
                throw new ArgumentNullException(nameof(writeToStream));
            }

            AddFileCore(path, writeToStream);
        }

        private void AddFileCore(string path, Action<Stream> writeToStream) {
            EnsureDirectory(Path.GetDirectoryName(path));

            string fullPath = GetFullPath(path);

            using (Stream outputStream = File.Create(fullPath)) {
                writeToStream(outputStream);
            }
        }

        public virtual void DeleteFile(string path) {
            if (!FileExists(path)) {
                return;
            }

            try {
                MakeFileWritable(path);
                path = GetFullPath(path);
                File.Delete(path);
            } catch (FileNotFoundException) {

            }
        }

        public virtual void DeleteDirectory(string path) {
            DeleteDirectory(path, recursive: false);
        }

        public virtual void DeleteDirectory(string path, bool recursive) {
            if (!DirectoryExists(path)) {
                return;
            }

            try {
                path = GetFullPath(path);
                Directory.Delete(path, recursive);

                // The directory is not guaranteed to be gone since there could be
                // other open handles. Wait, up to half a second, until the directory is gone.
                for (int i = 0; Directory.Exists(path) && i < 5; ++i) {
                    System.Threading.Thread.Sleep(100);
                }
            } catch (DirectoryNotFoundException) {
            }
        }

        public virtual IEnumerable<string> GetFiles(string path, bool recursive) {
            return GetFiles(path, null, recursive);
        }

        public virtual IEnumerable<string> GetFiles(string path, string filter, bool recursive) {
            path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
            if (String.IsNullOrEmpty(filter)) {
                filter = "*.*";
            }
            try {
                if (!Directory.Exists(path)) {
                    return Enumerable.Empty<string>();
                }
                return Directory.EnumerateFiles(path, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                                .Select(MakeRelativePath);
            } catch (UnauthorizedAccessException) {

            } catch (DirectoryNotFoundException) {

            }

            return Enumerable.Empty<string>();
        }

        public virtual IEnumerable<string> GetDirectories(string path) {
            try {
                path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
                if (!Directory.Exists(path)) {
                    return Enumerable.Empty<string>();
                }
                return Directory.EnumerateDirectories(path)
                                .Select(MakeRelativePath);
            } catch (UnauthorizedAccessException) {

            } catch (DirectoryNotFoundException) {

            }

            return Enumerable.Empty<string>();
        }

        public virtual DateTimeOffset GetLastModified(string path) {
            path = GetFullPath(path);
            if (File.Exists(path)) {
                return File.GetLastWriteTimeUtc(path);
            }
            return Directory.GetLastWriteTimeUtc(path);
        }

        public DateTimeOffset GetCreated(string path) {
            path = GetFullPath(path);
            if (File.Exists(path)) {
                return File.GetCreationTimeUtc(path);
            }
            return Directory.GetCreationTimeUtc(path);
        }

        public DateTimeOffset GetLastAccessed(string path) {
            path = GetFullPath(path);
            if (File.Exists(path)) {
                return File.GetLastAccessTimeUtc(path);
            }
            return Directory.GetLastAccessTimeUtc(path);
        }

        public virtual bool FileExists(string path) {
            path = GetFullPath(path);
            return File.Exists(path);
        }

        public virtual bool DirectoryExists(string path) {
            path = GetFullPath(path);
            return Directory.Exists(path);
        }

        public virtual Stream OpenFile(string path) {
            path = GetFullPath(path);
            return File.OpenRead(path);
        }

        public virtual Stream CreateFile(string path) {
            string fullPath = GetFullPath(path);

            // before creating the file, ensure the parent directory exists first.
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            return File.Create(fullPath);
        }

        protected string MakeRelativePath(string fullPath) {
            return fullPath.Substring(Root.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        protected virtual void EnsureDirectory(string path) {
            path = GetFullPath(path);
            Directory.CreateDirectory(path);
        }

        public void MakeFileWritable(string path) {
            path = GetFullPath(path);
            FileAttributes attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReadOnly)) {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        public virtual void MoveFile(string source, string destination) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }
            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            }
            string srcFull = GetFullPath(source);
            string destFull = GetFullPath(destination);

            if (string.Equals(srcFull, destFull, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            try {
                File.Move(srcFull, destFull);
            } catch (IOException) {
                File.Delete(srcFull);
            }
        }
    }

    public interface IFileSystem2 {
      
        string Root { get; }
        void DeleteDirectory(string path, bool recursive);
        IEnumerable<string> GetFiles(string path, string filter, bool recursive);
        IEnumerable<string> GetDirectories(string path);
        string GetFullPath(string path);
        void DeleteFile(string path);
        bool FileExists(string path);
        bool DirectoryExists(string path);
        void AddFile(string path, Stream stream);
        void AddFile(string path, Action<Stream> writeToStream);
        void MakeFileWritable(string path);
        void MoveFile(string source, string destination);
        Stream CreateFile(string path);
        Stream OpenFile(string path);
        DateTimeOffset GetLastModified(string path);
        DateTimeOffset GetCreated(string path);
        DateTimeOffset GetLastAccessed(string path);
    }

    internal static class PathUtility {
        public static string GetPathWithForwardSlashes(string path) {
            return path.Replace('\\', '/');
        }

        public static string EnsureTrailingSlash(string path) {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter) {
            if (path == null) {
                throw new ArgumentNullException(nameof(path));
            }

            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (path.Length == 0
                || path[path.Length - 1] == trailingCharacter) {
                return path;
            }

            return path + trailingCharacter;
        }
    }
}