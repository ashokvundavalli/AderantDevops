using System.IO;

namespace Aderant.Build {
    public static class FileSystemExtensions {
        public static void WriteAllText(this IFileSystem2 fileSystem, string path, string content) {
            fileSystem.AddFile(path, stream => {
                using (var writer = new StreamWriter(stream)) {
                    writer.Write(content);
                }
            });
        }

        public static string ReadAllText(this IFileSystem2 fileSystem, string path) {
            using (var fs = fileSystem.OpenFile(path)) {
                using (var reader = new StreamReader(fs)) {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
