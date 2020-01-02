using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableClassRuleTests : IDisposableRuleBaseTests {
        #region Properties

        protected override RuleBase Rule => new IDisposableClassRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void IDisposableClassRule_Disposable_Field() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private DisposeMe item;

        public void Dispose() {
            item.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableClassRule_Disposable_FieldAndProperty() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private DisposeMe item;
        private DisposeMe Property { get; set; }

        public void Dispose() {
            item.Dispose();
            Property.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableClassRule_Disposable_Property() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private DisposeMe Property { get; set; }

        public void Dispose() {
            Property.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableClassRule_NotDisposable_Field() {
            const string code = @"
namespace Test {
    public class TestClass {
        private DisposeMe item;

        private void Method() {
            item.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: TestClass
                GetDiagnostic(3, 18, "TestClass"));
        }

        [TestMethod]
        public void IDisposableClassRule_NotDisposable_FieldAndProperty() {
            const string code = @"
namespace Test {
    public class TestClass {
        private DisposeMe item;
        private DisposeMe Property { get; set; }

        private void Method() {
            item.Dispose();
            Property.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: TestClass
                GetDiagnostic(3, 18, "TestClass"));
        }

        [TestMethod]
        public void IDisposableClassRule_NotDisposable_Property() {
            const string code = @"
namespace Test {
    public class TestClass {
        private DisposeMe Property { get; set; }

        private void Method() {
            Property.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: TestClass
                GetDiagnostic(3, 18, "TestClass"));
        }

        [TestMethod]
        public void IDisposableClassRule_WhitelistedType() {
            const string code = @"
namespace Aderant.Billing.Services {
    public class BillingService {
        public DisposeMe DisposeMe { get; set; }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableClassRule_Collection() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly ICollection<DisposeMe> items = new List<DisposeMe>();

        private void Method() {
            items.Add(new DisposeMe());
            items.Add(new DisposeMe());
        }

        public void Dispose() {
            items.DisposeItems();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableClassRule_Dictionary() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly Dictionary<int, DisposeMe> items = new Dictionary<int, DisposeMe>();

        private void Method() {
            items.Add(0, new DisposeMe());
            items.Add(1, new DisposeMe());
        }

        public void Dispose() {
            items.DisposeItems();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableClassRule_Disposable_Field_AssignedFromConstructorParam() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private IDisposable item;

        public TestClass(IDisposable parm) {
            (item) = (parm);
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableClassRule_Disposable_Field_AssignedFromConstructorParam_AssignedInMethod() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private IDisposable item;

        public TestClass(IDisposable parm) {
            (item) = (parm);
        }

        public void TestMethod(IDisposable parm) {
            (item) = (parm);
        }

        public void Dispose() {
            item?.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
