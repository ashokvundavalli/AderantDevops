﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;
using Aderant.Build.TeamFoundation;
using LibGit2Sharp;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public sealed class PublishArtifacts : ContextTaskBase {

        public string SolutionRoot { get; set; }

        public string[] RelativeFrom { get; set; }

        public ITaskItem[] Artifacts { get; set; }

        public string FileVersion { get; set; }

        public string AssemblyVersion { get; set; }

        public override bool Execute() {
            if (Artifacts != null) {
                var artifacts = MaterializeArtifactPackages();
                var artifactService = new ArtifactService(new PhysicalFileSystem(), new BucketService());

                artifactService.FileVersion = FileVersion;
                artifactService.AssemblyVersion = AssemblyVersion;

                var storageInfo = artifactService.PublishArtifacts(
                    Context,
                    SolutionRoot,
                    null,
                    artifacts);

                var commands = new TfBuildCommands(Logger);
                
                foreach (var item in storageInfo) {
                    commands.LinkArtifact(item.Name, TfBuildArtifactType.FilePath, item.FullPath);
                }
            }

            return !Log.HasLoggedErrors;
        }

        private List<ArtifactPackage> MaterializeArtifactPackages() {
            List<ArtifactPackage> artifacts = new List<ArtifactPackage>();
            var grouping = Artifacts.GroupBy(g => g.GetMetadata("ArtifactId"));

            foreach (var group in grouping) {
                List<PathSpec> pathSpecs = new List<PathSpec>();
                foreach (var file in group) {
                    var pathSpec = ArtifactPackage.CreatePathSpecification(
                        SolutionRoot,
                        RelativeFrom,
                        file.GetMetadata("FullPath"),
                        file.GetMetadata("TargetPath") // The destination location (assumed to be relative to "RelativeFrom")
                    );

                    if (!pathSpecs.Contains(pathSpec)) {
                        pathSpecs.Add(pathSpec);
                    }
                }

                var artifact = new ArtifactPackage(group.Key, pathSpecs);
                artifacts.Add(artifact);
            }

            return artifacts;
        }
    }

    internal class ArtifactService {
        private readonly BucketService bucketService;
        private readonly IFileSystem fileSystem;

        public ArtifactService(IFileSystem fileSystem, BucketService bucketService) {
            this.fileSystem = fileSystem;
            this.bucketService = bucketService;
        }

        public string FileVersion { get; set; }
        public string AssemblyVersion { get; set; }

        internal IReadOnlyCollection<ArtifactStorageInfo> PublishArtifacts(Context context, string solutionRoot, object cacheKey, IReadOnlyCollection<ArtifactPackage> packages) {
            // TODO: Slow - optimize
            // TODO: Test for duplicates in the artifact inputs

            List<Tuple<string, PathSpec>> copyList = new List<Tuple<string, PathSpec>>();
            List<ArtifactStorageInfo> storageInfoList = new List<ArtifactStorageInfo>();

            IEnumerable<IGrouping<string, ArtifactPackage>> grouping = packages.GroupBy(g => g.Id);
            foreach (var group in grouping) {
                string bucketId = bucketService.GetBucketId(solutionRoot);
                // \\dfs\artifacts\<name>\<bucket>\<build_id>
                foreach (var artifact in group) {
                    var container = Path.Combine(context.PrimaryDropLocation, group.Key, bucketId, !string.IsNullOrEmpty(context.BuildMetadata.BuildId) ? context.BuildMetadata.BuildId : "-1");
                    foreach (var pathSpec in artifact.GetFiles()) {
                        //CopyToDestination(container, pathSpec);
                    }
                    
                    if (context.BuildMetadata.IsPullRequest) {
                        storageInfoList.Add(PrepareFilesForPullRequestDrop(copyList, context, artifact.Id, artifact.GetFiles()));
                    } else {
                       storageInfoList.Add(PrepareFilesForLegacyDrop(copyList, context, artifact.Id, artifact.GetFiles()));
                    }
                }
            }

            foreach (var item in copyList) {
                CopyToDestination(item.Item1, item.Item2);
            }

            return storageInfoList;
        }

        private ArtifactStorageInfo PrepareFilesForPullRequestDrop(List<Tuple<string, PathSpec>> copyList, Context context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            ErrorUtilities.IsNotNull(context.PullRequestDropLocation, nameof(context.PullRequestDropLocation));
            ErrorUtilities.IsNotNull(context.BuildMetadata.PullRequest.Id, nameof(context.BuildMetadata.PullRequest.Id));
            ErrorUtilities.IsNotNull(artifactId, nameof(artifactId));
            ErrorUtilities.IsNotNull(AssemblyVersion, nameof(AssemblyVersion));
            ErrorUtilities.IsNotNull(FileVersion, nameof(FileVersion));

            // TODO: Temporary shim to allow PR layering to work. This should be replaced by the artifact service
            var destination = Path.Combine(context.PullRequestDropLocation, context.BuildMetadata.PullRequest.Id);
            string artifactName;
            destination = CreateDropLocationPath(destination, artifactId, out artifactName);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(destination, pathSpec));
            }

            return new ArtifactStorageInfo {
                FullPath = destination,
                Name = artifactName,
            };
        }

        private string CreateDropLocationPath(string destinationRoot, string artifactId, out string artifactName) {
            artifactName = Path.Combine(artifactId, AssemblyVersion, FileVersion, "Bin", "Module"); //TODO: Bin\Module is for compatibility only
            return Path.Combine(destinationRoot, artifactName);
        }

        private void CopyToDestination(string destinationRoot, PathSpec pathSpec) {
            var destination = Path.Combine(destinationRoot, pathSpec.Destination);

            if (fileSystem.FileExists(pathSpec.Location)) {
                fileSystem.CopyFile(pathSpec.Location, destination);
            }
        }

        private ArtifactStorageInfo PrepareFilesForLegacyDrop(List<Tuple<string, PathSpec>> copyList, Context context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            string artifactName;
            var destination = CreateDropLocationPath(context.PrimaryDropLocation, artifactId, out artifactName);

            foreach (var pathSpec in files) {
                copyList.Add(Tuple.Create(destination, pathSpec));
            }

            return new ArtifactStorageInfo {
                FullPath = destination,
                Name = artifactName,
            };
        }
    }

    internal class ArtifactStorageInfo {
        public string FullPath { get; set; }
        public string Name { get; set; }
    }

    internal class BucketService {
        public string GetBucketId(string path) {
            return GetCommitForSolutionRoot(path);
        }

        private static string GetCommitForSolutionRoot(string solutionRoot) {
            string discover = Repository.Discover(solutionRoot);

            using (var repo = new Repository(discover)) {
                // Covert the full path to the relative path within the repository
                int start = solutionRoot.IndexOf(repo.Info.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
                if (start >= 0) {
                    string relativePath = solutionRoot.Substring(start + repo.Info.WorkingDirectory.Length);

                    IEnumerable<LogEntry> logEntries = repo.Commits.QueryBy(relativePath);
                    var latestCommitForDirectory = logEntries.First();

                    return latestCommitForDirectory.Commit.Id.Sha;
                }

                return string.Empty;
            }
        }
    }

    internal class ArtifactPackage : IArtifact {
        private List<PathSpec> pathSpecs;

        public ArtifactPackage(string id, IEnumerable<PathSpec> pathSpecs) {
            Id = id;
            this.pathSpecs = pathSpecs.ToList();
        }

        public string Id { get; }

        public IReadOnlyCollection<IDependable> GetDependencies() {
            return null;
        }

        public void AddResolvedDependency(IUnresolvedDependency unresolvedDependency, IDependable dependable) {
        }

        public IReadOnlyCollection<PathSpec> GetFiles() {
            return pathSpecs;
        }

        public static PathSpec CreatePathSpecification(string solutionRoot, string[] trimPaths, string fullPath, string targetPath) {
            string outputRelativePath = null;

            foreach (var path in trimPaths) {
                if (fullPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
                    outputRelativePath = fullPath.Remove(0, path.Length);
                    break;
                }
            }

            if (outputRelativePath == null) {
                throw new InvalidOperationException(string.Format("Unable to construct an output relative path from {0} to {1}", solutionRoot, fullPath));
            }

            return new PathSpec(fullPath, Path.Combine(targetPath ?? string.Empty, outputRelativePath));
        }
    }

    internal struct PathSpec {

        public PathSpec(string location, string relativePath) {
            this.Location = location;
            this.Destination = relativePath;
        }

        public string Destination { get; }

        public string Location { get; }

        public override bool Equals(object obj) {
            if (!(obj is PathSpec)) {
                return false;
            }

            var spec = (PathSpec)obj;
            return Location == spec.Location && Destination == spec.Destination;
        }

        public override int GetHashCode() {
            var hashCode = -79747215;
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(Location);
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(Destination);
            return hashCode;
        }
    }

}
