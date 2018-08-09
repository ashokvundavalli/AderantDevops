using Aderant.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class PackageArtifactReaderTests {

        [TestMethod]
        public void Foo() {
            var reader = new PackageArtifactReader();
            reader.BuildEngine = new Moq.Mock<IBuildEngine>().Object;

            string xml = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>  
  <Target Name='PackageArtifacts'>
    <ItemGroup>
      <PackageArtifact Include='A'>
        <ArtifactId>A1</ArtifactId>
      </PackageArtifact>

      <PackageArtifact Include='B'>
        <ArtifactId>B1</ArtifactId>
      </PackageArtifact>
    </ItemGroup>
  </Target>

</Project>";

            reader.ProjectXml = xml;
            reader.Execute();

            Assert.IsNotNull(reader.ArtifactIds);
            CollectionAssert.Contains(reader.ArtifactIds, "A1");
            CollectionAssert.Contains(reader.ArtifactIds, "B1");
        }
    }
}
