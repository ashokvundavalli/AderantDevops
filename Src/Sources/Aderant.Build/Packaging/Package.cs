using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Aderant.Build.Packaging {
    public interface IPackageDependency {

        
    }

    /// <summary>
    /// A receptacle in which packages may be stored and retrieved from
    /// </summary>
    public class MemoryPackageRepository {
        public void AddPackage(IPackage package) {

        }
    }

    public interface IPackage {

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        string Name { get; }

        IEnumerable<IPackageDependency> Dependencies { get; }
    }

    internal class Package : IPackage {
        private readonly string packageName;
        private readonly PackageSpecification packageSpecification;

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name {
            get { return packageName; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Package"/> class.
        /// </summary>
        /// <param name="packageName">Name of the package.</param>
        /// <param name="packageSpecification">The package specification.</param>
        public Package(string packageName, PackageSpecification packageSpecification) {
            this.packageName = packageName;
            this.packageSpecification = packageSpecification;
        }

        public void CreatePackageFile(string rootDirectory, Stream stream) {
            // We need to call dispose on ZipArchive before we can use it, which means passing 'true' as the third parameter to the ZipArchive so we can
            // still access the stream after disposing it.

            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                foreach (var file in packageSpecification.Files) {

                    string packageRelativeFile = MakePackageRelativeFile(rootDirectory, file);
                    ZipArchiveEntry entry = archive.CreateEntry(packageRelativeFile);

                    using (Stream entryStream = entry.Open()) {
                        using (Stream fileStream = file.GetStream()) {
                            fileStream.CopyTo(entryStream);
                        }
                    }
                }
            }
        }

        private static string MakePackageRelativeFile(string rootDirectory, IPackageFile file) {
            int i = file.FullPath.IndexOf(rootDirectory, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) {
                var packageRelativeFile = file.FullPath.Substring(i + rootDirectory.Length);

                return packageRelativeFile = packageRelativeFile.TrimStart(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            }

            return file.FullPath;
        }

        public IEnumerable<IPackageDependency> Dependencies { get; private set; }

      
    }
}