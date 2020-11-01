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
                DtdProcessing = DtdProcessing.Allow
            };
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        Allow,
        Ignore,
        Prohibit
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: DtdProcessing = DtdProcessing.Allow
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
                DtdProcessing = DtdProcessing.Allow
            };
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        Allow,
        Ignore,
        Prohibit
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: DtdProcessing = DtdProcessing.Allow
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
        Allow,
        Ignore,
        Prohibit
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(8, 13));
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Invalid_IndirectAssignment() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var value = DtdProcessing.Ignore;
            var item = new XmlReaderSettings();
            item.DtdProcessing = value;
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        Allow,
        Ignore,
        Prohibit
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(9, 13));
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Invalid_IncorrectNamespaceAssignment() {
            const string code = @"
using System.Fake;
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = new XmlReaderSettings();
            item.DtdProcessing = System.Fake.DtdProcessing.Allow;
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        Allow,
        Ignore,
        Prohibit
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}

namespace System.Fake {
    public enum DtdProcessing {
        Allow,
        Ignore,
        Prohibit
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(9, 13));
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Valid_Benign() {
            const string code = @"
namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = (new object());
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Valid_Ignore() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = new XmlReaderSettings();
            item.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        Allow,
        Ignore,
        Prohibit
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Valid_Prohibit() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = new XmlReaderSettings();
            item.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        Allow,
        Ignore,
        Prohibit
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void SecurityBanNewXmlReader_Valid_FullNameSpace() {
            const string code = @"
using System.Xml;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var item = new System.Xml.XmlReaderSettings();
            item.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
        }
    }
}

namespace System.Xml {
    public enum DtdProcessing {
        Allow,
        Ignore,
        Prohibit
    }

    public sealed class XmlReaderSettings {
        public DtdProcessing DtdProcessing { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
