using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    [DeploymentItem(TargetsFile)]
    [DeploymentItem(TestDeployment)]
    [DeploymentItem(Tasks, Tasks)]
    [DeploymentItem(EndToEnd, EndToEnd)]
    [DeploymentItem(Packaging, Packaging)]
    public abstract class MSBuildIntegrationTestBase {

        public const string TargetsFile = "IntegrationTest.targets";
        public const string Tasks = "Tasks\\";
        public const string EndToEnd = "EndToEnd\\";
        public const string Packaging = "Packaging\\";
        public const string TestDeployment = "TestDeployment\\";

        public TestContext TestContext { get; set; }

        public InternalBuildLogger Logger { get; set; }

        public LoggerVerbosity LoggerVerbosity { get; set; } = LoggerVerbosity.Detailed;

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

            Dictionary<string, string> globalProperties = new Dictionary<string, string>(properties) {
                { "BuildToolsDirectory", TestContext.DeploymentDirectory },
                { "TestContextDeploymentDirectory", Path.Combine(TestContext.DeploymentDirectory) }
            };

            using (ProjectCollection collection = new ProjectCollection(globalProperties)) {
                collection.UnregisterAllLoggers();

                var logger = new InternalBuildLogger(TestContext, LoggerVerbosity);

                collection.RegisterLogger(Logger = logger);

                using (var manager = new BuildManager()) {
                    var result = manager.Build(
                        new BuildParameters(collection) {
                            Loggers = collection.Loggers,
                            DetailedSummary = DetailedSummary,
                            EnableNodeReuse = false,
                        },
                        new BuildRequestData(
                            Path.Combine(TestContext.DeploymentDirectory, "IntegrationTest.targets"),
                            globalProperties,
                            null,
                            new[] { targetName },
                            null));

                    if (result.OverallResult == BuildResultCode.Failure) {
                        LogFile = @"C:\temp\" + TestContext.TestName + ".log";
                        WriteLogFile(LogFile, logger.LogLines);
                    }

                    Result = result;

                    if (BuildMustSucceed) {
                        if (BuildResultCode.Failure == result.OverallResult && Environment.UserInteractive) {
                            Process.Start("notepad++", LogFile);
                        }

                        Assert.AreNotEqual(BuildResultCode.Failure, result.OverallResult);
                    }

                    LogLines = logger.LogLines;
                }
            }
        }

        public string LogFile { get; set; }

        public BuildResult Result { get; set; }

        public bool BuildMustSucceed { get; set; } = true;

        protected void WriteLogFile(string path, List<string> logFile) {
            using (FileStream file = new FileStream(path, FileMode.Create)) {
                var writer = new StreamWriter(file);

                foreach (var text in logFile) {
                    writer.Write(text);
                }
                file.Flush(true);
            }
        }

        public class InternalBuildLogger : ConsoleLogger {

            private char[] newLine = Environment.NewLine.ToCharArray();

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
