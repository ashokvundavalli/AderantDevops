using System.IO;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Packaging.NuGet;

namespace Aderant.Build.Packaging {
    internal class VersionTracker {
        public IFileSystem2 FileSystem { get; set; }

        public void RecordVersion(ExpertModule module, string packageDirectory) {
            foreach (string file in FileSystem.GetFiles(packageDirectory, "*.nuspec", true)) {
                string text;
                using (var stream = FileSystem.OpenFile(FileSystem.GetFullPath(file))) {
                    using (var reader = new StreamReader(stream)) {
                        text = reader.ReadToEnd();
                    }
                }

                NuspecSerializer.GetVersion(text);
            }
        }
    }
}