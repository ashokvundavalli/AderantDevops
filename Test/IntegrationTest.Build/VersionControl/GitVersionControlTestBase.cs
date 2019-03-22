﻿using System;
using System.IO;
using IntegrationTest.Build.EndToEnd;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.VersionControl {
    [TestClass]
    [DeploymentItem(@"TestDeployment\x86\", "x86")]
    [DeploymentItem(@"TestDeployment\x64\", "x64")]
    public abstract class GitVersionControlTestBase {
        private static bool shareRepositoryBetweenTests;

        public virtual TestContext TestContext { get; set; }

        public string RepositoryPath {
            get {
                if (TestContext.Properties.Contains("Repository")) {
                    return TestContext.Properties["Repository"].ToString();
                }

                return TestDirectory(TestContext, shareRepositoryBetweenTests);
            }
        }

        protected static void Initialize(TestContext context, string resources, bool shareRepositoryBetweenTests) {
            GitVersionControlTestBase.shareRepositoryBetweenTests = shareRepositoryBetweenTests;

            var testDirectory = TestDirectory(context, shareRepositoryBetweenTests);

            Directory.CreateDirectory(testDirectory);

            RunPowerShell(context, resources);
        }

        private static string TestDirectory(TestContext context, bool shareRepositoryBetweenTests) {
            var testDirectory = Path.Combine(context.DeploymentDirectory, shareRepositoryBetweenTests ? "0C68A615-E3A7-47E7-8BEF-AB858B28CD92" : context.TestName);
            context.Properties["Repository"] = testDirectory;
            return testDirectory;
        }

        protected static void RunPowerShell(TestContext context, string script) {
            PowerShellHelper.RunCommand(script, context, context.Properties["Repository"].ToString());
        }
    }
}