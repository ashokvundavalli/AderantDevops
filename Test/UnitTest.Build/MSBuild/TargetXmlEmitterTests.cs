using System.Linq;
using System.Xml.Linq;
using Aderant.Build.MSBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.MSBuild {

    [TestClass]
    public class TargetXmlEmitterTests {

        [TestMethod]
        public void WritesTargetChildren() {
            Target target = new Target("Foo");
            target.Add(new Message("Foo"));

            TargetXmlEmitter visitor = new TargetXmlEmitter();
            target.Accept(visitor);

            XElement document = visitor.GetXml();

            var expected = @"<Project ToolsVersion=""14.0"" DefaultTargets="""" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <!--Properties is a special element understood by the MS Build task and will associate the unique properties to each project-->
  <Target Name=""Foo"">
    <Message Text=""Foo"" />
  </Target>
</Project>";

            Assert.AreEqual(expected, document.ToString());
        }

        [TestMethod]
        public void DependsOnTargetWritesTarget() {
            Target parent = new Target("Foo");
            Target child = new Target("Bar");

            child.DependsOnTargets.Add(parent);

            TargetXmlEmitter visitor = new TargetXmlEmitter();
            child.Accept(visitor);

            XElement document = visitor.GetXml();

            var expected = @"<Project ToolsVersion=""14.0"" DefaultTargets="""" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <!--Properties is a special element understood by the MS Build task and will associate the unique properties to each project-->
  <Target Name=""Foo"" />
  <Target Name=""Bar"" DependsOnTargets=""Foo"" />
</Project>";

            Assert.AreEqual(expected, document.ToString());
        }
    }
}
