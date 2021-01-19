using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Hosting;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks.GitVersion {
    [TestClass]
    public class GitVersionTests : MSBuildIntegrationTestBase {
        [TestInitialize]
        public void NativeLibraryAvailable() {
            var foo = typeof(Aderant.Build.Tasks.GitVersion);
            string nativeLibraryPath = LibGit2Sharp.GlobalSettings.NativeLibraryPath;

            TestContext.WriteLine("NativeLibraryPath: '{0}'.", nativeLibraryPath);

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => string.Equals(x.FullName, "LibGit2Sharp, Version=0.26.0.0, Culture=neutral, PublicKeyToken=7cbde695407f0333"));
            
            Assert.IsNotNull(assembly);

            Type type = assembly.GetType("LibGit2Sharp.Core.NativeMethods");
            var propertyInfo = type.GetMethod("git_config_find_programdata", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(propertyInfo);

            var nativeAssemblyName = propertyInfo.GetCustomAttributesData().FirstOrDefault(x => x.AttributeType == typeof(DllImportAttribute))?.ConstructorArguments.FirstOrDefault().Value;

            Assert.IsNotNull(nativeAssemblyName);

            TestContext.WriteLine("Native assembly name: '{0}.dll'.", nativeAssemblyName);

            Assert.IsTrue(Directory.Exists(nativeLibraryPath));
        }

        [TestMethod]
        public void GitVersion_runs_without_exception() {
            RunTarget("GitVersion");

            Assert.IsFalse(Logger.HasRaisedErrors);
        }
    }
}