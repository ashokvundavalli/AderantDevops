using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.IO;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.Utilities;
using Microsoft.Build.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Aderant.Build {

    [Export]
    [Export(typeof(IFileSystem2))]
    [Export(typeof(IFileSystem))]
    [Export("FileSystemService")]
    public class PhysicalFileSystem : IFileSystem2 {
        private static class LinkHelper {
            public static readonly CreateSymlinkLink CreateSymlinkLink = (newFileName, target, flags) => {
                bool result = NativeMethods.CreateSymbolicLink(newFileName, target, 0);
                if (!result) {
                    CheckLastError(newFileName, target);
                }
                return result;
            };

            public static readonly CreateSymlinkLink CreateHardlink = (newFileName, target, flags) => {
                bool result = NativeMethods.CreateHardLink(newFileName, target, IntPtr.Zero);
                if (!result) {
                    CheckLastError(newFileName, target);
                }

                return result;
            };

            private static void CheckLastError(string newFileName, string target) {
                if (Marshal.GetLastWin32Error() != 0) {
                    throw new IOException($"Failed to create link {newFileName} ==> {target}", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                }
            }
        }

        private readonly ILogger logger;

        [ImportingConstructor]
        public PhysicalFileSystem() {
        }

        /// <summary>
        /// Creates a new instance rooted at the given directory.
        /// </summary>
        /// <param name="root">The root directory</param>
        public PhysicalFileSystem(string root)
            : this(root, null) {
        }

        public PhysicalFileSystem(ILogger logger) : this(null, logger) {
        }

        public PhysicalFileSystem(string root, ILogger logger) {
            this.Root = root;
            this.logger = logger;
        }

        /// <summary>
        /// The root directory for this instance.
        /// </summary>
        public string Root { get; }

        public virtual string GetFullPath(string path) {
            ErrorUtilities.IsNotNull(path, nameof(path));

            return Path.GetFullPath(path);
        }

        public string GetParent(string path) {
            return Directory.GetParent(path.TrimEnd(PathUtility.DirectorySeparatorCharArray)).FullName;
        }

        public string CreateDirectory(string path) {
            return Directory.CreateDirectory(path).FullName;
        }

        public virtual string AddFile(string path, Stream stream) {
            ErrorUtilities.IsNotNull(stream, nameof(stream));

            return AddFileCore(path, targetStream => stream.CopyTo(targetStream));
        }

        public virtual string AddFile(string path, Action<Stream> writeToStream) {
            ErrorUtilities.IsNotNull(writeToStream, nameof(writeToStream));

            return AddFileCore(path, writeToStream);
        }

        public virtual void DeleteFile(string path) {
            DeleteFile(path, false);
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
                    Thread.Sleep(100);
                }
            } catch (DirectoryNotFoundException) {
            }
        }

        public virtual DateTime GetLastAccessTimeUtc(string path) {
            return File.GetLastAccessTimeUtc(path);
        }

        public virtual IEnumerable<string> GetFiles(string path, string inclusiveFilter = null, bool recursive = true) {
            path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
            if (string.IsNullOrEmpty(inclusiveFilter)) {
                inclusiveFilter = "*";
            }

            try {
                if (!Directory.Exists(path)) {
                    return Enumerable.Empty<string>();
                }

                var files = Directory.EnumerateFiles(path, inclusiveFilter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                return files;
            } catch (UnauthorizedAccessException) {

            } catch (DirectoryNotFoundException) {

            }

            return Enumerable.Empty<string>();
        }

        public virtual IEnumerable<string> GetDirectories(string path, bool recursive = false) {
            try {
                path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
                if (!Directory.Exists(path)) {
                    return Enumerable.Empty<string>();
                }

                var files = Directory.EnumerateDirectories(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                return files;
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
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        }

        public virtual Stream OpenFileForWrite(string path) {
            path = GetFullPath(path);
            return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public virtual Stream CreateFile(string path) {
            string fullPath = GetFullPath(path);
            EnsureDirectory(Path.GetDirectoryName(fullPath));

            return File.Create(fullPath);
        }

        public virtual void MakeFileWritable(string path) {
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
                EnsureDirectory(Path.GetDirectoryName(destFull));

                File.Move(srcFull, destFull);
            } catch (IOException) {
                DeleteFileCore(srcFull);
            }
        }

        public void CopyFile(string source, string destination) {
            CopyFile(source, destination, false);
        }

        public void CopyFile(string source, string destination, bool overwrite) {
            // before creating the file, ensure the parent directory exists first.
            EnsureDirectory(Path.GetDirectoryName(destination));

            CopyFileInternal(source, destination, overwrite);
        }

        public virtual void CopyDirectory(string source, string destination) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            }

            string srcFull = GetFullPath(source);
            string destFull = GetFullPath(destination);

            CopyDirectoryInternal(srcFull, destFull, true);
        }

        public void MoveDirectory(string source, string destination) {
            if (!DirectoryExists(destination)) {
                Directory.CreateDirectory(Directory.GetParent(destination).FullName);
                Directory.Move(source, destination);
            } else {
                IEnumerable<string> files = GetFiles(source, true);

                source = PathUtility.EnsureTrailingSlash(source);

                foreach (string file in files) {
                    MoveFile(file, Path.Combine(destination, file.Replace(source, "", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        public ActionBlock<PathSpec> BulkCopy(IEnumerable<PathSpec> pathSpecs, bool overwrite, bool useSymlinks = false, bool useHardlinks = false) {
            ExecutionDataflowBlockOptions actionBlockOptions = new ExecutionDataflowBlockOptions {
                MaxDegreeOfParallelism = ParallelismHelper.MaxDegreeOfParallelism
            };

            HashSet<string> knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CreateSymlinkLink link = null;
            if (useSymlinks) {
                link = LinkHelper.CreateSymlinkLink;
            } else if (useHardlinks) {
                link = LinkHelper.CreateHardlink;
            }

            ActionBlock<PathSpec> bulkCopy = new ActionBlock<PathSpec>(
                async file => {
                    // Break from synchronous thread context of caller to get onto thread pool thread.
                    await Task.Yield();

                    string destination = Path.GetDirectoryName(file.Destination);

                    lock (knownPaths) {
                        if (!knownPaths.Contains(destination)) {
                            EnsureDirectoryInternal(destination);
                            knownPaths.Add(destination);
                        }
                    }

                    switch (file.UseHardLink) {
                        case true:
                            CopyViaLink(file, LinkHelper.CreateHardlink);
                            break;
                        case false:
                            CopyFileInternal(file.Location, file.Destination, overwrite);
                            break;
                        default:
                            if (link != null) {
                                CopyViaLink(file, link);
                            } else {
                                CopyFileInternal(file.Location, file.Destination, overwrite);
                            }
                            break;
                    }
                },
                actionBlockOptions);

            foreach (PathSpec pathSpec in pathSpecs) {
                bulkCopy.Post(pathSpec);
            }

            bulkCopy.Complete();

            return bulkCopy;
        }



        private void CopyViaLink(PathSpec file, CreateSymlinkLink link) {
            try {
                CreateLink(file.Location, file.Destination, link, true);
            } catch (UnauthorizedAccessException) {
                logger?.Warning("File " + file.Destination + " is in use or can not be accessed. Trying to rename.");

                MoveFile(file.Destination, file.Destination + ".__LOCKED");
                CreateLink(file.Location, file.Destination, link, true);
            }
        }

        public void ExtractZipToDirectory(string sourceArchiveFileName, string destination, bool overwrite = false) {
            ExtractToDirectory(sourceArchiveFileName, destination, overwrite);
        }

        public bool IsSymlink(string linkPath) {
            DirectoryInfo directoryInfo = new DirectoryInfo(linkPath);
            if (directoryInfo.Exists) {
                return directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }

            return false;
        }

        /// <summary>
        /// Makes a file link.
        /// </summary>
        /// <param name="linkPath">The link path.</param>
        /// <param name="actualFilePath">The actual file path.</param>
        /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
        public void CreateFileHardLink(string linkPath, string actualFilePath, bool overwrite = false) {
            if (Path.IsPathRooted(linkPath) && Path.IsPathRooted(actualFilePath)) {
                var linkRoot = Path.GetPathRoot(linkPath);
                var actualPathRoot = Path.GetPathRoot(actualFilePath);

                if (!string.Equals(linkRoot, actualPathRoot)) {
                    CreateFileSymlink(actualFilePath, linkPath, overwrite);
                    return;
                }
            }
            CreateLink(actualFilePath, linkPath, LinkHelper.CreateHardlink, overwrite);
        }

        /// <summary>
        /// Makes a file link.
        /// </summary>
        /// <param name="linkPath">The link path.</param>
        /// <param name="actualFilePath">The actual file path.</param>
        /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
        public void CreateFileSymlink(string linkPath, string actualFilePath, bool overwrite = false) {
            CreateLink(actualFilePath, linkPath, LinkHelper.CreateSymlinkLink, overwrite);
        }

        /// <summary>
        /// Creates a junction point from the specified directory to the specified target directory.
        /// </summary>
        /// <param name="linkPath">The link path.</param>
        /// <param name="actualFolderPath">The actual folder path.</param>
        /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
        public void CreateDirectoryLink(string linkPath, string actualFolderPath, bool overwrite = false) {
            if (logger != null) {
                logger.Info("Creating junction {0} <=====> {1}", linkPath, actualFolderPath);
            }

            if (overwrite) {
                DeleteDirectory(linkPath, true);
            }

            JunctionNativeMethods.CreateJunction(actualFolderPath, linkPath, overwrite);
        }

        internal static void CopyFileInternal(string source, string destination, bool overwrite) {
            var linkTargetState = GetLinkTargetState(source, destination);

            switch (linkTargetState) {
                case LinkTargetState.IsSibling:
                case LinkTargetState.IsSameOrSibling:
                    return;
                case LinkTargetState.NonExistent:
                    break;
            }

            File.Copy(source, destination, overwrite);
        }

        private string AddFileCore(string path, Action<Stream> writeToStream) {
            EnsureDirectory(Path.GetDirectoryName(path));

            string fullPath = GetFullPath(path);

            using (Stream outputStream = File.Create(fullPath)) {
                writeToStream(outputStream);
            }

            return fullPath;
        }

        public virtual IEnumerable<string> GetFiles(string path, bool recursive) {
            return GetFiles(path, null, recursive);
        }

        protected virtual void EnsureDirectory(string path) {
            path = GetFullPath(path);

            EnsureDirectoryInternal(path);
        }

        private void EnsureDirectoryInternal(string path) {
            // If the destination directory doesn't exist, create it.
            if (!DirectoryExists(path)) {
                Directory.CreateDirectory(path);
            }
        }

        private void CopyDirectoryInternal(string source, string destination, bool recursive) {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(source);

            if (!dir.Exists) {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + source);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            EnsureDirectory(destination);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                string destinationPath = Path.Combine(destination, file.Name);

                FileInfo destFile = new FileInfo(destinationPath);
                if (destFile.Exists) {
                    if (destFile.IsReadOnly) {
                        // Clear read-only
                        logger?.Warning($"Overwriting read-only file: {file.Name}.");
                        destFile.IsReadOnly = false;
                    }

                    file.CopyTo(destFile.FullName, true); // Copy and overwrite
                } else {
                    file.CopyTo(destinationPath, false);
                }
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (recursive) {
                foreach (DirectoryInfo subDir in dirs) {
                    string tempPath = Path.Combine(destination, subDir.Name);
                    CopyDirectoryInternal(subDir.FullName, tempPath, true);
                }
            }
        }

        private void ExtractToDirectory(string zipArchive, string destinationDirectoryName, bool overwrite) {
            if (string.IsNullOrWhiteSpace(zipArchive)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(zipArchive));
            }
            if (string.IsNullOrWhiteSpace(destinationDirectoryName)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(destinationDirectoryName));
            }

            if (!overwrite) {
                ZipFile.ExtractToDirectory(zipArchive, destinationDirectoryName);
                return;
            }

            using (ZipArchive archive = new ZipArchive(File.OpenRead(zipArchive), ZipArchiveMode.Read)) {
                Dictionary<string, byte> directoriesKnownToExist = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

                foreach (ZipArchiveEntry file in archive.Entries) {
                    string completeFileName = Path.Combine(destinationDirectoryName, file.FullName);

                    var destinationFolder = Path.GetDirectoryName(completeFileName);

                    if (!string.IsNullOrEmpty(destinationFolder) &&
                        !directoriesKnownToExist.ContainsKey(destinationFolder)) {
                        if (!DirectoryExists(destinationFolder)) {
                            Directory.CreateDirectory(destinationFolder);
                        }

                        // It's very common for a lot of files to be copied to the same folder.
                        // Eg., "c:\foo\a"->"c:\bar\a", "c:\foo\b"->"c:\bar\b" and so forth.
                        // We don't want to check whether this folder exists for every single file we copy. So store which we've checked.
                        directoriesKnownToExist.Add(destinationFolder, 0);
                    }

                    using (FileStream fileStream = File.Create(completeFileName)) {
                        try {
                            using (Stream stream = file.Open()) {
                                stream.CopyTo(fileStream);
                            }
                        } catch {
                            logger?.Error("Unable to successfully extract archive: '{0}'.", zipArchive);
                            throw;
                        }
                    }
                }
            }
        }

        internal static LinkTargetState GetLinkTargetState(string fileLocation, string fileDestination) {
            if (fileLocation.Equals(fileDestination, StringComparison.OrdinalIgnoreCase)) {
                return LinkTargetState.IsSameOrSibling;
            }

            // This can be subject to race conditions - if we are invoked by multiple projects
            // e.g as part of the CopyLocal process then another project may have already created the destination.
            // Unfortunately we cannot get hold of the full copy local closure so we need to use careful error
            // handling instead.
            string[] links = NativeMethods.GetFileSiblingHardLinks(fileLocation);

            if (links != null && links.Length > 1) {
                // Test if source and destination are already the same file.
                foreach (string link in links) {
                    if (string.Equals(link, fileDestination, StringComparison.OrdinalIgnoreCase)) {
                        return LinkTargetState.IsSibling;
                    }
                }
            }

            return LinkTargetState.NonExistent;
        }

        internal enum LinkTargetState {
            NonExistent,
            IsSameOrSibling,
            IsSibling
        }

        /// <param name="fileLocation">The symbolic link to be created.</param>
        /// <param name="fileDestination">The target of the symbolic link.</param>
        /// <param name="createLink">The creation delegate.</param>
        /// <param name="overwrite"></param>
        private void CreateLink(string fileLocation, string fileDestination, CreateSymlinkLink createLink, bool overwrite) {
            var destinationState = GetLinkTargetState(fileLocation, fileDestination);

            switch (destinationState) {
                case LinkTargetState.IsSibling:
                case LinkTargetState.IsSameOrSibling:
                    return;
                case LinkTargetState.NonExistent:
                    break;
            }

            // CreateHardLink and CreateSymbolicLink cannot overwrite an existing file or link
            // so we need to delete the existing entry before we create the hard or symbolic link.
            DeleteFile(fileDestination, true);
            
            // Check again if the file exists, if it does we must be racing with another thread.
            var exists = FileExists(fileDestination);

            if (!exists) {
                createLink(fileDestination, fileLocation, (uint) NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_FILE);
            }
        }

        private void DeleteFile(string path, bool skipChecks) {
            if (!FileExists(path)) {
                return;
            }

            try {
                if (!skipChecks) {
                    MakeFileWritable(path);
                }

                path = GetFullPath(path);
                DeleteFileCore(path);
            } catch (FileNotFoundException) {

            }
        }

        protected internal virtual void DeleteFileCore(string path) {
            File.Delete(path);
        }

        internal delegate bool CreateSymlinkLink(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);
    }
}
