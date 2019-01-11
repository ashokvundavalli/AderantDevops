using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Aderant.Build {
    public static class FileSystemExtensions {
        public static void WriteAllText(this IFileSystem fileSystem, string path, string content) {
            fileSystem.AddFile(path, stream => {
                using (var writer = new StreamWriter(stream)) {
                    writer.Write(content);
                }
            });
        }

        public static string ReadAllText(this IFileSystem fileSystem, string path) {
            using (var fs = fileSystem.OpenFile(path)) {
                using (var reader = new StreamReader(fs)) {
                    return reader.ReadToEnd();
                }
            }
        }

        public static string[] ReadAllLines(this IFileSystem fileSystem, string path) {
            List<String> lines = new List<String>();

            using (Stream fs = fileSystem.OpenFile(path)) {
                using (StreamReader reader = new StreamReader(fs)) {
                    string line;
                    while ((line = reader.ReadLine()) != null) {
                        lines.Add(line);
                    }
                }
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Creates a 40 character SHA1 hash of the given file.
        /// </summary>
        public static string ComputeSha1Hash(this IFileSystem fileSystem, string file) {
            using (Stream stream = fileSystem.OpenFile(file)) {
                using (var sha1 = SHA1.Create()) {
                    var computedHash = sha1.ComputeHash(stream);
                    return BitConverter.ToString(computedHash).Replace("-", string.Empty); // Yay allocations
                }
            }
        }
    }
}
