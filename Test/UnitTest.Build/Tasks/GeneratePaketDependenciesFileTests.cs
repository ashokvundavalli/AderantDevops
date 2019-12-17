using System;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Aderant.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class GeneratePaketDependenciesFileTests {
        [TestMethod]
        public void GeneratePaketDependenciesContentTest() {
            XDocument expertManifestContent = XDocument.Parse(Resources.ReferenceExpertManifest);

            ExpertManifest expertManifest = new ExpertManifest(expertManifestContent);

            TaskItem taskItem1 = new TaskItem("Aderant.Deployment.Internal");
            taskItem1.SetMetadata("ReferenceRequirement", "Aderant.Deployment.Core");

            TaskItem taskItem2 = new TaskItem("Aderant.Database.Backup");
            taskItem2.SetMetadata("ReferenceRequirement", "Aderant.Libraries.Models");

            string manifestContent = GeneratePaketDependencies.GeneratePaketDependenciesContent(expertManifest, new ITaskItem2[] { taskItem1, taskItem2 });
            Assert.IsTrue(manifestContent.Contains("Aderant.Deployment.Internal >= 12.0.0 build"));
            Assert.IsTrue(manifestContent.Contains("Aderant.Database.Backup 13.0.0-build4978"));
        }
    }
}
