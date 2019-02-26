using System;
using System.Collections.Generic;
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
            get { return groveler.DirectoriesInBuild.ToArray(); }
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
            System.Diagnostics.Debugger.Launch();
            if (Context.Exclude != null) {
                ExcludedPaths = Context.Exclude.Union(ExcludedPaths ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                    .Except(Context.Include, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (ExcludedPaths != null) {
                ExcludedPaths = ExcludedPaths.Select(PathUtility.GetFullPath).ToArray();
                Log.LogMessage("Excluding paths: " + Environment.NewLine + string.Join(Environment.NewLine + "-> ", ExcludedPaths));
            }

            groveler = new DirectoryGroveler(new PhysicalFileSystem());
            groveler.Logger = new BuildTaskLogger(Log);

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

            return !Log.HasLoggedErrors;
        }

        private List<string> GetDirectoriesWithNoCachedBuild(string buildRoot, HashSet<string> contextInclude) {
            var buildStateMetadata = Context.BuildStateMetadata;
            var contextBuildMetadata = Context.SourceTreeMetadata;

            var pathsToAnalyze = new List<string>(contextInclude);

            List<BucketId> unassignedBuckets;
            if (buildStateMetadata != null) {
                buildStateMetadata.QueryCacheForBuckets(contextBuildMetadata.GetBuckets(), out unassignedBuckets);

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
