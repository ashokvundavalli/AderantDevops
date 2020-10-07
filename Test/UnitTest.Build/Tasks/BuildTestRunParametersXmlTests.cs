using System.Collections;
using System.Collections.Generic;
using Aderant.Build;
using Aderant.Build.PipelineService;
using Aderant.Build.Tasks.Testing;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class BuildTestRunParametersXmlTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Global_parameters_do_not_override_local() {
            var settings = new TaskItem("/RunSettings/TestRunParameters", new Hashtable {
                {
                    "Value",
                    @"<![CDATA[
<Parameter name=""X"" value=""Y"" />
]]>"
                }
            });

            var task = new BuildTestRunParametersXml();
            task.BuildEngine = new Moq.Mock<IBuildEngine>().Object;

            var context = new BuildOperationContext();
            context.ScopedVariables[BuildTestRunParametersXml.TestRunParametersNodeName] = new Dictionary<string, string> {
                {"MyParameter1", "Foo"},
                {"MyParameter2", "Bar"}
            };

            var mock = new Mock<IBuildPipelineService>();
            mock.Setup(s => s.GetContext()).Returns(context);
            task.Service = mock.Object;
            task.TestRunParameters = new[] {settings};

            task.ExecuteTask();

            Assert.AreEqual(1, task.TestRunParameters.Length);
            var expected = @"<Parameter name=""MyParameter1"" value=""Foo"" />
<Parameter name=""MyParameter2"" value=""Bar"" />
<![CDATA[
<Parameter name=""X"" value=""Y"" />
]]>
";

            Assert.AreEqual(expected, task.TestRunParameters[0].GetMetadata("Value"));
        }
    }
}
