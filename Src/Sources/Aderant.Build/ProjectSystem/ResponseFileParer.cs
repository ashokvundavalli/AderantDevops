using System;
using System.IO;
using Aderant.Build.MSBuild;

namespace Aderant.Build.ProjectSystem {
    internal class ResponseFileParser {
        private static readonly char[] newLineArray = Environment.NewLine.ToCharArray();

        private readonly IFileSystem fileSystem;

        public ResponseFileParser(IFileSystem fileSystem) {
            this.fileSystem = fileSystem;
        }

        /// <summary>
        /// Parses a response file into a <see cref="PropertyList"/>
        /// </summary>
        public PropertyList ParseFile(string responseFile) {
            if (fileSystem.FileExists(responseFile)) {
                string propertiesText;
                using (StreamReader reader = new StreamReader(fileSystem.OpenFile(responseFile))) {
                    propertiesText = reader.ReadToEnd();
                }

                return ParseRspContent(propertiesText.Split(newLineArray, StringSplitOptions.None));
            }

            return null;
        }

        private PropertyList ParseRspContent(string[] responseFileContent) {
            PropertyList propertyList = new PropertyList();

            if (responseFileContent == null || responseFileContent.Length == 0) {
                return propertyList;
            }

            foreach (string line in responseFileContent) {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) {
                    continue;
                }

                if (line.IndexOf("/p:", StringComparison.OrdinalIgnoreCase) >= 0) {
                    string[] split = line.Replace("\"", "").Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);

                    propertyList.Add(split[0].Substring(3, split[0].Length - 3), split[1]);
                }
            }

            return propertyList;
        }

        /// <summary>
        /// Constructs the default path to a response file from a given root directory.
        /// </summary>
        public static string CreatePath(string solutionDirectoryPath) {
            return Path.Combine(solutionDirectoryPath, "Build", Path.ChangeExtension(WellKnownPaths.EntryPointFileName, "rsp"));
        }
    }
}