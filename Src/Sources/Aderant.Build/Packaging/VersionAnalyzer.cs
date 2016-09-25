using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using Aderant.Build.Versioning;

namespace Aderant.Build.Packaging {
    internal class VersionAnalyzer {
        private ILogger logger;
        private readonly IFileSystem2 fileSystem;

        public VersionAnalyzer(ILogger logger, IFileSystem2 fileSystem) {
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        public FileVersionAnalyzer Analyzer {
            get;
            set;
        }

        public Version Execute(string directory) {
            if (Analyzer == null) {
                throw new ArgumentNullException(nameof(Analyzer));
            }

            Dictionary<string, Version> versions = new Dictionary<string, Version>();

            ExecuteInternal(directory, versions);

            var versionPair = versions.Where(v => v.Value != null).OrderByDescending(v => v.Value).FirstOrDefault();

            if (versionPair.Key == null && versionPair.Value == null) {
                logger.Warning("Auto versioning could not determine a for {0}. Assuming 1.0.0", directory);
                return new Version(1, 0, 0);
            }

            if (versionPair.Key != null && versionPair.Value.Major == 0 && versionPair.Value.Minor == 0) {
                logger.Warning("Auto versioning could not determine a for {0}. Assuming 1.0.0", directory);
                return new Version(1, 0, 0);
            }
            return versionPair.Value;
        }

        internal void ExecuteInternal(string directory, Dictionary<string, Version> versions) {
            // version analyzer setup
            Analyzer.OpenFile = fileSystem.OpenFile;

            foreach (var file in fileSystem.GetFiles(directory, "*", true)) {
                FileVersionDescriptor version = Analyzer.GetVersion(fileSystem.GetFullPath(file));

                if (version != null) {
                    var fileVersion = new Version();

                    if (version.FileVersion != null) {
                        Version.TryParse(version.FileVersion, out fileVersion);
                    } else if (version.AssemblyVersion != null) {
                        fileVersion = version.AssemblyVersion;
                    }

                    //if (fileVersion != null && fileVersion > moduleVersion) {
                    //    moduleVersion = fileVersion;
                    //}

                    //info = " * " + Path.GetFileName(file) + " - " + version.FileVersion + " (" + version.AssemblyVersion + ")";
                    //this.logger.Info(info);

                    versions[file] = fileVersion;
                }

                //// strip version to 3 relevant fileVersion
                //var major = Math.Max(0, fileVersion.Major);
                //var minor = Math.Max(0, fileVersion.Minor);
                //var build = Math.Max(0, fileVersion.Build);

                //moduleVersion = new Version(major, minor, build, 0);
                //if (moduleVersion.Build > 999) {
                //    moduleVersion = new Version(major, minor, 0, 0);
                //}
            }

            IEnumerable<string> directories = fileSystem.GetDirectories(directory);
            foreach (var dir in directories) {
                ExecuteInternal(dir, versions);
            }
        }
    }
}