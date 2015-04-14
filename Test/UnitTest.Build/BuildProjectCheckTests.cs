using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class BuildProjectCheckTests {

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void When_project_file_references_unexpected_branch_exception_is_thrown() {
            var check = new BuildProjectCheck();

            string text = Resources.ProjectFileText1;

            check.CheckForInvalidBranch(text, "$(BranchName)");
        }
    }
}
