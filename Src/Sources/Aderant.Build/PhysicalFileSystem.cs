using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;

namespace Aderant.Build {
    public class PhysicalFileSystem : IFileSystem2 {
        private readonly string root;
        private ILogger logger;


        public PhysicalFileSystem(string root) : this(root, null) {
        }
        public PhysicalFileSystem(string root, ILogger logger) {
            if (String.IsNullOrEmpty(root)) {
                throw new ArgumentException($"Argument cannot be null or empty", nameof(root));
            }
            this.root = root;
            this.logger = logger;
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

        public virtual IEnumerable<string> GetFiles(string path, string filter, bool recursive, bool notRelative = false) {
            path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
            if (String.IsNullOrEmpty(filter)) {
                filter = "*.*";
            }
            try {
                if (!Directory.Exists(path)) {
                    return Enumerable.Empty<string>();
                }
                var files = Directory.EnumerateFiles(path, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                return notRelative ? files : files.Select(MakeRelativePath);
            } catch (UnauthorizedAccessException) {

            } catch (DirectoryNotFoundException) {

            }

            return Enumerable.Empty<string>();
        }

        public virtual IEnumerable<string> GetDirectories(string path, bool recursive = false, bool notRelative = false) {
            try {
                path = PathUtility.EnsureTrailingSlash(GetFullPath(path));
                if (!Directory.Exists(path)) {
                    return Enumerable.Empty<string>();
                }
                var files = Directory.EnumerateDirectories(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                return notRelative ? files : files.Select(MakeRelativePath);
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

        public virtual Stream OpenFileForWrite(string path) {
            path = GetFullPath(path);
            return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
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
                string destinationDirectory = Path.GetDirectoryName(destFull);

                EnsureDirectory(destinationDirectory);

                File.Move(srcFull, destFull);
            } catch (IOException) {
                File.Delete(srcFull);
            }
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

        public string GetParent(string path) {
            return Directory.GetParent(path.TrimEnd(Path.DirectorySeparatorChar)).FullName;
        }

        private void CopyDirectoryInternal(string source, string destination, bool recursive) {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(source);

            if (!dir.Exists) {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + source);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!DirectoryExists(destination)) {
                EnsureDirectory(destination);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                string destinationPath = Path.Combine(destination, file.Name);

                FileInfo destFile = new FileInfo(destinationPath);
                if (destFile.Exists) {
                    if (destFile.IsReadOnly) { // Clear read-only
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
}