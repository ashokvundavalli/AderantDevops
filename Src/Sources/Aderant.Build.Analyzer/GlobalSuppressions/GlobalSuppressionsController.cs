using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aderant.Build.Analyzer.GlobalSuppressions {
    /// <summary>
    /// Responsible for handling manipulation of GlobalSuppressions.cs files during builds.
    /// Handles the sanitization and organization of affected files,
    /// with the intent of keeping changesets as small as possible.
    /// </summary>
    public static class GlobalSuppressionsController {
        private const string aderantGeneratedSuppression =
            "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Aderant.GeneratedSuppression\", \"";

        private const string aderantSyntax =
            "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Aderant.Syntax\", \"";

        private const string completeSuppressionAttribute =
            "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(";

        private const string incompleteSuppressionAttribute =
            "[assembly: SuppressMessage(";

        private const string suppressionsProjectContent =
            "<Compile Include=\"GlobalSuppressions.cs\" />";

        private static readonly bool isAutomaticSuppressionEnabled;

        /// <summary>
        /// Initializes the <see cref="GlobalSuppressionsController"/> class.
        /// </summary>
        static GlobalSuppressionsController() {
            string value = Environment.GetEnvironmentVariable("AderantRoslynRuleAutomaticSuppression");

            if (string.IsNullOrWhiteSpace(value)) {
                isAutomaticSuppressionEnabled = false;
                return;
            }

            isAutomaticSuppressionEnabled =
                bool.TryParse(value, out isAutomaticSuppressionEnabled) &&
                isAutomaticSuppressionEnabled;
        }

        /// <summary>
        /// Gets a value indicating whether automatic suppression is enabled.
        /// </summary>
        public static bool IsAutomaticSuppressionEnabled => isAutomaticSuppressionEnabled;

        /// <summary>
        /// Cleans the specified 'GlobalSuppressions.cs' files.
        /// This removes all 'Automated Suppression' messages.
        /// </summary>
        /// <param name="rootDirectoryPath">The root directory path.</param>
        public static void CleanFiles(string rootDirectoryPath) {
            if (rootDirectoryPath == null) {
                throw new ArgumentNullException(nameof(rootDirectoryPath));
            }

            var filePaths = new List<string>(500);
            GetFilePaths(rootDirectoryPath, ref filePaths);

            Parallel.ForEach(filePaths, CleanFile);
        }

        /// <summary>
        /// Cleans the specified 'GlobalSuppressions.cs' file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        private static void CleanFile(string filePath) {
            var contents = File.ReadAllLines(filePath);

            // Cleans up 'using' statements,
            // replacing each suppression attribute with the long-form name.
            for (int i = 0; i < contents.Length; ++i) {
                if (contents[i].StartsWith(incompleteSuppressionAttribute)) {
                    contents[i] = contents[i].Replace(
                        incompleteSuppressionAttribute,
                        completeSuppressionAttribute);
                }
            }

            // Removes all lines from suppression files that are not suppression messages.
            // Also removes all auto-generated Roslyn Rule suppression messages.
            // The above occurs under the expection that auto-suppression will be re-run,
            // which will reinstate any necessary automatic suppressions,
            // thus ensuring unused cruft is removed.
            var cleanContents = contents
                .Where(line =>
                    line.StartsWith(completeSuppressionAttribute) &&
                    !line.StartsWith(aderantGeneratedSuppression) &&
                    !line.StartsWith(aderantSyntax))
                .OrderBy(line => line);

            WriteToFile(filePath, cleanContents, Encoding.UTF8);
        }

        /// <summary>
        /// Organizes the specified 'GlobalSuppressions.cs' files.
        /// </summary>
        /// <param name="rootDirectoryPath">The root directory path.</param>
        public static void OrganizeFiles(string rootDirectoryPath) {
            if (rootDirectoryPath == null) {
                throw new ArgumentNullException(nameof(rootDirectoryPath));
            }

            var filePaths = new List<string>(500);
            GetFilePaths(rootDirectoryPath, ref filePaths);

            Parallel.ForEach(filePaths, OrganizeFile);
        }

        /// <summary>
        /// Organizes the contents of the specified 'GlobalSuppressions.cs' file,
        /// or removes the file if it is empty.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        private static void OrganizeFile(string filePath) {
            var contents = File.ReadAllLines(filePath);

            if (contents.Length < 1 ||
                contents.All(string.IsNullOrWhiteSpace)) {
                RemoveFileFromProject(filePath);
                return;
            }

            var orderedContents = contents
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .OrderBy(line => line);

            WriteToFile(filePath, orderedContents, Encoding.UTF8);
        }

        /// <summary>
        /// Removes the specified 'GlobalSuppressions.cs' file file from the associated projects.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        private static void RemoveFileFromProject(string filePath) {
            string directory = new FileInfo(filePath)
                .Directory?
                .FullName;

            if (string.IsNullOrWhiteSpace(directory)) {
                return;
            }

            var projectFiles = Directory.EnumerateFiles(
                directory,
                "*.csproj",
                SearchOption.TopDirectoryOnly);

            foreach (string file in projectFiles) {
                var contents = File
                    .ReadAllLines(file)
                    .Where(line => !line.Contains(suppressionsProjectContent));

                WriteToFile(file, contents, Encoding.UTF8);
            }

            File.Delete(filePath);
        }

        /// <summary>
        /// Gets the file paths of all 'GlobalSuppressions.cs' files in all child directories.
        /// </summary>
        /// <param name="directoryPath">The directory path.</param>
        /// <param name="filePaths">The file paths.</param>
        private static void GetFilePaths(string directoryPath, ref List<string> filePaths) {
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                return;
            }

            var directories = Directory
                .EnumerateDirectories(
                    directoryPath,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Select(path => new DirectoryInfo(path))
                .Where(info => !info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                .Select(info => info.FullName);

            foreach (string path in directories) {
                GetFilePaths(path, ref filePaths);
            }

            filePaths.AddRange(
                Directory.EnumerateFiles(
                    directoryPath,
                    "GlobalSuppressions.cs",
                    SearchOption.TopDirectoryOnly));
        }

        /// <summary>
        /// Writes the specified content to the specified file,
        /// overwriting whatever is already present.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="contents">The contents.</param>
        /// <param name="encoding">The encoding.</param>
        private static void WriteToFile(
            string filePath,
            IEnumerable<string> contents,
            Encoding encoding) {
            var contentsList = contents.ToList();

            using (var file = File.Create(filePath)) {
                using (var writer = new StreamWriter(file, encoding)) {
                    for (int i = 0; i < contentsList.Count; ++i) {
                        string line = contentsList[i];

                        if (i != contentsList.Count - 1) {
                            line += Environment.NewLine;
                        }

                        writer.Write(line);
                    }
                }
            }
        }
    }
}
