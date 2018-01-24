using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableMethodInvocationTests : IDisposableRuleBaseTests {
        #region Properties

        protected override RuleBase Rule => new IDisposableMethodInvocationRule();

        #endregion Properties

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
    public class IFactory {
        public static Factory Current { get; set; }

        public T CreateInstance<T>() where T : DisposeMe.DisposeMe, new() {
            return new T();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
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
