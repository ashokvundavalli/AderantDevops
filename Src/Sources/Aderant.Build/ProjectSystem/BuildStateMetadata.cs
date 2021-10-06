using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    public sealed class BuildStateMetadata {

        [DataMember]
        public IReadOnlyCollection<BuildStateFile> BuildStateFiles { get; set; }

        /// <summary>
        /// Queries the cache for the given buckets and returns the assignment
        /// </summary>
        public IList<BuildStateFile> QueryCacheForBuckets(IReadOnlyCollection<BucketId> bucketIds, out List<BucketId> unassignedBuckets) {
            unassignedBuckets = new List<BucketId>();
            var assignedBuckets = new List<BuildStateFile>();

            if (BuildStateFiles == null) {
                return assignedBuckets;
            }

            foreach (var bucketId in bucketIds) {
                var stateFiles = BuildStateFiles.Where(x => string.Equals(x.BucketId.Id, bucketId.Id, StringComparison.OrdinalIgnoreCase)).ToList();

                if (stateFiles.Any()) {
                    assignedBuckets.AddRange(stateFiles);
                } else {
                    unassignedBuckets.Add(bucketId);
                }
            }

            return assignedBuckets.OrderBy(s => s.BucketId.Tag).ThenByDescending(s => s, BuildStateFileComparer.Default).ToList();
        }

        public void RemoveStateFilesForRoots(IEnumerable<string> roots) {
            var stateFiles = BuildStateFiles?.ToList();

            if (stateFiles != null) {
                stateFiles.RemoveAll(stateFile => roots.Contains(stateFile.BucketId.Tag, StringComparer.OrdinalIgnoreCase));

                stateFiles.Sort(BuildStateFileComparer.Default);

                BuildStateFiles = stateFiles;
            }
        }
    }
}
