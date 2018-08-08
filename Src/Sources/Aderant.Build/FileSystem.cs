using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Aderant.Build {
    public class FileSystem {
        /// <summary>
        /// Calculates the destination directory for a file
        /// </summary>
        /// <param name="fileToCopy">The file.</param>
        /// <returns></returns>
        internal delegate string FileDestinationSelector(FileInfo[] filesInDirectory, FileInfo fileToCopy);

        private static DirectoryOperations directory = new DirectoryOperations();

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
                    ClearReadOnly(destinationFile);
                    success = NativeMethods.DeleteFile(destinationFile);
                }
                if (!success) {
                    // sometimes throws a "Handle is not valid" error - don't know why (need to investigate)
                    var fileLinks = NativeMethods.GetFileSiblingHardLinks(destinationFile);
                    var randomOtherLink = fileLinks.FirstOrDefault(f => f.ToLowerInvariant() != destinationFile.ToLowerInvariant());
                    if (randomOtherLink != null) {
                        ClearReadOnly(destinationFile);
                        success = NativeMethods.DeleteFile(destinationFile);
                        SetReadOnly(randomOtherLink);
                    }
                }
                if (!success) {
                    throw new IOException(string.Format("Could not delete hard link {0}", destinationFile));
                }
            }
        }

        internal static void ClearReadOnly(string file) {
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

    public class DirectoryOperations {

        public virtual string[] GetFileSystemEntries(string path, string searchPattern) {
            return System.IO.Directory.GetFileSystemEntries(path, searchPattern);
        }

    }

}
