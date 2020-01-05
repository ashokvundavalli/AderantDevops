using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableObjectCreationRuleTests : IDisposableRuleBaseTests {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="IDisposableObjectCreationRuleTests" /> class.
        /// </summary>
        public IDisposableObjectCreationRuleTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IDisposableObjectCreationRuleTests" /> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public IDisposableObjectCreationRuleTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

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
        public void IDisposableObjectCreationRule_Return_Yield() {
            const string code = @"
using System;
using System.Collections.Generic;

namespace Test {
    public class TestClass {
        private IEnumerable<IDisposable> Method() {
            yield return (new DisposeMe());
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

        #region Conditional And Coalesce Expression Tests

        [TestMethod]
        public void IDisposableObjectCreationRule_NullCoalesce() {
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
        public void IDisposableObjectCreationRule_NullCoalesce_ReturnStatement() {
            const string code = @"using System;
namespace Test {
    public class TestClass{

        public IFoo TestMethod(FooDisposable input) {
            return input ?? new FooDisposable();
        }
    }

    public interface IFoo {
    }

    public class FooDisposable : IFoo, IDisposable {
        public void Dispose() {
        }
    }
}";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_NullCoalesce_LocalVariableAssignment() {
            const string code = @"using System;
namespace Test {
    public class TestClass{

        public void TestMethod(FooDisposable input) {
            var x = input ?? new FooDisposable();
            x.Dispose();
        }
    }

    public interface IFoo {
    }

    public class FooDisposable : IFoo, IDisposable {
        public void Dispose() {
        }
    }
}";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_NullCoalesce_ChainedCoalesce() {
            const string code = @"using System;
namespace Test {
    public class TestClass{

        public void TestMethod(FooDisposable input, FooDisposable input2) {
            var x = input ?? input2 ?? new FooDisposable();
            x.Dispose();
        }
    }

    public interface IFoo {
    }

    public class FooDisposable : IFoo, IDisposable {
        public void Dispose() {
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }


        [TestMethod]
        public void IDisposableObjectCreationRule_ConditionalExpression_InterfaceConditional() {
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

        [TestMethod]
        public void IDisposableObjectCreationRule_ConditionalExpression_LocalVariableAssignment() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method(bool useDisposable) {
            var disposable = bool ? new DisposeMe() : null;
            disposable?.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }

        public void DoNothing(){
            // Empty
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_ConditionalExpression_ChainedConditional() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method(bool useDisposable, bool nestedUseDisposable) {
            var x = useDisposable ? new DisposeMe() : nestedUseDisposable ? new DisposeMe() : null;
            x?.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }

        public void DoNothing() {
            // Do Nothing.
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_ConditionalExpression_ParenthesizedChainedConditional() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method(bool useDisposable, bool nestedUseDisposable) {
            var x = useDisposable ? new DisposeMe() : (nestedUseDisposable ? new DisposeMe() : null);
            x?.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }

        public void DoNothing() {
            // Do Nothing.
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_ConditionalExpression_ReturnStatement() {
            const string code = @"
namespace Test {
    public class TestClass {
        private DisposeMe Method(bool useDisposable) {
            return useDisposable ? new DisposeMe() : null;
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }

        public void DoNothing() {
            // Do Nothing.
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_ConditionalExpression_ThrowsErrorIfNotUsedForAssignment() {
            const string code = @"namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item = new DisposeMe().ReturnTrue() ? new DisposeMe() : null;
            item?.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }

        public bool ReturnTrue() {
            return true;
        }
    }
}";
            VerifyCSharpDiagnostic(code, GetDiagnostic(4, 30, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_ConditionalExpression_ThrowsErrorIfNotUsedForAssignmentIfParenthesized() {
            const string code = @"namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item = (new DisposeMe()).ReturnTrue() ? new DisposeMe() : null;
            item?.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }

        public bool ReturnTrue() {
            return true;
        }
    }
}";
            VerifyCSharpDiagnostic(code, GetDiagnostic(4, 31, "DisposeMe"));
        }

        [TestMethod]
        public void IDisposableObjectCreationRule_ConditionalAndCoalesce_RandomChainingLocalVariableAssignment() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method(bool useDisposable, bool nestedDispose, DisposeMe disposable1, DisposeMe disposable2) {
            var x = useDisposable
                ? (disposable1 ?? disposable2 ?? new DisposeMe())
                : nestedDispose ? disposable2 ?? new DisposeMe() : disposable1 ?? new DisposeMe();
            x.Dispose();
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

        #endregion Conditional And Coalesce Expression Tests

        #endregion Tests

        #region Tests: Base Class

        [TestMethod]
        public void IDisposableMethodInvocationRule_BaseClass_Field() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : TestClassBase {
        public void TestMethod() {
            field = new DisposeMe();
        }
    }

    public abstract class TestClassBase : IDisposable {
        public IDisposable field;

        public void Dispose() {
            field?.Dispose();
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
        public void IDisposableMethodInvocationRule_BaseClass_Property() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : TestClassBase {
        public void TestMethod() {
            Property = new DisposeMe();
        }
    }

    public abstract class TestClassBase : IDisposable {
        public IDisposable Property { get; set; }

        public void Dispose() {
            Property?.Dispose();
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

        #endregion Tests: Base Class
    }
}
