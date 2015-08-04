using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Aderant.Build.DependencyAnalyzer {
    public class WebModule : ExpertModule {

        private static readonly char[] directorySeparatorCharArray = new char[] { Path.DirectorySeparatorChar };

        /// <summary>
        /// Initializes a new instance of the <see cref="WebModule"/> class.
        /// </summary>
        /// <param name="element">The product manifest module element.</param>
        public WebModule(XElement element)
            : base(element) {
        }

        protected WebModule()
            : base() {

        }

        /// <summary>
        /// Convenience method. Extracts the zipped web module by name.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="moduleDependenciesDirectory">The module dependencies directory.</param>
        internal static void ExtractModule(string moduleName, string moduleDependenciesDirectory) {
            WebModule module = new WebModule { Name = moduleName };
            module.Deploy(moduleDependenciesDirectory);
        }

        public override void Deploy(string moduleDependenciesDirectory) {
            base.Deploy(moduleDependenciesDirectory);

            const string folderNameToExtract = "PackageTmp";

            string zipFile = Path.Combine(moduleDependenciesDirectory, Name + ".zip");

            if (!File.Exists(zipFile)) {
                throw new FileNotFoundException("Cannot locate zip: " + zipFile, zipFile);
            }

            ZipArchive zipArchive = new ZipArchive(File.Open(zipFile, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read, false);
            
            foreach (var entry in zipArchive.Entries) {
                int pos = entry.FullName.IndexOf(folderNameToExtract, StringComparison.OrdinalIgnoreCase);
                if (pos >= 0) {
                    // Replace /Content/PackageTmp/Foo with Foo (this is the path relative to the dependencies folder)
                    string relativePath = MakePathRelativeToDependenciesFolder(pos + folderNameToExtract.Length + 1, entry.FullName);
                    string target = null;
                    if (relativePath.EndsWith(".dll")) {
                        target = Path.Combine(moduleDependenciesDirectory, Path.GetFileName(relativePath));
                    } else {
                        target = Path.Combine(moduleDependenciesDirectory, Name, relativePath);
                    }

                    if (string.IsNullOrEmpty(entry.Name)) {
                        Directory.CreateDirectory(target);
                        FileSystem.ClearReadOnly(target);
                        continue;
                    }

                    entry.ExtractToFile(target, true);
                    FileSystem.ClearReadOnly(target);
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