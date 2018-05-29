using System;
using System.Collections.Generic;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Tasks.BuildTime.ProjectDependencyAnalyzer;
using Aderant.Build.Tasks.BuildTime.Sequencer;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace UnitTest.Build.DependencyAnalyzer {

    [TestClass]
    public class BuildSequencerTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void MarkDirtyTest() {


            HashSet<string> dirtyProjects = new HashSet<string> {"ASS1"};

            var p1 = new VisualStudioProject(null, Guid.Empty, "ASS1", null, null);
            p1.IsDirty = true;
            var p2 = new VisualStudioProject(null, Guid.Empty, "ASS2", null, null);
            p2.IsDirty = false;

            var m1 = new VisualStudioProject(null, Guid.Empty, "MOD1", null, null);
            m1.DependsOn.Add(p1);
            var m2 = new VisualStudioProject(null, Guid.Empty, "MOD2", null, null);
            m2.DependsOn.Add(m1);
            var m3 = new VisualStudioProject(null, Guid.Empty, "MOD3", null, null);
            m3.DependsOn.Add(p2);

            var projectList = new List<IDependencyRef> { p1, p2, m1, m2, m3 };

            var sequencer = new BuildSequencer(null, null, null, null, null);

            // Mark the projects to dirty directly depends on any project in the search list.
            sequencer.MarkDirty(projectList, dirtyProjects);

            Assert.IsTrue(m1.IsDirty);
            Assert.IsFalse(m2.IsDirty); // This should be unchanged yet.
            Assert.IsFalse(m3.IsDirty);

            // Walk further to all the downstream projects.
            sequencer.MarkDirtyAll(projectList, dirtyProjects);

            Assert.IsTrue(m1.IsDirty);
            Assert.IsTrue(m2.IsDirty); // This is now marked dirty.
            Assert.IsFalse(m3.IsDirty);

        }

        
    }
}
