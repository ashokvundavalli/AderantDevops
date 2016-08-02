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

namespace IntegrationTest.Build {
    [TestClass]
    public class VssApiSample {
        [TestMethod]
        public void Foo() {
            var conn = new VssConnection(new Uri("http://tfs:8080/tfs/Aderant"), new VssCredentials());

            Task.Run(async () => {
                var client = conn.GetClient<WorkItemTrackingHttpClient>();

                //var task = await client.GetWorkItemAsync(112126);
                var userstory = await client.GetWorkItemAsync(133912, null, null, WorkItemExpand.Relations);
                //var bug = await client.GetWorkItemAsync(123065);

                var patch = new JsonPatchDocument {
                    
                };

                patch.Add(new JsonPatchOperation {
                    Path = @"/relations/-",
                    Operation = Operation.Add,
                    Value = new WorkItemRelation {
                        Rel = "ArtifactLink",
                        Url = "vstfs:///Build/Build/647011",
                        Attributes = CreateComment(),
                    }
                });

                await client.UpdateWorkItemAsync(patch, userstory.Id.Value);

                var serializeObject = JsonConvert.SerializeObject(userstory);
                var patchDoc = JsonConvert.SerializeObject(patch);

                return Task.FromResult(false);
            }, CancellationToken.None).Wait();
        }

        private IDictionary<string, object> CreateComment() {
            return new Dictionary<string, object> {{"comment", "Integrated in build"}};
        }
    }
}