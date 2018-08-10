using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("IntegrationTest.targets")]
    [DeploymentItem("SystemUnderTest\\")]
    [DeploymentItem("Tasks\\", "Tasks\\")]
    public abstract class BuildTaskTestBase {
        private Project project;

        public TestContext TestContext { get; set; }

        public InternalBuildLogger Logger { get; set; }

        /// <summary>
        /// Runs a target in IntegrationTest.targets
        /// </summary>
        protected void RunTarget(string targetName, IDictionary<string, string> properties = null) {
            if (properties == null) {
                properties = new Dictionary<string, string>();
            }

            Dictionary<string, string> globalProperties = new Dictionary<string, string>(properties) {
                { "NoMSBuildCommunityTasks", "true" },
                { "BuildToolsDirectory", TestContext.DeploymentDirectory },
                { "BuildInfrastructureDirectory", Path.Combine(TestContext.DeploymentDirectory, @"..\..\") /*TODO: Remove the need for this*/ }
            };

            using (ProjectCollection collection = new ProjectCollection(globalProperties)) {
                collection.RegisterLogger(Logger = new InternalBuildLogger(TestContext)); // Must pass in a logger derived from Microsoft.Build.Framework.ILogger.

                using (var manager = new BuildManager()) {

                    var result = manager.Build(
                        new BuildParameters(collection) { Loggers = collection.Loggers, DetailedSummary = true },
                        new BuildRequestData(
                            Path.Combine(TestContext.DeploymentDirectory, "IntegrationTest.targets"),
                            globalProperties,
                            null,
                            new[] { targetName },
                            null));

                    Assert.AreNotEqual(BuildResultCode.Failure, result.OverallResult);
                }
            }
        }

        public class InternalBuildLogger : ILogger {
            private readonly TestContext textContext;

            public InternalBuildLogger(TestContext textContext) {
                this.textContext = textContext;
            }

            public bool HasRaisedErrors { get; set; }

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

            void EventSourceWarningRaised(object sender, BuildWarningEventArgs e) {
                string line = $"[MSBUILD]: Warning {e.Message} in file {e.File}({e.LineNumber},{e.ColumnNumber}): ";
                textContext.WriteLine(line);
            }

            void EventSourceMessageRaised(object sender, BuildMessageEventArgs e) {
                textContext.WriteLine("[MSBUILD]: " + e.Message.Replace("{", "{{").Replace("}", "}}"), Array.Empty<object>());
            }
        }
    }
}
