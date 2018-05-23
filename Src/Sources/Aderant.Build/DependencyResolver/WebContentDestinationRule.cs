using System;
using System.IO;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.DependencyResolver {
    internal class WebContentDestinationRule {
        private static readonly char[] directorySeparatorCharArray = new[] { Path.DirectorySeparatorChar };
        private readonly IDependencyRequirement dependency;
        private readonly string moduleDependenciesDirectory;

        public WebContentDestinationRule(IDependencyRequirement dependency, string moduleDependenciesDirectory) {
            this.dependency = dependency;
            this.moduleDependenciesDirectory = moduleDependenciesDirectory;
        }

        public string GetDestinationForFile(FileInfo[] files, FileInfo file) {
            // If this is web module content item it needs to go to Modules\Web.Expenses\Dependencies\ThirdParty.Foo
            string fileName = file.FullName;

            if (CreateInThirdPartyFolder(fileName)) {
                int pos = fileName.IndexOf(moduleDependenciesDirectory, StringComparison.OrdinalIgnoreCase);

                if (pos >= 0) {
                    string relativeDirectory = fileName.Substring(pos + moduleDependenciesDirectory.Length).TrimStart(directorySeparatorCharArray);
                    string destination = Path.Combine(moduleDependenciesDirectory, dependency.Name, relativeDirectory);

                    return destination;
                }
            }

            // Otherwise go to Modules\Web.Expenses\Dependencies
            return file.FullName;
        }

        private static bool CreateInThirdPartyFolder(string file) {
            string extension = Path.GetExtension(file).ToLowerInvariant();

            switch (extension) {
                case ".js":
                case ".ts":
                case ".css":
                case ".less":
                case ".png":
                case ".jpg":
                case ".gif":
                case ".svg":
                case ".ttf":
                case ".woff":
                case ".eot":
                    return true;
            }

            return false;
        }
    }
}