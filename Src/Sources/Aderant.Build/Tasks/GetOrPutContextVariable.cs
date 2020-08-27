using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class GetOrPutContextVariable : BuildOperationContextTask {
        private static ConcurrentDictionary<string, string> lookupCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static char[] splitChar = new char[] { '=' };

        private bool hasChanges;

        public string Scope { get; set; }

        public string VariableName { get; set; }

        [Output]
        public string Value { get; set; }

        [Output]
        public string[] Values {
            get {
                if (!string.IsNullOrEmpty(Value)) {
                    return Value.Split(';').ToArray();
                }
                return null;
            }
        }

        public ITaskItem[] Properties { get; set; }

        /// <summary>
        /// Gets the value from thread safe local storage rather than the build service for improved lookup performance.
        /// </summary>
        public bool AllowInProcLookup { get; set; }

        public override bool ExecuteTask() {
            if (!string.IsNullOrEmpty(VariableName)) {
                if (string.IsNullOrEmpty(Value) && NoProperties()) {
                    var value = GetExistingVariable();

                    if (string.IsNullOrEmpty(Scope) && !string.IsNullOrEmpty(value)) {
                        lookupCache.TryAdd(VariableName, value);
                    }

                    Log.LogMessage(MessageImportance.Low, $"Retrieved variable: {VariableName} with value {value}");

                    Value = value;

                    return !Log.HasLoggedErrors;
                }
            }

            if (Properties != null) {
                StringBuilder sb = new StringBuilder();

                foreach (var item in Properties) {
                    string[] parts = item.ItemSpec.Split(splitChar, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 2) {
                        string variableName = parts[0];
                        string value = parts[1];

                        if (string.IsNullOrEmpty(Scope)) {
                            AddVariable(variableName, value);
                        } else {
                            PipelineService.PutVariable(Scope, variableName, value);
                        }
                    } else if (parts.Length == 1) {
                        sb.Append(item.ItemSpec);
                        sb.Append(";");
                    }
                }

                if (sb.Length > 0) {
                    AddVariable(VariableName, sb.ToString());
                }
            }

            SendChangesToService();

            return !Log.HasLoggedErrors;
        }

        private void SendChangesToService() {
            if (hasChanges) {
                // Post the values up to the build coordinator
                foreach (var item in Context.Variables) {
                    Service.PutVariable("", item.Key, item.Value);
                }
            }
        }

        private void AddVariable(string variableName, string value) {
            hasChanges = true;

            Log.LogMessage($"Variable {variableName} -> {value}");

            lookupCache.TryAdd(variableName, value);
            Context.Variables[variableName] = value;
        }

        private bool NoProperties() {
            return Properties == null || Properties.Length == 0;
        }

        private string GetExistingVariable() {
            Log.LogMessage(MessageImportance.Low, "Looking up variable: " + VariableName);

            if (string.IsNullOrEmpty(Scope)) {
                string value;

                if (AllowInProcLookup) {
                    if (lookupCache.TryGetValue(VariableName, out value)) {
                        // Cache hit
                        return value;
                    }
                }

                if (Context.Variables.TryGetValue(VariableName, out value)) {
                    return value;
                }

                return PipelineService.GetVariable(string.Empty, VariableName);
            }

            return PipelineService.GetVariable(Scope, VariableName);
        }
    }
}