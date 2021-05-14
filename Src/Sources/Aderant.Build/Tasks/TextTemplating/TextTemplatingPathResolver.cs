﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using VisualStudioConfiguration;

namespace Aderant.Build.Tasks.TextTemplating {

    /// <summary>
    /// Grovels the current computer trying to find the various Visual Studio components that parts of the build depends on.
    /// </summary>
    public sealed class TextTemplatingPathResolver : Task {
        private static VisualStudioPathInfo visualStudioPathInfo;

        /// <summary>
        /// The reference paths to be provided to the Text Transform component. These are paths the engine will look in for on demand dependencies.s
        /// </summary>
        [Output]
        public string[] ReferencePaths {
            get { return ToArray(ref visualStudioPathInfo.ReferencePaths); }
        }

        /// <summary>
        /// The full path to standard references - these are assemblies that must be loaded.
        /// </summary>
        [Output]
        public string[] AssemblyReferences {
            get { return ToArray(ref visualStudioPathInfo.AssemblyReferences); }
        }

        /// <summary>
        /// The paths to the Microsoft Text Transform task(s).
        /// </summary>
        [Output]
        public string[] TextTemplatingBuildTaskPaths {
            get { return ToArray(ref visualStudioPathInfo.TextTemplatingBuildTaskPath); }
        }

        [Output]
        public string[] DslDirectiveProcessors {
            get { return ToArray(ref visualStudioPathInfo.DslDirectiveProcessors); }
        }

        private static string[] ToArray(ref SynchronizedCollection<string> synchronizedCollection) {
            lock (synchronizedCollection.SyncRoot) {
                return synchronizedCollection.ToArray();
            }
        }

        public override bool Execute() {
            // There is a race condition here as multiple threads could create VisualStudioPathInfo but the final outcome will be the same so we
            // allow the wasted effort
            if (visualStudioPathInfo == null) {
                ResolvePaths();
            }

            return !Log.HasLoggedErrors;
        }

        private void ResolvePaths() {
            visualStudioPathInfo = new VisualStudioPathInfo();

            IList<VisualStudioInstance> invalid;
            var visualStudioInstances = VisualStudioLocationHelper.GetInstances(out invalid);

            LogInvalidInstanceFound(invalid);

            var pathToVisualStudioIntegration = Path.Combine("VSSDK", "VisualStudioIntegration", "Common", "Assemblies", "v4.0");

            foreach (var instance in visualStudioInstances) {
                Log.LogMessage(MessageImportance.Normal, "Processing {0}", instance.Path);

                var taskAssembly = Path.Combine(instance.Path, "MSBuild", "Microsoft", "VisualStudio", CreateDottedMajorVersion(instance), "TextTemplating", "Microsoft.TextTemplating.Build.Tasks.dll");

                if (Add(visualStudioPathInfo.TextTemplatingBuildTaskPath, taskAssembly, true)) {
                    Log.LogMessage(MessageImportance.Normal, "Loading {0}", taskAssembly);

                    System.Reflection.Assembly.LoadFrom(taskAssembly);
                }

                var fullPathToVisualStudioIntegration = Path.Combine(instance.Path, pathToVisualStudioIntegration);
                Add(visualStudioPathInfo.ReferencePaths, fullPathToVisualStudioIntegration, false);

                string[] processors = Directory.GetFileSystemEntries(fullPathToVisualStudioIntegration, "Microsoft.VisualStudio.Modeling.Sdk.DslDefinition.*.dll");
                foreach (var dsl in processors) {
                    Add(visualStudioPathInfo.DslDirectiveProcessors, dsl, true);
                }

                string[] entries = Directory.GetFileSystemEntries(fullPathToVisualStudioIntegration, "Microsoft.VisualStudio.Modeling.Sdk*.dll");
                foreach (var sdkFile in entries) {
                    Add(visualStudioPathInfo.AssemblyReferences, sdkFile, true);
                }

                Add(visualStudioPathInfo.ReferencePaths, Path.Combine(instance.Path, "Common7", "IDE", "PublicAssemblies"), false);
                Add(visualStudioPathInfo.ReferencePaths, Path.Combine(instance.Path, "Common7", "IDE", "PrivateAssemblies"), false);

                var root = ToolLocationHelper.GetPathToBuildTools(instance.Version.Major.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(root)) {
                    Add(visualStudioPathInfo.ReferencePaths, Path.Combine(root, "Roslyn"), false);
                }
            }

            var paths = new[] {
                Environment.GetEnvironmentVariable("VSSDK140Install")
            };

            foreach (var path in paths) {
                if (!string.IsNullOrEmpty(path)) {
                    Add(visualStudioPathInfo.ReferencePaths, Path.Combine(path, pathToVisualStudioIntegration), false);
                }
            }
        }

        private void LogInvalidInstanceFound(IList<VisualStudioInstance> invalid) {
            if (invalid != null && invalid.Count > 0) {
                Log.LogWarning("The following VisualStudio installations are invalid. Check the installation state from the Visual Studio Installer.");

                foreach (var instance in invalid) {
                    Log.LogWarning(instance.Path);
                }
            }
        }

        internal static string CreateDottedMajorVersion(VisualStudioInstance instance) {
            return "v" + instance.Version.Major.ToString(CultureInfo.InvariantCulture) + ".0";
        }

        private bool Add(SynchronizedCollection<string> collection, string path, bool isFile) {
            if (isFile && !File.Exists(path)) {
                return false;
            }

            if (!isFile && !Directory.Exists(path)) {
                return false;
            }

            lock (collection.SyncRoot) {
                if (!collection.Contains(path, StringComparer.OrdinalIgnoreCase)) {
                    collection.Add(path);
                    return true;
                }
            }

            return false;
        }

        private sealed class VisualStudioPathInfo {
            internal SynchronizedCollection<string> TextTemplatingBuildTaskPath = new SynchronizedCollection<string>();
            internal SynchronizedCollection<string> DslDirectiveProcessors = new SynchronizedCollection<string>();
            internal SynchronizedCollection<string> ReferencePaths = new SynchronizedCollection<string>();
            internal SynchronizedCollection<string> AssemblyReferences = new SynchronizedCollection<string>();
        }
    }
}