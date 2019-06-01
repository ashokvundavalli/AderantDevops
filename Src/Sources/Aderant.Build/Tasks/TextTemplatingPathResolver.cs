using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class TextTemplatingPathResolver : Task {
        private static ConcurrentDictionary<string, string[]> lookupCache = new ConcurrentDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Is this task running under VS 2019 or higher?
        /// </summary>
        public bool IsVisualStudio2019OrHigher { get; set; }

        [Output]
        public string[] ReferencePaths {
            get { return GetItem(); }
        }

        [Output]
        public string[] AssemblyReferences {
            get { return GetItem(); }
        }

        [Output]
        public string[] TextTemplatingBuildTaskPaths {
            get { return GetItem(); }
        }

        [Output]
        public string[] DslDirectiveProcessors {
            get { return GetItem(); }
        }

        private string[] GetItem([CallerMemberName] string caller = null) {
            Debug.Assert(caller != null, "caller != null");

            return lookupCache[caller];
        }

        public override bool Execute() {
            if (lookupCache.IsEmpty) {
                var results = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var props = new Dictionary<string, string> { { "IsVisualStudio2019OrHigher", IsVisualStudio2019OrHigher.ToString() } };

                const string target = "TextTransformFindReferencePaths";

                var result = this.BuildEngine.BuildProjectFile(
                    this.BuildEngine.ProjectFileOfTaskNode,
                    new[] { target },
                    props,
                    results);

                Debug.Assert(result == true, "result == true");

                var outputs = results[target] as ITaskItem[];

                List<string> referencePaths = new List<string>();
                List<string> taskAssemblies = new List<string>();
                List<string> assemblyReferences = new List<string>();
                List<string> dslDirectiveProcessors = new List<string>();

                Debug.Assert(outputs != null, "outputs != null");

                foreach (var entry in outputs) {
                    if (string.Equals("true", entry.GetMetadata("IsTaskAssembly"), StringComparison.OrdinalIgnoreCase)) {
                        taskAssemblies.Add(entry.ItemSpec);
                    } else if (string.Equals("true", entry.GetMetadata("IsDslDirectiveProcessor"), StringComparison.OrdinalIgnoreCase)) {
                        dslDirectiveProcessors.Add(entry.ItemSpec);
                    } else if (entry.ItemSpec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                        assemblyReferences.Add(entry.ItemSpec);
                    } else {
                        referencePaths.Add(entry.ItemSpec);
                    }
                }

                lookupCache[nameof(ReferencePaths)] = referencePaths.ToArray();
                lookupCache[nameof(AssemblyReferences)] = assemblyReferences.ToArray();
                lookupCache[nameof(TextTemplatingBuildTaskPaths)] = taskAssemblies.ToArray();
                lookupCache[nameof(DslDirectiveProcessors)] = dslDirectiveProcessors.ToArray();

                LoadTemplateTask();
            } else {
                Log.LogMessage(MessageImportance.Low, "Using cached text template path information");
            }

            return !Log.HasLoggedErrors;
        }

        private void LoadTemplateTask() {
            foreach (var file in TextTemplatingBuildTaskPaths) {
                System.Reflection.Assembly.LoadFrom(file);
            }
        }
    }
}