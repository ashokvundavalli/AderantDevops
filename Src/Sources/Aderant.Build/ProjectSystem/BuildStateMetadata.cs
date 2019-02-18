using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    public class BuildStateMetadata {

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
                BuildStateFile stateFile = null;

                foreach (var file in BuildStateFiles) {
                    if (string.Equals(file.BucketId.Id, bucketId.Id, StringComparison.OrdinalIgnoreCase)) {
                        stateFile = file;
                        break;
                    }
                }

                if (stateFile != null) {
                    assignedBuckets.Add(stateFile);
                } else {
                    unassignedBuckets.Add(bucketId);
                }
            }

            return assignedBuckets;
        }
    }
}
