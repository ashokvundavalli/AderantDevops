using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.Packaging;
using Aderant.Build.Utilities;

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

        /// <summary>
        /// Convenient method for creating a stream for unit testing.
        /// </summary>
        internal static Stream ToStream(this string contents) {
            byte[] byteArray = Encoding.UTF8.GetBytes(contents);
            return new MemoryStream(byteArray);
        }

        /// <summary>
        /// Convenient method for replicating tree without copying. Delegates to <see cref="IFileSystem.BulkCopy"/>.
        /// </summary>
        public static ActionBlock<PathSpec> CopyDirectoryUsingLinks(this IFileSystem fileSystem, string source, string target) {
            ErrorUtilities.IsNotNull(source, nameof(source));
            ErrorUtilities.IsNotNull(target, nameof(target));

            source = PathUtility.EnsureTrailingSlash(source);
            target = PathUtility.EnsureTrailingSlash(target);

            IEnumerable<string> files = fileSystem.GetFiles(source);
            IEnumerable<PathSpec> destinationMap = files.Select(location => new PathSpec(
                location,
                location.Replace(source, target, StringComparison.OrdinalIgnoreCase), true));

            return fileSystem.BulkCopy(destinationMap, true, false, true);
        }
    }
}
