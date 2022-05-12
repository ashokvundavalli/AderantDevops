﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Framework.Presentation.AppShell;

namespace UnitTest.Aderant.Build.Analyzer.Tests.MemberInitialization.StaticFieldFromInstanceConstructor {
    public partial class App : AppShellApplication {

        private static DialogService staticFieldFromInstanceConstructor;

        public App() {
            staticFieldFromInstanceConstructor = new DialogService();
        }

    }


    internal partial class DialogService {

    }
}

namespace Aderant.Framework.Presentation.AppShell {
    public partial class AppShellApplication {

    }
}