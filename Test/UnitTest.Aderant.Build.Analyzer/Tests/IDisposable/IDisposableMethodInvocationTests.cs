using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableMethodInvocationTests : IDisposableRuleBaseTests {
        #region Properties

        protected override RuleBase Rule => new IDisposableMethodInvocationRule();

        #endregion Properties

        #region Tests: Constructors

        [TestMethod]
        public void IDisposableMethodInvocationRule_ThisConstructor() {
            const string code = @"
using System;
using System.Collections.Generic;

namespace Test {
    public class Test : IDisposable {
        private readonly IDisposable item;

        public Test()
            : this(Factory.CreateDisposable()) {
            // Empty.
        }

        internal Test(IDisposable item) {
            this.item = item;
        }

        public void Dispose() {
            item?.Dispose();
        }
    }

    public static class Factory {
        public static IDisposable CreateDisposable() {
            return null;
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> items) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests: Constructors

        #region Tests: Factory

        [TestMethod]
        public void IDisposableMethodInvocationRule_Factory_Aderant() {
            const string code = @"
namespace Test {
    public class TestClass {
        public static void Method() {
            Aderant.Framework.Factories.Factory.Current.CreateInstance<DisposeMe.DisposeMe>();
        }
    }
}

namespace DisposeMe {
    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}

namespace Aderant.Framework.Factories {
    public class Factory {
        public static Factory Current { get; set; }

        public T CreateInstance<T>() where T : DisposeMe.DisposeMe, new() {
            return new T();
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: CreateInstance<DisposeMe.DisposeMe>()
                GetDiagnostic(5, 57));
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_Factory_LocalDisposed() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item;

            item = Factory.CreateDisposeMe();

            item.Dispose();
        }
    }

    public static class Factory {
        public static DisposeMe CreateDisposeMe() {
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
        public void IDisposableMethodInvocationRule_Factory_LocalDisposed_NullCheck() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item;

            item = Factory.CreateDisposeMe();

            item?.Dispose();
        }
    }

    public static class Factory {
        public static DisposeMe CreateDisposeMe() {
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
        public void IDisposableMethodInvocationRule_Factory_Using() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item;

            using (item = Factory.CreateDisposeMe()) {
                // Empty.
            }
        }
    }

    public static class Factory {
        public static DisposeMe CreateDisposeMe() {
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
        public void IDisposableMethodInvocationRule_Factory_Orphan() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            Factory.CreateDisposeMe();
        }
    }

    public static class Factory {
        public static DisposeMe CreateDisposeMe() {
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

            VerifyCSharpDiagnostic(
                code,
                // Error: Factory.CreateDisposeMe();
                GetDiagnostic(5, 21));
        }

        #endregion Tests: Factory

        #region Tests: Standard Method

        [TestMethod]
        public void IDisposableMethodInvocationRule_StandardMethod_LocalDisposed() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item;

            item = CreateDisposeMe();

            item.Dispose();
        }

        public static DisposeMe CreateDisposeMe() {
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
        public void IDisposableMethodInvocationRule_StandardMethod_Using() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item;

            using (item = CreateDisposeMe()) {
                // Empty.
            }
        }

        public static DisposeMe CreateDisposeMe() {
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
        public void IDisposableMethodInvocationRule_StandardMethod_Orphan() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            CreateDisposeMe();
        }

        public static DisposeMe CreateDisposeMe() {
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

            VerifyCSharpDiagnostic(
                code,
                // Error: CreateDisposeMe();
                GetDiagnostic(5, 13));
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_ChainedDisposal() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            CreateDisposeMe().Dispose();
        }

        public static DisposeMe CreateDisposeMe() {
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

        #endregion Tests: Standard Method

        #region Tests: Remove

        [TestMethod]
        public void IDisposableMethodInvocationRule_RemoveAll_Diagnostic() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly List<DisposeMe> items = new List<DisposeMe>();

        private void Method() {
            items.Add(new DisposeMe());
            items.RemoveAll(x => x != null);
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

            VerifyCSharpDiagnostic(
                code,
                // Error: items.RemoveAll(x => x != null);
                GetDiagnostic(11, 19));
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_RemoveAll_NoDiagnostic() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly List<DisposeMe> items = new List<DisposeMe>();

        private void Method() {
            items.Add(new DisposeMe());
            items.RemoveAllAndDispose(x => x != null);
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
        public void IDisposableMethodInvocationRule_RemoveAt_Diagnostic() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly List<DisposeMe> items = new List<DisposeMe>();

        private void Method() {
            items.Add(new DisposeMe());
            items.RemoveAt(0);
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

            VerifyCSharpDiagnostic(
                code,
                // Error: items.RemoveAt(0);
                GetDiagnostic(11, 19));
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_RemoveAt_NoDiagnostic() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly List<DisposeMe> items = new List<DisposeMe>();

        private void Method() {
            items.Add(new DisposeMe());
            items.RemoveAtAndDispose(0);
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
        public void IDisposableMethodInvocationRule_RemoveRange_Diagnostic() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly List<DisposeMe> items = new List<DisposeMe>();

        private void Method() {
            items.Add(new DisposeMe());
            items.RemoveRange(0, 2);
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

            VerifyCSharpDiagnostic(
                code,
                // Error: items.RemoveRange(0, 2);
                GetDiagnostic(11, 19));
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_RemoveRange_NoDiagnostic() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly List<DisposeMe> items = new List<DisposeMe>();

        private void Method() {
            items.Add(new DisposeMe());
            items.RemoveRangeAndDispose(0, 2);
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
        public void IDisposableMethodInvocationRule_Remove_Diagnostic() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly Dictionary<int, DisposeMe> items = new Dictionary<int, DisposeMe>();

        private void Method() {
            items.Add(0, new DisposeMe());
            items.Remove(0);
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

            VerifyCSharpDiagnostic(
                code,
                // Error: items.Remove(0);
                GetDiagnostic(11, 19));
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_Remove_NoDiagnostic() {
            const string code = @"
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly Dictionary<int, DisposeMe> items = new Dictionary<int, DisposeMe>();

        private void Method() {
            items.Add(0, new DisposeMe());
            items.RemoveAndDispose(0);
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

        #endregion Tests: Remove

        #region Tests: Workflow Dependency

        [TestMethod]
        public void IDisposableMethodInvocationRule_WorkflowDependency() {
            const string code = @"
using System;
using System.Activites;

namespace Test.Aderant.Things {
    public class TestClass {
        public void TestMethod() {
            var actContext = new AsyncCodeActivityContext();
            using (var dependencyObject = actContext.GetDependency(GetDisposeMe)) {
                // Empty.
            }
        }

        private static DisposeMe GetDisposeMe() {
            return null;
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Activites {
    public class ActivityContext {
        public T GetDependency<T>(Func<T> dependency) {
            return dependency.Invoke();
        }
    }

    public class CodeActivityContext : ActivityContext {
        // Empty.
    }

    public class AsyncCodeActivityContext : CodeActivityContext {
        // Empty.
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_WorkflowDependency_NoBaseClass() {
            const string code = @"
using System;
using System.Activites;

namespace Test.Aderant.Things {
    public class TestClass {
        public void TestMethod() {
            var actContext = new ActivityContext();
            using (var dependencyObject = actContext.GetDependency(GetDisposeMe)) {
                // Empty.
            }
        }

        private static DisposeMe GetDisposeMe() {
            return null;
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Activites {
    public class ActivityContext {
        public T GetDependency<T>(Func<T> dependency) {
            return dependency.Invoke();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests: Workflow Dependency

        #region Tests: IDisposable Collection

        [TestMethod]
        public void IDisposableMethodInvocationRule_Collection_Invocation_Add() {
            const string code = @"
using System;
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class Test {
        private void Method() {
            var items = new List<IDisposable>();
            items.Add(Factory.CreateDisposable());
            items.DisposeItems();
        }
    }

    public static class Factory {
        public static IDisposable CreateDisposable() {
            return null;
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> items) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_Collection_Invocation_AddRange() {
            const string code = @"
using System;
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class Test {
        private void Method() {
            var items = new List<IDisposable>();
            items.AddRange(Factory.CreateDisposables());
            items.DisposeItems();
        }
    }

    public static class Factory {
        public static IDisposable[] CreateDisposables() {
            return null;
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> items) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_Collection_Indirect_Add() {
            const string code = @"
using System;
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class Test {
        private void Method() {
            var items = new List<IDisposable>();

            var disposable = Factory.CreateDisposable();

            items.Add(disposable);
            items.DisposeItems();
        }
    }

    public static class Factory {
        public static IDisposable CreateDisposable() {
            return null;
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> items) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_Factory_Collection_Indirect_AddRange() {
            const string code = @"
using System;
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class Test {
        private void Method() {
            var items = new List<IDisposable>();

            var disposables = Factory.CreateDisposables();

            items.AddRange(disposables);
            items.DisposeItems();
        }
    }

    public static class Factory {
        public static IDisposable[] CreateDisposables() {
            return null;
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> items) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_Factory_Collection_InitializerSyntax() {
            const string code = @"
using System;
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class Test {
        private void Method() {
            var disposables = Factory.CreateDisposables();

            var items = new List<IDisposable> {
                disposables[0],
                disposables[1]
            };

            items.DisposeItems();
        }
    }

    public static class Factory {
        public static IDisposable[] CreateDisposables() {
            return null;
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> items) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableMethodInvocationRule_Factory_Collection_ParameterSyntax() {
            const string code = @"
using System;
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class Test {
        private void Method() {
            var disposables = Factory.CreateDisposables();

            var items = new List<IDisposable>(disposables);

            items.DisposeItems();
        }
    }

    public static class Factory {
        public static IDisposable[] CreateDisposables() {
            return null;
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> items) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests: IDisposable Collection

        #region Tests: Base Class

        [TestMethod]
        public void IDisposableMethodInvocationRule_BaseClass_Field() {
           const string code = @"
using System;

namespace Test {
    public class TestClass : TestClassBase {
        public void TestMethod() {
            field = CreateDisposable();
        }

        private static IDisposable CreateDisposable() {
            return null;
        }
    }

    public abstract class TestClassBase : IDisposable {
        public IDisposable field;

        public void Dispose() {
            field?.Dispose();
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
            Property = CreateDisposable();
        }

        private static IDisposable CreateDisposable() {
            return null;
        }
    }

    public abstract class TestClassBase : IDisposable {
        public IDisposable Property { get; set; }

        public void Dispose() {
            Property?.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests: Base Class

        #region Tests: Extension Methods

        [TestMethod]
        public void IDisposableMethodInvocationRule_ExtensionMethod() {
            // This test confirms that extension methods performed on
            // collections of IDisposable items do not raise diagnostics,
            // as long as the collection itself is disposed.
            // This test also confirms that variables assigned values from extension methods
            // from collections of IDisposable items are not flagged as 'require disposal'.
            const string code = @"
using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : IDisposable {
        private List<DisposeMe> Items { get; set; }

        public void TestMethod() {
            Items = new List<DisposeMe>(new DisposeMe[] { new DisposeMe() });
            Items.DisposeItems();
            Items = new List<DisposeMe>(new[] { new DisposeMe() });

            var unused = (Items.FirstOrDefault() ?? Items[0]);
            unused = (Items.FirstOrDefault() ?? Items[0]);
        }

        public void Dispose() {
            Items?.DisposeItems();
        }
    }

    public sealed class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> enumerable) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests: Extension Methods

        #region Tests: UI Automation

        [TestMethod]
        public void IDisposableMethodInvocationRule_UIAutomation() {
            const string code = @"
using System;
using System.Collections.Generic;
using Expert.Billing.UIAutomation;
using TestStack.White.Utility;
using TestStack.White.UIItems.WindowItems;

namespace Test {
    public class TestClass : IDisposable {
        private Window MainWindow {
            get {
                ExpertBilling.MainWindow =
                    Retry.For(
                        () =>
                            ExpertBilling
                                .GetAllOpenWindows()
                                .Find(window => window.Title.Contains(""Test"")),
                        TimeSpan.FromSeconds(5));
                return ExpertBilling.MainWindow;
            }
        }

        public void Dispose() {
            MainWindow.Dispose();
        }
    }
}

namespace Expert.Billing.UIAutomation {
    public class ExpertBilling : IDisposable {
        public static Window MainWindow { get; set; }

        public static List<Window> GetAllOpenWindows() {
            return null;
        }

        public void Dispose() {
            MainWindow.Dispose();
        }
    }
}

namespace TestStack.White.UIItems.WindowItems {
    public abstract class Window : IDisposable {
        public virtual string Title { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace TestStack.White.Utility {
    public static class Retry {
        public static T For<T>(
            Func<T> getMethod,
            TimeSpan retryFor,
            TimeSpan? retryInterval = null) {
            return getMethod();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests: UI Automation
    }
}
