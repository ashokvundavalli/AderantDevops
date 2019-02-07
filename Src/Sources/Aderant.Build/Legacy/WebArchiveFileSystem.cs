using System;
using System.IO;
using System.IO.Compression;

namespace Aderant.Build {
    internal class WebArchiveFileSystem : PhysicalFileSystem {
        private static readonly char[] directorySeparatorCharArray = new char[] { Path.DirectorySeparatorChar };

        const string FolderNameToExtract = "PackageTmp";

        public WebArchiveFileSystem(string root)
            : base(root) {
        }

        public void ExtractArchive(string archive, string moduleDependenciesDirectory) {
            string zipFile = archive;

            if (!this.FileExists(zipFile)) {
                throw new FileNotFoundException("Cannot locate zip: " + zipFile);
            }



            string name = Path.Combine(moduleDependenciesDirectory, Path.GetFileNameWithoutExtension(zipFile));

            ZipArchive zipArchive = new ZipArchive(File.Open(zipFile, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read, false);
            foreach (var entry in zipArchive.Entries) {
                int pos = entry.FullName.IndexOf(FolderNameToExtract, StringComparison.OrdinalIgnoreCase);
                if (pos >= 0) {
                    // Replace /Content/PackageTmp/Foo with Foo (this is the path relative to the dependencies folder)
                    string relativePath = MakePathRelativeToDependenciesFolder(pos + FolderNameToExtract.Length + 1, entry.FullName);

                    string target;

                    if (relativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                        target = Path.Combine(moduleDependenciesDirectory, Path.GetFileName(relativePath));
                    } else {
                        target = Path.Combine(moduleDependenciesDirectory, name, relativePath);
                    }

                    if (string.IsNullOrEmpty(entry.Name)) {
                        EnsureDirectory(target);
                        continue;
                    }

                    entry.ExtractToFile(target, true);
                }
            }

            zipArchive.Dispose();
        }

        private string MakePathRelativeToDependenciesFolder(int i, string zipEntryFullName) {
            return zipEntryFullName.Substring(i)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(directorySeparatorCharArray);
        }
    }
}