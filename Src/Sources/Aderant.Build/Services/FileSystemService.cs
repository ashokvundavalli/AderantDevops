using System.ComponentModel.Composition;
using System.IO;

namespace Aderant.Build.Services {
    [Export(typeof(FileSystemService))]
    [Export(typeof(IFileSystem))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class FileSystemService : FlexService, IFileSystem {

        [ImportingConstructor]
        public FileSystemService([Import]Context context) {
        }

        internal string GetDirectoryNameOfFileAbove(string startingDirectory, string fileName, string[] ceilingDirectories = null) {
            // Canonicalize our starting location
            string lookInDirectory = Path.GetFullPath(startingDirectory);

            do {
                // Construct the path that we will use to test against
                string possibleFileDirectory = Path.Combine(lookInDirectory, fileName);

                // If we successfully locate the file in the directory that we're
                // looking in, simply return that location. Otherwise we'll
                // keep moving up the tree.
                if (File.Exists(possibleFileDirectory)) {
                    // We've found the file, return the directory we found it in
                    return lookInDirectory;
                } else {
                    // GetDirectoryName will return null when we reach the root
                    // terminating our search
                    lookInDirectory = Path.GetDirectoryName(lookInDirectory);
                }
            } while (lookInDirectory != null);

            // When we didn't find the location, then return an empty string
            return string.Empty;
        }
    }

    internal interface IFileSystem {
    }
}