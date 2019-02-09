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

            List<string> inputDirectories = ExpandInputDirectories(Context.BuildRoot, Context.Include);
            groveler.Grovel(inputDirectories, ExcludedPaths);

            if (!Context.Switches.RestrictToProvidedPaths) {
                groveler.ExpandBuildTree(PipelineService);
            }

            return !Log.HasLoggedErrors;
        }

        private List<string> ExpandInputDirectories(string buildRoot, string[] contextInclude) {
            var buildStateMetadata = Context.BuildStateMetadata;
            var contextBuildMetadata = Context.SourceTreeMetadata;

            var pathsToAnalyze = new List<string>(contextInclude);

            List<BucketId> unassignedBuckets;
            if (buildStateMetadata != null) {
                buildStateMetadata.QueryCacheForBuckets(contextBuildMetadata.GetBuckets(), out unassignedBuckets);

                if (ExcludedPaths != null) {
                    var filters = ExcludedPaths.Select(s => s.TrimTrailingSlashes()).ToList();
                    unassignedBuckets = unassignedBuckets.Where(s => !PathUtility.IsPathExcludedByFilters(s.Tag, filters)).ToList();

                    var message = string.Join(Environment.NewLine + "-> ", unassignedBuckets.Select(s => s.Tag));
                    Log.LogMessage(MessageImportance.High, $"There are no cached builds for these directories {message}. They will be added to this build.");
                }

                foreach (var id in unassignedBuckets) {
                    pathsToAnalyze.Add(Path.Combine(buildRoot, id.Tag));
                }
            }

            return pathsToAnalyze;
        }
    }
}