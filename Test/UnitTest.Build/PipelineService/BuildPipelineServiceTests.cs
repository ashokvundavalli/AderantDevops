using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.PipelineService;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.PipelineService {
    [TestClass]
    public class BuildPipelineServiceTests {
        [TestMethod]
        public void RecordRelatedFilesMergesDictionaries() {
            var service = new BuildPipelineServiceImpl();

            var relatedFiles1 = new Dictionary<string, List<string>> {
                { "Aderant.Query.dll", new List<string> { "Aderant.Query.Views.dll", "Aderant.Query.Services.dll" } },
                { "QueryService.svc", new List<string> { "WebQueryService.svc" } }
            };

            var relatedFiles2 = new Dictionary<string, List<string>> {
                { "Aderant.QueRy.dll", new List<string> { "ExpertQuery.SqlViews.sql" } },
                { "Global.asax", new List<string> { "Domain.htm" } }
            };

            var mergedFiles = new Dictionary<string, List<string>> {
                { "Aderant.Query.dll", new List<string> { "Aderant.Query.Views.dll", "Aderant.Query.Services.dll", "ExpertQuery.SqlViews.sql" } },
                { "QueryService.svc", new List<string> { "WebQueryService.svc" } },
                { "Global.asax", new List<string> { "Domain.htm" } }
            };

            service.RecordRelatedFiles(relatedFiles1);

            var expected = service.GetRelatedFiles();
            Assert.IsTrue(expected.Keys.SequenceEqual(relatedFiles1.Keys));
            Assert.IsTrue(expected.Values.SequenceEqual(relatedFiles1.Values));

            service.RecordRelatedFiles(relatedFiles2);

            expected = service.GetRelatedFiles();

            Assert.IsTrue(expected.Keys.OrderBy(s => s).SequenceEqual(mergedFiles.Keys.OrderBy(s => s)));

            foreach (var key in expected.Keys) {
                var values = expected[key];
                Assert.IsTrue(values.OrderBy(s => s).SequenceEqual(mergedFiles[key].OrderBy(s => s)));
            }
        }
    }
}
