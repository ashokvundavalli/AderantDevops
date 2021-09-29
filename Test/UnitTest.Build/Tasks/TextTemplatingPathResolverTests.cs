using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Tasks.TextTemplating;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class TextTemplatingPathResolverTests {

        [TestMethod]
        public void Can_construct_visual_studio_instance() {
            var visualStudioInstance = TextTemplatingPathResolver.Create2015Install("C:\\SomeDirectory");
        }

    }
}