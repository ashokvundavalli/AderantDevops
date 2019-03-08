﻿using System;
using System.Threading;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace UnitTest.Build.DependencyAnalyzer {
    internal class FakeVisualStudioProject : IDependable {

        private static int i;
        private int id = Interlocked.Increment(ref i);

        public string Id {
            get { return id.ToString(); }
        }
    }

    internal class TestConfiguredProject : ConfiguredProject {
        private readonly Guid guid = Guid.NewGuid();
        internal string outputAssembly;

        public TestConfiguredProject(IProjectTree tree)
            : base(tree) {
        }

        public TestConfiguredProject(IProjectTree projectTree, Guid guid)
            : base(projectTree) {
            this.guid = guid;
        }

        protected override Lazy<Project> InitializeProject(Lazy<ProjectRootElement> projectElement) {
            return new Lazy<Project>(() => new Project());
        }

        public override string OutputAssembly {
            get { return outputAssembly; }
        }

        public override Guid ProjectGuid {
            get { return guid; }
        }
    }
}
