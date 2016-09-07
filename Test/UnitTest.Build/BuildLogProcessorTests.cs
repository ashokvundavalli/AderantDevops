using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Tasks;
using Aderant.Build.Tasks.WarningProcess;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class BuildLogProcessorTests {

        [TestMethod]
        public void Can_delta_build_log() {
            var processor = new Aderant.Build.Tasks.WarningProcess.BuildLogProcessor();

            var result = processor.GetWarnings(
                new StringReader(BuildLogProcessor.Resources.buildlog_baseline),
                new StringReader(BuildLogProcessor.Resources.log_with_more_warnings));

            Assert.AreEqual(1, result.GetDifference().Count());
        }

        [TestMethod]
        public void Line_is_split_on_colon() {
            var actual = WarningReportBuilder.CreateLine(@"Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23): Warning CS1591: Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'");

            var expected = @"Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23)  
Warning CS1591 Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'";

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Line_is_not_changed_if_not_split() {
            var actual = WarningReportBuilder.CreateLine(@"Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23) Warning CS1591 Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'");

            var expected = @"Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23) Warning CS1591 Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'";

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CreateLine_is_null_safe() {
            var actual = WarningReportBuilder.CreateLine(null);
            Assert.IsNotNull(actual);
        }
    }
}
