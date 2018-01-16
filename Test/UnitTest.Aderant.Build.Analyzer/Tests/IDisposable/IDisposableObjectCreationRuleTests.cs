using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableObjectCreationRuleTests : IDisposableRuleBaseTests {
        #region Properties

        protected override RuleBase Rule => new IDisposableObjectCreationRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void IDisposableObjectCreationRule_Orphan() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            new DisposeMe();
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
                // Error: new DisposeMe();
                GetDiagnostic(5, 13, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_Orphan_Property_Arrow() {
            const string code = @"
namespace Test {
    public class TestClass {
        public object Foo => new DisposeMe();
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
                // Error: new DisposeMe();
                GetDiagnostic(4, 30, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_Orphan_Property_Get() {
            const string code = @"
namespace Test {
    public class TestClass {
        public object Foo {
            get { return new DisposeMe(); }
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
                // Error: new DisposeMe();
                GetDiagnostic(5, 26, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_Orphan_Property_Get_Conditional() {
            const string code = @"
namespace Test {
    public class TestClass {
        public object Foo {
            get { return false ? new DisposeMe() : null; }
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
                // Error: new DisposeMe();
                GetDiagnostic(5, 34, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_Orphan_Property_Get_ConditionalMulti() {
            const string code = @"
namespace Test {
    public class TestClass {
        public object Foo {
            get { return false ? new DisposeMe() : new DisposeMe(); }
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
                // Error: new DisposeMe();
                GetDiagnostic(5, 34, "DisposeMe"),
                // Error: new DisposeMe();
                GetDiagnostic(5, 52, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_Orphan_CreationSyntax() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            new DisposeMe {
                // Empty.
            };
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
                // Error: new DisposeMe();
                GetDiagnostic(5, 13, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_Using() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            using (new DisposeMe()) {
                // Empty.
            }
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
        public void IDisposableObjectCreationRule_Return() {
            const string code = @"
namespace Test {
    public class TestClass {
        private DisposeMe Method() {
            return new DisposeMe();
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
        public void IDisposableObjectCreationRule_Return_CreationSyntax() {
            const string code = @"
namespace Test {
    public class TestClass {
        private DisposeMe Method() {
            return new DisposeMe {
                // Empty.
            };
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
        public void IDisposableObjectCreationRule_ReturnConditional() {
            const string code = @"
namespace Test {
    public class TestClass {
        private DisposeMe Method() {
            return true ? new DisposeMe() : true ? null : new DisposeMe();
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
        public void IDisposableObjectCreationRule_OverridenConstructor() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private readonly DisposeMe item;

        public TestClass()
            : this(new DisposeMe()) {
            // Empty.
        }

        public TestClass(DisposeMe disposeMe) {
            item = disposeMe;
        }

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
        public void IDisposableObjectCreationRule_NullConditionalOperator() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private readonly DisposeMe item;

        public TestClass()
            : this(new DisposeMe()) {
            // Empty.
        }

        public TestClass(DisposeMe disposeMe) {
            item = disposeMe ?? new DisposeMe();
        }

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
        public void IDisposableObjectCreationRule_Return_ObjectCreationAsMethodParameter() {
            const string code = @"
namespace Test {
    public class TestClass {
        private bool Method() {
            return OtherMethod(new DisposeMe());
        }

        private static bool OtherMethod(DisposeMe item) {
            return item != null;
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
                // Error: OtherMethod(new DisposeMe())
                GetDiagnostic(5, 32, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_InterfaceCoalesce() {
            const string code = @"using System;
namespace Test {
    public class TestClass{

        public IFoo TestMethod(bool input) {
            return input ? new FooDisposable() as IFoo : new FooNonDisposable();
        }
    }

    public interface IFoo {
    }

    public class FooDisposable : IFoo, IDisposable {
        public void Dispose() {
        }
    }

    public class FooNonDisposable : IFoo {
    }
}";

            VerifyCSharpDiagnostic(
                code,
                // Error: new FooDisposable()
                GetDiagnostic(6, 28, "FooDisposable"));
        }

        #endregion Tests
    }
}
