using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Samples {
    [TestClass]
    public class VssApiSample {
        [TestMethod]
        public void RelationshipSample() {
            var conn = new VssConnection(new Uri("http://tfs:8080/tfs/Aderant"), new VssCredentials());

            Task.Run(async () => {
                var client = conn.GetClient<WorkItemTrackingHttpClient>();

                var userstory = await client.GetWorkItemAsync(133912, null, null, WorkItemExpand.All);

                var patch = new JsonPatchDocument();

                patch.Add(new JsonPatchOperation {
                    Path = @"/relations/-",
                    Operation = Operation.Add,
                    Value = new WorkItemRelation {
                        Rel = "ArtifactLink",
                        Url = "vstfs:///Build/Build/647011",
                        Attributes = CreateAttributes()
                    }
                });

                await client.UpdateWorkItemAsync(patch, userstory.Id.Value);
                return Task.FromResult(false);
            }, CancellationToken.None).Wait();
        }

        private IDictionary<string, object> CreateAttributes() {
            return new Dictionary<string, object> {
                {"name", "Build"},
                {"comment", "Integrated in build"}
            };
        }
    }
}