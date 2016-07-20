using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Aderant.Build {
    public static class FileSystemExtensions {
        public static void WriteAllText(this IFileSystem fileSystem, string path, string contents, Encoding encoding) {
            using (Stream stream = fileSystem.CreateFile(path)) {
                using (StreamWriter writer = new StreamWriter(stream, encoding)) {
                    writer.Write(contents);
                }
            }
        }
    }

    public interface IFileSystem {
        void DeleteDirectory(string path, bool recursive);

        IEnumerable<string> GetFiles(string path, string filter, bool recursive);

        IEnumerable<string> GetDirectories(string path);

        void DeleteFile(string path);

        bool FileExists(string path);

        bool DirectoryExists(string path);

        void CreateDirectory(string path);

        Stream CreateFile(string path);

        Stream OpenFile(string path);
    }
}