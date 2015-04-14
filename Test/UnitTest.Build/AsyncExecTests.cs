using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class AsyncExecTests {

        string command = @"\""Powershell\"" -noprofile \""C:\tfs\ExpertSuite\Dev\Framework\Modules\Build.Infrastructure\Src\Build\CopyToDrop.ps1\"" -moduleName Libraries.Models -moduleRootPath C:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Models\ -dropRootUNCPath C:\Temp\Out\\Libraries.Models\ -assemblyFileVersion  -suppressUniqueCheck";

        [TestMethod]
        public void AsyncTask_extracts_tool_from_commandline() {
            var exec = new AsyncExec();
            exec.Command = command;

            string toolName = exec.GetToolName();

            Assert.AreEqual("Powershell", toolName);
        }

        [TestMethod]
        public void AsyncTask_extracts_tool_from_commandline2() {
            var exec = new AsyncExec();
            exec.Command = command;

            var result = AsyncExec.GetCommandArguments(command);

            Assert.AreEqual(@"-noprofile \""C:\tfs\ExpertSuite\Dev\Framework\Modules\Build.Infrastructure\Src\Build\CopyToDrop.ps1\"" -moduleName Libraries.Models -moduleRootPath C:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Models\ -dropRootUNCPath C:\Temp\Out\\Libraries.Models\ -assemblyFileVersion  -suppressUniqueCheck", result);
        }
    }
}
