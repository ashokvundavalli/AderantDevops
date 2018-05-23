using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aderant.Build.Logging;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks.BuildTime {
    public class RunPowerShell : Task {
        public string Command { get; set; }

        public string File { get; set; }

        public string Arguments { get; set; }

        public bool ErrorStreamToOutputStream { get; set; }

        public bool OutputAsItemGroup { get; set; }

        [Output]
        public ITaskItem[] OutputValues { get; set; }

        [Output]
        public string Output { get; set; }

        public override bool Execute() {
            IDictionary<string, object> globals = ExtractGlobalsFromBuildEngine();

            PowerShellScriptRunner runner = new PowerShellScriptRunner(new BuildTaskLogger(this.Log));
            runner.SetGlobals(globals);
            runner.ErrorStreamToOutputStream = ErrorStreamToOutputStream;
            runner.ScriptCompleted += ScriptCompleted;

            InitializeEngineGlobals(runner, globals);

            if (!string.IsNullOrWhiteSpace(File)) {
                Log.LogMessage("Loading PowerShell script: " + File);

                var script = System.IO.File.ReadAllText(File);

                if (string.IsNullOrEmpty(script)) {
                    throw new BuildException("No script contents found: " + File);
                }
                runner.ExecuteScript(script, Arguments);
            } else {
                runner.ExecuteCommand(Command);
            }

            if (OutputAsItemGroup) {
                var itemGroup = new List<TaskItem>();

                foreach (var item in runner.Output) {
                    itemGroup.Add(new TaskItem(item));
                }

                OutputValues = itemGroup.ToArray();
            } else {
                string output = runner.Output.FirstOrDefault();
                this.Output = output;
            }

            return !Log.HasLoggedErrors;
        }

        internal virtual void ScriptCompleted(object sender, ScriptCompletedEventArgs scriptCompletedEventArgs) {
        }

        internal virtual void InitializeEngineGlobals(PowerShellScriptRunner runner, IDictionary<string, object> globals) {
        }

        // Dirty. Rips variable variables straight out of the MSBuild internal property state
        private IDictionary<string, object> ExtractGlobalsFromBuildEngine() {
            IDictionary<string, object> globals = new Dictionary<string, object>();

            SetGlobal("IsDesktopBuild", globals);
            SetGlobal("IsComboBuild", globals);

            return globals;
        }

        private void SetGlobal(string key, IDictionary<string, object> globals) {
            var value = this.BuildEngine.GetBuildEngineVariable(key, false).First();

            if (!string.IsNullOrEmpty(value)) {
                bool boolValue;
                if (bool.TryParse(value, out boolValue)) {
                    globals[key] = boolValue;
                }
            }
        }
    }

    internal static class BuildEngineExtensions {
        const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public;

        public static IEnumerable<string> GetBuildEngineVariable(this IBuildEngine buildEngine, string key, bool throwIfNotFound) {
            var projectInstance = GetProjectInstance(buildEngine);

            // Check properties
            var properties = projectInstance.Properties.Where(x => string.Equals(x.Name, key, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (properties.Count > 0) {
                return properties.Select(x => x.EvaluatedValue);
            }

            var items = projectInstance.Items.Where(x => string.Equals(x.ItemType, key, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (items.Count > 0) {
                return items.Select(x => x.EvaluatedInclude);
            }

            if (throwIfNotFound) {
                throw new Exception(string.Format("Could not extract from '{0}' environmental variables.", key));
            }

            return Enumerable.Empty<string>();
        }

        static ProjectInstance GetProjectInstance(IBuildEngine buildEngine) {
            var buildEngineType = buildEngine.GetType();
            var targetBuilderCallbackField = buildEngineType.GetField("_targetBuilderCallback", bindingFlags);
            if (targetBuilderCallbackField == null) {
                throw new Exception("Could not extract targetBuilderCallback from " + buildEngineType.FullName);
            }
            var targetBuilderCallback = targetBuilderCallbackField.GetValue(buildEngine);
            var targetCallbackType = targetBuilderCallback.GetType();
            var projectInstanceField = targetCallbackType.GetField("_projectInstance", bindingFlags);
            if (projectInstanceField == null) {
                throw new Exception("Could not extract projectInstance from " + targetCallbackType.FullName);
            }
            return (ProjectInstance)projectInstanceField.GetValue(targetBuilderCallback);
        }
    }
}