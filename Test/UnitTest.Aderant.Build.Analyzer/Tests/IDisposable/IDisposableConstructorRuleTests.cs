using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableConstructorRuleTests : IDisposableRuleBaseTests {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="IDisposableConstructorRuleTests" /> class.
        /// </summary>
        public IDisposableConstructorRuleTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IDisposableConstructorRuleTests" /> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public IDisposableConstructorRuleTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

        #region Properties

        protected override RuleBase Rule => new IDisposableConstructorRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void IDisposableConstructorRule_ConstructorParameter_Public_Disposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        public TestClass(int arg) {
            // Empty.
        }

        public TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            item.Dispose();
        }
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
        public void IDisposableConstructorRule_ConstructorParameter_Public_NotAssigned() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass(int arg) {
            // Empty.
        }

        public TestClass(IDisposable arg) {
            // Empty.
        }
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
        public void IDisposableConstructorRule_ConstructorParameter_Protected_Disposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        public TestClass(int arg) {
            // Empty.
        }

        protected TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            item.Dispose();
        }
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
        public void IDisposableConstructorRule_ConstructorParameter_Protected_NotAssigned() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass(int arg) {
            // Empty.
        }

        protected TestClass(IDisposable arg) {
            // Empty.
        }
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
        public void IDisposableConstructorRule_ConstructorParameter_Private_Disposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        public TestClass(int arg) {
            // Empty.
        }

        private TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            item.Dispose();
        }
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
        public void IDisposableConstructorRule_ConstructorParameter_Private_NotAssigned() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass(int arg) {
            // Empty.
        }

        private TestClass(IDisposable arg) {
            // Empty.
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: IDisposable arg;
                GetDiagnostic(10, 27, "arg"));
        }

        [TestMethod]
        public void IDisposableConstructorRule_ConstructorParameter_Internal_Disposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        public TestClass(int arg) {
            // Empty.
        }

        internal TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            item.Dispose();
        }
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
        public void IDisposableConstructorRule_ConstructorParameter_Internal_NotAssigned() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass(int arg) {
            // Empty.
        }

        internal TestClass(IDisposable arg) {
            // Empty.
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: IDisposable arg;
                GetDiagnostic(10, 28, "arg"));
        }

        [TestMethod]
        public void IDisposableConstructorRule_Initializer() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass()
            : this(new DisposeMe()) {
            // Empty.
        }

        internal TestClass(IDisposable arg) {
            // Empty.
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: IDisposable arg;
                GetDiagnostic(11, 28, "arg"));
        }

        [TestMethod]
        public void IDisposableConstructorRule_Initializer_MultiParam() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass()
            : this(new DisposeMe(), new DisposeMe()) {
            // Empty.
        }

        internal TestClass(IDisposable arg, IDisposable arg2) {
            // Empty.
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: IDisposable arg;
                GetDiagnostic(11, 28, "arg"),
                // Error: IDisposable arg2;
                GetDiagnostic(11, 45, "arg2"));
        }

        [TestMethod]
        public void IDisposableConstructorRule_Initializer_MultiParam_SingleError() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass()
            : this(new DisposeMe(), 0) {
            // Empty.
        }

        internal TestClass(IDisposable arg, int arg2) {
            // Empty.
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: IDisposable arg;
                GetDiagnostic(11, 28, "arg"));
        }

        [TestMethod]
        public void IDisposableConstructorRule_Initializer_MultiParam_SingleError_Public() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass(IDisposable arg)
            : this(arg, new DisposeMe()) {
            // Empty.
        }

        internal TestClass(IDisposable arg, IDisposable arg2) {
            // Empty.
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: IDisposable arg2;
                GetDiagnostic(11, 45, "arg2"));
        }

        [TestMethod]
        public void IDisposableConstructorRule_Initializer_Public() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass(IDisposable arg)
            : this(arg, 0) {
            // Empty.
        }

        internal TestClass(IDisposable arg, int i) {
            // Empty.
        }
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
        public void IDisposableConstructorRule_Initializer_LocalDispose() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass()
            : this(new DisposeMe()) {
            // Empty.
        }

        internal TestClass(IDisposable arg) {
            arg.Dispose();
        }
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
        public void IDisposableConstructorRule_Initializer_LocalVariable() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass()
            : this(new DisposeMe()) {
            // Empty.
        }

        internal TestClass(IDisposable arg) {
            var item = arg;

            item.Dispose();
        }
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
        public void IDisposableConstructorRule_Initializer_DisposeMethod() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        public TestClass()
            : this(new DisposeMe()) {
            // Empty.
        }

        internal TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            item?.Dispose();
        }
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
        public void IDisposableConstructorRule_Initializer_DisposeMethod_ThisSyntax() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        public TestClass()
            : this(new DisposeMe()) {
            // Empty.
        }

        internal TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            item?.Dispose();
        }
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
        public void IDisposableConstructorRule_Initializer_Assigned_Conditional() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public TestClass()
            : this(new DisposeMe()) {
            // Empty.
        }

        internal TestClass(IDisposable arg) {
            var item = true ? arg : null;
            item?.Dispose();
        }
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

        #endregion Tests
    }
}
