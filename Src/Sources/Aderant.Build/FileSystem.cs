using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace Aderant.Build {
    internal class FileSystem {
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
        public DirectoryOperations Directory {
            get { return directory; }
            internal set { directory = value; }
        }

        /// <summary>
        /// Gets or sets the file operations.
        /// </summary>
        /// <value>
        /// The file.
        /// </value>
        public FileOperations File {
            get { return file; }
            internal set { file = value; }
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

                if (useHardLinks) {
                    //Need to delete any existing file first otherwise the create hard link won't bother doing anything.
                    NativeMethods.DeleteFile(destinationFile);
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

        public static void ClearReadOnly(string file) {
            FileAttributes attributes = System.IO.File.GetAttributes(file);
            System.IO.File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
        }
    }

    internal class FileOperations {

        public virtual bool Exists(string file) {
            return File.Exists(file);
        }
    }

    internal class DirectoryOperations {
        public virtual string[] GetDirectories(string path) {
            return System.IO.Directory.GetDirectories(path);
        }

        public virtual string[] GetFileSystemEntries(string path) {
            return System.IO.Directory.GetFileSystemEntries(path);
        }

        public virtual bool Exists(string path) {
            return System.IO.Directory.Exists(path);
        }
    }

    internal static class NativeMethods {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateHardLink(string newFileName, string exitingFileName, IntPtr securityAttributes);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool DeleteFile(string fileName);
    }
}