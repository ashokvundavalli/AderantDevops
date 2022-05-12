using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Framework.Presentation.AppShell;

namespace UnitTest.Aderant.Build.Analyzer.Tests.MemberInitialization.DelegateTypeFieldFromInstanceConstructor {
    public partial class App : AppShellApplication {

        private readonly EventHandler<object> startupInvoker;

        public App() {
            startupInvoker += (sender, o) => { };
        }

    }
}

namespace Aderant.Framework.Presentation.AppShell {
    public partial class AppShellApplication {

    }
}