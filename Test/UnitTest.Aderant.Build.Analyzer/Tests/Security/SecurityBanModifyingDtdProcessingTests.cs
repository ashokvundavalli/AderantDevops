using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.Security {
    [TestClass]
    public class SecurityBanModifyingDtdProcessingTests : AderantCodeFixVerifier {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityBanModifyingDtdProcessingTests"/> class.
        /// </summary>
        public SecurityBanModifyingDtdProcessingTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityBanModifyingDtdProcessingTests"/> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public SecurityBanModifyingDtdProcessingTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

        #region Properties

        protected override RuleBase Rule => new SecurityBanModifyingDtdProcessingRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void SecurityBanNewXmlReader_Invalid_ObjectInitializer() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = new XmlReaderSettings() {
                DtdProcessing = DtdProcessing.TestValue
            };
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        TestValue
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: DtdProcessing = DtdProcessing.TestValue
                GetDiagnostic(8, 17));
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Invalid_ObjectInitializer_Alternate() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = new XmlReaderSettings {
                DtdProcessing = DtdProcessing.TestValue
            };
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        TestValue
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: DtdProcessing = DtdProcessing.TestValue
                GetDiagnostic(8, 17));
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Invalid_PropertyAssignment() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = new XmlReaderSettings();
            item.DtdProcessing = DtdProcessing.TestValue;
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        TestValue
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: DtdProcessing = DtdProcessing.TestValue
                GetDiagnostic(8, 13));
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
