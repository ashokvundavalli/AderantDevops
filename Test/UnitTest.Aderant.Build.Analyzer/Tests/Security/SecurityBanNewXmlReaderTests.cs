using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.Security {
    [TestClass]
    public class SecurityBanNewXmlReaderTests : AderantCodeFixVerifier<SecurityBanNewXmlReaderRule> {

        #region Tests

        [TestMethod]
        public void SecurityBanNewXmlReader_Invalid() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = (new XmlTextReader());
        }
    }
}

namespace System.Xml {
    public class XmlTextReader {
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: new XmlTextReader()
                GetDiagnostic(7, 25));
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Valid() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = (new object());
        }
    }
}

namespace System.Xml {
    public class XmlTextReader {
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
