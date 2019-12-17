using System;
using Aderant.Build.ProjectSystem.References;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class UnresolvedBuildDependencyProjectReferenceTests {

        [TestMethod]
        public void Moniker_sets_reference_properties() {
            var id = Guid.Parse("B3DC1129-A687-41FB-A9B0-B08AA64DAFB8");
            var projectPath = @"..\..\MyProject.csproj";
            var moniker = new UnresolvedP2PReferenceMoniker(projectPath, id);
            var reference = new UnresolvedBuildDependencyProjectReference(null, moniker);

            Assert.AreEqual("MyProject.csproj", reference.ProjectFileName);
            Assert.AreEqual(id, reference.ProjectGuid);
            Assert.AreEqual(projectPath, reference.ProjectPath);
            Assert.IsNull(reference.GetAssemblyName());

        }
    }
}