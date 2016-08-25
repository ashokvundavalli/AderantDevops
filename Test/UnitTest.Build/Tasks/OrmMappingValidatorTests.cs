using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Utilities;

namespace UnitTest.Build.Tasks {

    [TestClass]
    public class OrmMappingValidatorTests {
        [TestMethod]
        public void When_template_is_none_type_and_missing_embedded_resource_returns_false() {
            OrmMappingValidator v = new OrmMappingValidator();
            v.BuildEngine = new Moq.Mock<IBuildEngine>().Object;

            v.None = new[] { new TaskItem("Resources\\OrmMappingDefinitions.tt") };

            Assert.IsFalse(v.Execute());
        }

        [TestMethod]
        public void When_template_is_content_type_and_missing_embedded_resource_returns_false() {
            OrmMappingValidator v = new OrmMappingValidator();
            v.BuildEngine = new Moq.Mock<IBuildEngine>().Object;

            v.Content = new[] { new TaskItem("Resources\\OrmMappingDefinitions.tt") };

            Assert.IsFalse(v.Execute());
        }

        [TestMethod]
        public void Invalid_hbm_extension_returns_false() {
            OrmMappingValidator v = new OrmMappingValidator();
            v.BuildEngine = new Moq.Mock<IBuildEngine>().Object;

            v.Compile = new[] { new TaskItem("Resources\\OrmMappingDefinitions.cs") };

            Assert.IsFalse(v.Execute());
        }

        [TestMethod]
        public void When_valid_result_is_true() {
            OrmMappingValidator v = new OrmMappingValidator();
            v.BuildEngine = new Moq.Mock<IBuildEngine>().Object;

            v.Content = new[] { new TaskItem("Resources\\OrmMappingDefinitions.tt") };
            v.EmbeddedResource = new[] { new TaskItem("Resources\\OrmMappingDefinitions.hbm.xml") };

            Assert.IsTrue(v.Execute());
        }

        [TestMethod]
        public void Is_test_project() {
            OrmMappingValidator v = new OrmMappingValidator();
            v.BuildEngine = new Moq.Mock<IBuildEngine>().Object;

            v.ProjectTypeGuids = new[] { new TaskItem("{3AC096D0-A1C2-E12C-1390-A8335801FDAB}"), new TaskItem("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"),  };

            v.Execute();

            Assert.IsTrue(v.IsTestProject);
        }
    }
}