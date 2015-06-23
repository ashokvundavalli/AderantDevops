using System;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class SourceIndexStreamTests {
        [TestMethod]
        public void Simple_path_to_file_is_added_to_source_stream() {
            string srcSrvStream1 = Resources.SrcSrvStream1;

            var indexStream = SourceIndexStream.ModifySourceIndexStream(srcSrvStream1);

            Assert.IsTrue(indexStream.Contains(@"c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.CheckRequest\Src\Aderant.CheckRequest.Presentation\ViewModels\NewVendor.cs*VSTFSSERVER*/ExpertSuite/Dev/Framework/Modules/Libraries.CheckRequest/Src/Aderant.CheckRequest.Presentation/ViewModels/NewVendor.cs*252821*NewVendor.cs*581d17f7e526f2db6120ce5fed4becd1"));
        }

        [TestMethod]
        public void TFS_EXTRACT_TARGET_is_updated() {
            string srcSrvStream1 = Resources.SrcSrvStream1;

            var indexStream = SourceIndexStream.ModifySourceIndexStream(srcSrvStream1);

            Assert.IsTrue(indexStream.Contains(@"TFS_EXTRACT_TARGET=%targ%\%var2%\%var6%\%var4%\%fnfile%(%var5%)"));
        }
    }
}