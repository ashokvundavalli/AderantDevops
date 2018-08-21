﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using Aderant.Build.Logging;

namespace Aderant.Build {

    [Export]
    [Export(typeof(IFileSystem2))]
    [Export(typeof(IFileSystem))]
    [Export("FileSystemService")]
    public class PhysicalFileSystem : IFileSystem2 {
        private ILogger logger;

        [ImportingConstructor]
        public PhysicalFileSystem() {
        }

        /// <summary>
        /// Creates a new instance a a PhysicalFileSystem at the given root directory.
        /// </summary>
        /// <param name="root">The root directory</param>
        public PhysicalFileSystem(string root)
            : this(root, null) {
        }

        public PhysicalFileSystem(string root, ILogger logger) {
            if (string.IsNullOrEmpty(root)) {
                throw new ArgumentException("Argument cannot be null or empty", nameof(root));
            }

            this.Root = root;
            this.logger = logger;
        }

        /// <summary>
        /// The root directory for this instance.
        /// </summary>
        public string Root { get; }

        public IEnumerable<string> GetDirectoryNameOfFilesAbove(string startingDirectory, string filter, IReadOnlyCollection<string> ceilingDirectories = null) {
            if (startingDirectory == null) {
                throw new ArgumentNullException(nameof(startingDirectory));
            }

            if (filter == null) {
                throw new ArgumentNullException(nameof(filter));
            }

            return SearchByPattern(startingDirectory, filter, ceilingDirectories);
        }

        public string GetDirectoryNameOfFileAbove(string startingDirectory, string fileName) {
            return GetDirectoryNameOfFileAbove(startingDirectory, fileName, null, false);
        }

        public string GetDirectoryNameOfFileAbove(string startingDirectory, string fileName, string[] ceilingDirectories = null, bool considerDirectories = false) {
            if (startingDirectory == null) {
                throw new ArgumentNullException(nameof(startingDirectory));
            }

            if (fileName == null) {
                throw new ArgumentNullException(nameof(fileName));
            }

            return SearchForFile(startingDirectory, fileName, new SearchOptions { CeilingDirectories = ceilingDirectories, ConsiderDirectories = considerDirectories});
        }

        public virtual string GetFullPath(string path) {
            return Path.GetFullPath(path);
        }

        public string GetParent(string path) {
            return Directory.GetParent(path.TrimEnd(Path.DirectorySeparatorChar)).FullName;
        }

        public virtual string AddFile(string path, Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return AddFileCore(path, targetStream => stream.CopyTo(targetStream));
        }

        public virtual string AddFile(string path, Action<Stream> writeToStream) {
            if (writeToStream == null) {
                throw new ArgumentNullException(nameof(writeToStream));
            }

            return AddFileCore(path, writeToStream);
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

        public virtual IEnumerable<string> GetFiles(string path, string inclusiveFilter, bool recursive) {
            path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
            if (string.IsNullOrEmpty(inclusiveFilter)) {
                inclusiveFilter = "*.*";
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
                EnsureDirectory(Path.GetDirectoryName(destFull));

                File.Move(srcFull, destFull);
            } catch (IOException) {
                File.Delete(srcFull);
            }
        }

        public void CopyFile(string source, string destination) {
           CopyFile(source, destination, false);
        }

        public void CopyFile(string source, string destination, bool overwrite) {
            // before creating the file, ensure the parent directory exists first.
            EnsureDirectory(Path.GetDirectoryName(destination));

            File.Copy(source, destination, overwrite);
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

                foreach (string file in files) {
                    MoveFile(file, Path.Combine(destination, file));
                }
            }
        }

        private IEnumerable<string> SearchByPattern(string startingDirectory, string filter, IReadOnlyCollection<string> ceilingDirectories) {
            // Canonicalize our starting location
            string lookInDirectory = Path.GetFullPath(startingDirectory);

            do {
                IEnumerable<string> entries = this.GetFiles(lookInDirectory, filter, false);

                // If we successfully locate the file in the directory that we're
                // looking in, simply return that location. Otherwise we'll
                // keep moving up the tree.
                if (entries.Any()) {
                    // We've found the file, return the directory we found it in
                    return entries;
                } else {
                    // GetDirectoryName will return null when we reach the root
                    // terminating our search
                    lookInDirectory = Path.GetDirectoryName(lookInDirectory);
                }
            } while (lookInDirectory != null);

            return Enumerable.Empty<string>();
        }

        private string SearchForFile(string startingDirectory, string fileName, SearchOptions searchOptions) {
            // Canonicalize our starting location
            string lookInDirectory = Path.GetFullPath(startingDirectory);

            do {
                // Construct the path that we will use to test against
                string possibleFileDirectory = Path.Combine(lookInDirectory, fileName);

                // If we successfully locate the file in the directory that we're
                // looking in, simply return that location. Otherwise we'll
                // keep moving up the tree.
                if (this.FileExists(possibleFileDirectory) || searchOptions.ConsiderDirectories && DirectoryExists(possibleFileDirectory)) {
                    // We've found the file, return the directory we found it in
                    return lookInDirectory;
                } else {
                    // GetDirectoryName will return null when we reach the root
                    // terminating our search
                    lookInDirectory = Path.GetDirectoryName(lookInDirectory);
                }
            } while (lookInDirectory != null);

            // When we didn't find the location, then return an empty string
            return string.Empty;
        }

        private string AddFileCore(string path, Action<Stream> writeToStream) {
            EnsureDirectory(Path.GetDirectoryName(path));

            string fullPath = GetFullPath(path);

            using (Stream outputStream = File.Create(fullPath)) {
                writeToStream(outputStream);
            }

            return fullPath;
        }

        public virtual void DeleteDirectory(string path) {
            DeleteDirectory(path, recursive: false);
        }

        public virtual IEnumerable<string> GetFiles(string path, bool recursive) {
            return GetFiles(path, null, recursive);
        }

        protected virtual void EnsureDirectory(string path) {
            path = GetFullPath(path);

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
                foreach (DirectoryInfo subdir in dirs) {
                    string temppath = Path.Combine(destination, subdir.Name);
                    CopyDirectoryInternal(subdir.FullName, temppath, recursive);
                }
            }
        }
    }

    internal struct SearchOptions {
        public IEnumerable<string> CeilingDirectories { get; set; }
        public bool ConsiderDirectories { get; set; }
    }
}
