using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    [DeploymentItem(TargetsFile)]
    [DeploymentItem(TestDeployment)]
    [DeploymentItem(Tasks, Tasks)]
    [DeploymentItem(EndToEnd, EndToEnd)]
    [DeploymentItem(Packaging, Packaging)]
#if INTEGRATION_TEST_CORE
    [DeploymentItem(@"..\..\Src\Build.Tools\", "Build.Tools")] // Deploy the native libgit binaries
    [DeploymentItem(@"..\..\Src\Build\Tasks\", "Build\\Tasks")]
#endif
    public abstract class MSBuildIntegrationTestBase {

        private BuildResult result;
        private InternalBuildLogger logger;

        public const string TargetsFile = "IntegrationTest.targets";
        public const string Tasks = "Tasks\\";
        public const string EndToEnd = "EndToEnd\\";
        public const string Packaging = "Packaging\\";
        internal const string TestDeployment = "TestDeployment\\";

        public TestContext TestContext { get; set; }

        // String property so it doesn't take a dependency on MSBuild.
        // If this was typed then MSTest won't be able to reflect this type and no tests will run as it can't find the dependencies
        // in the output
        public string LoggerVerbosity { get; set; } = nameof(Microsoft.Build.Framework.LoggerVerbosity.Detailed);

        public bool DetailedSummary { get; set; } = true;

        public List<string> LogLines { get; set; }

        /// <summary>
        /// Runs a target in your targets
        /// </summary>
        protected void RunTarget(string targetName, IDictionary<string, string> properties = null) {
            if (properties == null) {
                properties = new Dictionary<string, string>();
            }

            Assert.IsTrue(File.Exists(Path.Combine(TestContext.DeploymentDirectory, TargetsFile)));
            Assert.IsTrue(Directory.Exists(Path.Combine(TestContext.DeploymentDirectory, nameof(Tasks))));
            Assert.IsTrue(Directory.Exists(Path.Combine(TestContext.DeploymentDirectory, nameof(EndToEnd))));
            Assert.IsTrue(Directory.Exists(Path.Combine(TestContext.DeploymentDirectory, nameof(Packaging))));

            Dictionary<string, string> globalProperties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase) {
                { "BuildToolsDirectory", Path.Combine(TestContext.DeploymentDirectory, "Build.Tools") },
                { "BUILD_REPOSITORY_NAME", "Build.Infrastructure" }
            };

            LoggerVerbosity verbosity = (LoggerVerbosity)Enum.Parse(typeof(LoggerVerbosity), LoggerVerbosity);
            if (verbosity > Microsoft.Build.Framework.LoggerVerbosity.Normal) {
                Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "1", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("MSBUILDLOGTASKINPUTS", "1", EnvironmentVariableTarget.Process);
            }

            using (ProjectCollection collection = new ProjectCollection(globalProperties)) {
                collection.UnregisterAllLoggers();

                this.logger = new InternalBuildLogger(TestContext, verbosity);

                collection.RegisterLogger(logger);

                string logFile = $"LogFile={Path.Combine(TestContext.TestResultsDirectory, TestContext.TestName + ".binlog")}";

                collection.RegisterLogger(new BinaryLogger() {
                    Verbosity = verbosity,
                    CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.None,
                    Parameters = logFile
                });

                using (var manager = new BuildManager()) {
                    BuildRequestData data = new BuildRequestData(
                        Path.Combine(TestContext.DeploymentDirectory, "IntegrationTest.targets"),
                        globalProperties,
                        null,
                        new[] {
                            targetName
                        },
                        null,
                        BuildRequestDataFlags.ProvideProjectStateAfterBuild);

                    this.result = manager.Build(
                        new BuildParameters(collection) {
                            Loggers = collection.Loggers,
                            DetailedSummary = DetailedSummary,
                            EnableNodeReuse = false,
                            DisableInProcNode = DisableInProcNode,
                        }, data);


                    if (result.OverallResult == BuildResultCode.Failure) {
                        LogFile = logFile;
                    }

                    if (BuildMustSucceed) {
                        Assert.AreNotEqual(BuildResultCode.Failure, result.OverallResult);
                    }

                    LogLines = logger.LogLines;
                }
            }
        }

        public bool DisableInProcNode {
            get {
                string version = ToolLocationHelper.CurrentToolsVersion;
                if (version == "Current" || version == "15.0") {
                    return true;
                }

                return false;
            }
        }

        public string LogFile { get; set; }

        protected BuildResult GetResult() {
            return result;
        }

        public bool BuildMustSucceed { get; set; } = true;

        public bool HasRaisedErrors {
            get {
                return logger.HasRaisedErrors;
            }
        }

        public class InternalBuildLogger : ConsoleLogger {

            private static readonly char[] newLine = Environment.NewLine.ToCharArray();

            public InternalBuildLogger(TestContext context, LoggerVerbosity verbosity)
                : base(verbosity) {
                WriteHandler = message => {
                    LogLines.Add(message);
                    context.WriteLine(message.Replace("{", "{{").Replace("}", "}}").TrimEnd(newLine));
                };
            }

            public bool HasRaisedErrors { get; set; }

            public List<string> LogLines { get; } = new List<string>(10000);

            public override void Initialize(IEventSource eventSource) {
                base.Initialize(eventSource);
                eventSource.ErrorRaised += (sender, args) => HasRaisedErrors = true;
            }

        }

    }
}