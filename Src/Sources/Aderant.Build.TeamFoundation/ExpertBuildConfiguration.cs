using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Aderant.Build {
    public sealed class ExpertBuildConfiguration {
        private string moduleName;
        private string teamProject;
        private string buildInfrastructurePath;
        private string sourceControlPathToModule;

        private BranchName branchName;
        private string dropLocation;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertBuildConfiguration"/> class.
        /// </summary>
        /// <param name="branchName">Name of the branch.</param>
        public ExpertBuildConfiguration(string branchName) {
            this.branchName = new BranchName(branchName);
        }

        /// <summary>
        /// Gets or sets the team project e.g. ExpertSuite
        /// </summary>
        /// <value>
        /// The team project.
        /// </value>
        public string TeamProject {
            get { return teamProject; }
            set {
                teamProject = value;
                BuildSourceControlPaths();
            }
        }

        /// <summary>
        /// Gets or sets the name of the module.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public string ModuleName {
            get { return moduleName; }
            set {
                moduleName = value;
                BuildSourceControlPaths();
            }
        }

        /// <summary>
        /// Gets or sets the source control path to module.
        /// e.g  $/ExpertSuite/Dev/Branch/Modules/Foo
        /// </summary>
        /// <value>
        /// The server path to module.
        /// </value>
        public string SourceControlPathToModule {
            get { return sourceControlPathToModule; }
            set { sourceControlPathToModule = value; }
        }

        public string BuildName {
            get { return string.Concat(branchName.Name, ".", ModuleName); }
        }

        public string DropLocation {
            get { return dropLocation; }
            set {
                if (value != null) {
                    value = value.TrimEnd(Path.DirectorySeparatorChar);
                }

                dropLocation = value;
            }
        }

        /// <summary>
        /// Builds the drop location for module eg \\na.aderant.com\ExpertSuite\[branch]\[module]
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">No DropLocation specified</exception>
        /// <exception cref="System.InvalidOperationException">Cannot set the ModuleName component of the DropLocation</exception>
        public string BuildDropLocationForModule() {
            if (string.IsNullOrEmpty(DropLocation)) {
                throw new ArgumentNullException("No DropLocation specified");
            }

            List<string> parts = new List<string>();
            parts.Add(DropLocation);

            string branchFolder = string.Join("\\", branchName.NameParts);
            if (parts[0].IndexOf(branchFolder, StringComparison.OrdinalIgnoreCase) == -1) {
                // Only add the branch parts if it isn't already in the drop location
                foreach (string namePart in branchName.NameParts) {
                    parts.Add(namePart);
                }
            }

            if (!string.IsNullOrEmpty(ModuleName)) {
                parts.Add(ModuleName);
            } else {
                throw new InvalidOperationException("Cannot set the ModuleName component of the DropLocation");
            }

            return string.Join("\\", parts);
        }

        private void BuildServerPathToFolder(ref string property, string folder) {
            if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(TeamProject)) {
                if (string.IsNullOrEmpty(property)) {
                    List<string> parts = new List<string>();
                    parts.Add("$");
                    parts.Add(teamProject);
                    parts.AddRange(branchName.NameParts);
                    parts.Add(Constants.ModulesDirectory);
                    parts.Add(folder);

                    property = string.Join("/", parts);
                }
            }
        }

        private void BuildSourceControlPaths() {
            BuildServerPathToFolder(ref sourceControlPathToModule, ModuleName);
            BuildServerPathToFolder(ref buildInfrastructurePath, Constants.BuildInfrastructureDirectory);
        }


        internal struct BranchName {
            private string branchName;
            private string[] branchNameParts;

            /// <summary>
            /// Initializes a new instance of the <see cref="BranchName"/> struct.
            /// </summary>
            /// <param name="branchName">Name of the branch.</param>
            public BranchName(string branchName) {
                this.branchNameParts = branchName.Split(new char[] { '.', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                this.branchName = branchName.TrimEnd(new char[] { '\\' });
                this.branchName = branchName.Replace("\\", ".");
            }

            public string Name {
                get { return branchName; }
            }

            public string[] NameParts {
                get { return branchNameParts.ToArray(); }
            }
        }
    }
}
