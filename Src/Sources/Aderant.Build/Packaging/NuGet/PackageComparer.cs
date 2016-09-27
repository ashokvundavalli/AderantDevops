using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Aderant.Build.Packaging.NuGet {
    internal class PackageComparer {
        private readonly IFileSystem2 fileSystem;
        private readonly Logging.ILogger logger;

        public PackageComparer(IFileSystem2 fileSystem, Logging.ILogger logger) {
            this.fileSystem = fileSystem;
            this.logger = logger;
        }

        public bool HasChanges(string existingPackageDirectory, string currentPackageDirectory) {
            if (!existingPackageDirectory.EndsWith("lib")) {
                existingPackageDirectory = Path.Combine(existingPackageDirectory, "lib");
            }

            if (!currentPackageDirectory.EndsWith("bin")) {
                currentPackageDirectory = Path.Combine(currentPackageDirectory, "bin");
            }

            logger.Info("Existing package directory: {0}. Current package directory: {1}", existingPackageDirectory, currentPackageDirectory);
            
            IEnumerable<string> currentContents = fileSystem.GetFiles(currentPackageDirectory, "*", true).ToList();
            IEnumerable<string> existingContents = fileSystem.GetFiles(existingPackageDirectory, "*", true).ToList();

            Dictionary<string, string> currentHashes = HashContents(currentContents);
            Dictionary<string, string> existingHashes = HashContents(existingContents);

            IEnumerable<string> difference = currentHashes.Values.Except(existingHashes.Values);

            foreach (var item in difference) {
                string myKey = currentHashes.FirstOrDefault(x => x.Value == item).Key;
                logger.Info("New or changed item found: {0}", myKey);
            }

            return difference.Any();
        }

        private Dictionary<string, string> HashContents(IEnumerable<string> files) {
            Dictionary<string, string> hashes = new Dictionary<string, string>();

            foreach (var file in files) {
                string hashValue;

                using (Stream stream = fileSystem.OpenFile(file)) {
                    using (var sha1 = new SHA1Managed()) {
                        byte[] hash = sha1.ComputeHash(stream);
                        hashValue = BitConverter.ToString(hash);
                    }
                }

                hashes[file] = hashValue;
            }

            return hashes;
        }
    }
}