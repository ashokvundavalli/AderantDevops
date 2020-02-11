using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityDataProviderIllegalPublicPropertiesTests : AderantCodeFixVerifier {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeQualityDataProviderIllegalPublicPropertiesTests" /> class.
        /// </summary>
        public CodeQualityDataProviderIllegalPublicPropertiesTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeQualityDataProviderIllegalPublicPropertiesTests"/> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public CodeQualityDataProviderIllegalPublicPropertiesTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

        #region Properties

        protected override RuleBase Rule => new CodeQualityDataProviderIllegalPublicPropertiesRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_EmptyMethod() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; set; }

        public string TestProperty { get; }

        public void TestMethod() {
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_Mixed() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; set; }

        public string TestProperty { get; }

        public string TestMethod() {
            var localTest1 = TestPropertySetter1;
            return localTest1 + TestProperty + TestPropertySetter2;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: [...] TestPropertySetter2;
                GetDiagnostic(16, 48));
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_Mixed_Parenthesized() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; set; }

        public string TestProperty { get; }

        public string TestMethod() {
            var localTest1 = ((TestPropertySetter1));
            return (((localTest1)) + (((TestProperty)) + ((TestPropertySetter2))));
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: [...] ((TestPropertySetter2));
                GetDiagnostic(16, 60));
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_Mixed_Private() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        private string TestPropertySetter2 { get; set; }

        public string TestProperty { get; }

        public string TestMethod() {
            var localTest1 = TestPropertySetter1;
            return localTest1 + TestProperty + TestPropertySetter2;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_Mixed_PrivateSetter() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; private set; }

        public string TestProperty { get; }

        public string TestMethod() {
            var localTest1 = TestPropertySetter1;
            return localTest1 + TestProperty + TestPropertySetter2;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_MultiMethod_MultiProperty() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; set; }

        public string TestMethod1() {
            return TestPropertySetter1 + TestPropertySetter2;
        }

        public string TestMethod2() {
            return TestPropertySetter1 + TestPropertySetter2;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: return TestPropertySetter1;
                GetDiagnostic(13, 20),
                // Error: [...] TestPropertySetter2;
                GetDiagnostic(13, 42),
                // Error: return TestPropertySetter1;
                GetDiagnostic(17, 20),
                // Error: [...] TestPropertySetter2;
                GetDiagnostic(17, 42));
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_MultiMethod_MultiProperty_WithValidMethod() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; set; }

        public string TestMethod1() {
            return TestPropertySetter1 + TestPropertySetter2;
        }

        public string TestMethod2() {
            return TestPropertySetter1 + TestPropertySetter2;
        }

        public string TestMethod3() {
            return string.Empty;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: return TestPropertySetter1;
                GetDiagnostic(13, 20),
                // Error: [...] TestPropertySetter2;
                GetDiagnostic(13, 42),
                // Error: return TestPropertySetter1;
                GetDiagnostic(17, 20),
                // Error: [...] TestPropertySetter2;
                GetDiagnostic(17, 42));
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_MultiMethod_MultiProperty_WithValidMethodAndProperty() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; set; }

        public string TestProperty { get; }

        public string TestMethod1() {
            return TestPropertySetter1 + TestPropertySetter2;
        }

        public string TestMethod2() {
            return TestPropertySetter1 + TestPropertySetter2;
        }

        public string TestMethod3() {
            return TestProperty;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: return TestPropertySetter1;
                GetDiagnostic(15, 20),
                // Error: [...] TestPropertySetter2;
                GetDiagnostic(15, 42),
                // Error: return TestPropertySetter1;
                GetDiagnostic(19, 20),
                // Error: [...] TestPropertySetter2;
                GetDiagnostic(19, 42));
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_MultiMethod_SingleProperty() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter { get; set; }

        public string TestMethod1() {
            return TestPropertySetter;
        }

        public string TestMethod2() {
            return TestPropertySetter;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: return TestPropertySetter;
                GetDiagnostic(11, 20),
                // Error: return TestPropertySetter;
                GetDiagnostic(15, 20));
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_NotDataProvider() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public string TestPropertySetter { get; set; }

        public string TestMethod() {
            return TestPropertySetter;
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_NotDataProvider_WithAdditionalAttribute() {
            const string code = @"
using System;

namespace Test {
    [TestAttribute]
    public class TestClass {
        public string TestPropertySetter { get; set; }

        public string TestMethod() {
            return TestPropertySetter;
        }
    }

    public class TestAttribute : Attribute {
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_NoMethods() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; set; }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_NoProperties() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestMethod() {
            return string.Empty;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_SingleMethod_MultiProperty() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistrationAttribute(typeof(int))]
    public class TestClass {
        public string TestPropertySetter1 { get; set; }

        public string TestPropertySetter2 { get; set; }

        public string TestMethod() {
            return TestPropertySetter1 + TestPropertySetter2;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: return TestPropertySetter1;
                GetDiagnostic(13, 20),
                // Error: [...] TestPropertySetter2;
                GetDiagnostic(13, 42));
        }

        [TestMethod]
        public void CodeQualityDataProviderIllegalPublicProperties_SingleMethod_SingleProperty() {
            const string code = @"
using System;
using Aderant.PresentationFramework.Windows.Data;

namespace Test {
    [DataProviderRegistration(typeof(int))]
    public class TestClass {
        public string TestPropertySetter { get; set; }

        public string TestMethod() {
            return TestPropertySetter;
        }
    }
}

namespace Aderant.PresentationFramework.Windows.Data {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class DataProviderRegistrationAttribute : Attribute {
        public DataProviderRegistrationAttribute(Type type) {
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: return TestPropertySetter;
                GetDiagnostic(11, 20));
        }

        #endregion Tests
    }
}
