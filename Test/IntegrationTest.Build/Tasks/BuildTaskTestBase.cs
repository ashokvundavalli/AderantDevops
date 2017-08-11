using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("IntegrationTest.targets")]
    [DeploymentItem("Aderant.Build.Common.targets")]
    [DeploymentItem("SystemUnderTest")]
    public abstract class BuildTaskTestBase {
        public TestContext TestContext { get; set; }

        protected void RunTarget(string targetName) {
            var globalProperties = new Dictionary<string, string> {
                { "NoMSBuildCommunityTasks", "true" },
                { "BuildToolsDirectory", TestContext.DeploymentDirectory }
            };

            using (var collection = new ProjectCollection(globalProperties)) {
                collection.RegisterLogger(Logger = new InternalBuildLogger(TestContext)); // Must pass in a logger derived from Microsoft.Build.Framework.ILogger.

                var project = collection.LoadProject(Path.Combine(TestContext.DeploymentDirectory, "IntegrationTest.targets"));
                var result = project.Build(targetName);

                Assert.IsFalse(Logger.HasRaisedErrors);
            }
        }

        public InternalBuildLogger Logger { get; set; }

        /// <summary>
        /// Class to hook msbuild messages.
        /// </summary>
        public class InternalBuildLogger : Microsoft.Build.Framework.ILogger {
            private readonly TestContext textContext;

            public InternalBuildLogger(TestContext textContext) {
                this.textContext = textContext;
            }

            public void Initialize(IEventSource eventSource) {
                eventSource.MessageRaised += new BuildMessageEventHandler(EventSourceMessageRaised);
                eventSource.WarningRaised += new BuildWarningEventHandler(EventSourceWarningRaised);
                eventSource.ErrorRaised += new BuildErrorEventHandler(EventSourceErrorRaised);
            }

            public void Shutdown() {
            }

            public LoggerVerbosity Verbosity { get; set; }
            public string Parameters { get; set; }

            void EventSourceErrorRaised(object sender, BuildErrorEventArgs e) {
                HasRaisedErrors = true;
                string line = $"[MSBUILD]: ERROR {e.Message} in file {e.File} ({e.LineNumber},{e.ColumnNumber}): ";
                textContext.WriteLine(line);
            }

            public bool HasRaisedErrors { get; set; }

            void EventSourceWarningRaised(object sender, BuildWarningEventArgs e) {
                string line = $"[MSBUILD]: Warning {e.Message} in file {e.File}({e.LineNumber},{e.ColumnNumber}): ";
                textContext.WriteLine(line);
            }

            void EventSourceMessageRaised(object sender, BuildMessageEventArgs e) {
                textContext.WriteLine($"[MSBUILD]: {e.Message}");
            }
        }
    }
}