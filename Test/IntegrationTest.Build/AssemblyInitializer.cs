using System;
using System.IO;
using IntegrationTest.Build.Helpers;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {

    [TestClass]
    [DeploymentItem(@"TestDeployment\x86\", "x86")]
    [DeploymentItem(@"TestDeployment\x64\", "x64")]
    public class AssemblyInitializer {

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context) {
            Environment.CurrentDirectory = context.DeploymentDirectory;

            GlobalSettings.NativeLibraryPath = context.DeploymentDirectory;

            PowerShellHelper.RunCommand("& git config --global core.autocrlf false", context, null);
        }
    }
}