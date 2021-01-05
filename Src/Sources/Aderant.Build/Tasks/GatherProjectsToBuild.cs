using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.VersionControl;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class GatherProjectsToBuild : BuildOperationContextTask {
        private DirectoryGroveler groveler;

        public string[] ExcludedPaths { get; set; }

        [Output]
        public string[] DirectoriesInBuild {
            get {
                var paths = groveler.DirectoriesInBuild.ToArray();

                foreach (string path in paths) {
                    ErrorUtilities.VerifyThrowArgument(!path.EndsWith(PathUtility.DirectorySeparator), "The path {0} must not end with a directory separator.", path);
                }

                return paths;
            }
        }

        [Output]
        public string[] ExtensibilityFiles {
            get { return groveler.ExtensibilityFiles.ToArray(); }
        }

        [Output]
        public string[] DirectoryMakeFiles {
            get { return groveler.MakeFiles.ToArray(); }
        }

        [Output]
        public string[] ProjectFiles {
            get { return groveler.ProjectFiles.ToArray(); }
        }

        public override bool ExecuteTask() {
            if (Context.Exclude.Length > 0) {
                ExcludedPaths = Context.Exclude.Union(ExcludedPaths ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                    .Except(Context.Include, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (ExcludedPaths != null) {
                ExcludedPaths = ExcludedPaths.Select(PathUtility.GetFullPath).ToArray();
                Log.LogMessage("Excluding paths: " + Environment.NewLine + string.Join(Environment.NewLine + "-> ", ExcludedPaths));
            }

            groveler = new DirectoryGroveler(new PhysicalFileSystem()) {
                Logger = new BuildTaskLogger(Log)
            };

            HashSet<string> inputDirectories = new HashSet<string>(Context.Include, StringComparer.OrdinalIgnoreCase);
            if (!Context.Switches.RestrictToProvidedPaths) {
                GetDirectoriesWithNoCachedBuild(Context.BuildRoot, inputDirectories).ForEach(s => inputDirectories.Add(s));
            } else {
                Log.LogMessage("Build will not expand build tree.");
            }

            groveler.AddDirectoryInBuild(inputDirectories);

            if (!Context.Switches.RestrictToProvidedPaths) {
                groveler.ExpandBuildTree(PipelineService, inputDirectories);
            }

            groveler.Grovel(DirectoriesInBuild, ExcludedPaths);

            ValidatePaths();

            return !Log.HasLoggedErrors;
        }

        private void ValidatePaths() {
            List<string> missingDirectories = new List<string>();

            foreach (string directory in DirectoriesInBuild) {
                if (!Directory.Exists(directory)) {
                    missingDirectories.Add(directory);
                }
            }

            if (missingDirectories.Any()) {
                Log.LogError($"Build {(missingDirectories.Count == 1 ? "directory does not have a physical path" : "directories do not have physical paths")}:\n{string.Join("\n", missingDirectories)}");
            }
        }

        private List<string> GetDirectoriesWithNoCachedBuild(string buildRoot, HashSet<string> contextInclude) {
            var buildStateMetadata = Context.BuildStateMetadata;
            var contextBuildMetadata = Context.SourceTreeMetadata;

            var pathsToAnalyze = new List<string>(contextInclude);

            List<BucketId> unassignedBuckets;
            if (buildStateMetadata != null) {
                buildStateMetadata.QueryCacheForBuckets(contextBuildMetadata.GetBuckets(BucketKind.PreviousCommit), out unassignedBuckets);

                if (ExcludedPaths != null) {
                    var filters = ExcludedPaths.Select(s => s.TrimTrailingSlashes()).ToList();
                    unassignedBuckets = unassignedBuckets.Where(s => !PathUtility.IsPathExcludedByFilters(s.Tag, filters)).ToList();

                    var message = string.Join(Environment.NewLine, unassignedBuckets.Select(s => "-> " + s.Tag.PadRight(80) + " Hash: " + s.Id));
                    Log.LogMessage(MessageImportance.High, "There are no cached builds for these directories. They will be added to this build." + Environment.NewLine + message);
                }

                foreach (var id in unassignedBuckets) {
                    pathsToAnalyze.Add(Path.Combine(buildRoot, id.Tag));
                }
            }

            return pathsToAnalyze;
        }
    }
}
