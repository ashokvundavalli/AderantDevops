using System;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class GetOrPutContextVariable : BuildOperationContextTask {

        private static char[] splitChar = new char[] { '=' };

        public string Scope { get; set; }

        public string VariableName { get; set; }

        [Output]
        public string Value { get; set; }

        public ITaskItem[] Data { get; set; }

        public override bool ExecuteTask() {
            if (!string.IsNullOrEmpty(VariableName)) {
                if (string.IsNullOrEmpty(Value)) {
                    Value = GetExistingVariable();
                    Log.LogMessage($"Retrieved variable: {VariableName} with value {Value}");
                    return !Log.HasLoggedErrors;
                }
            }

            if (Data != null) {
                foreach (var item in Data) {
                    string[] parts = item.ItemSpec.Split(splitChar, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 2) {
                        string variableName = parts[0];
                        string value = parts[1];

                        Log.LogMessage($"Variable {variableName} -> {value}");

                        if (string.IsNullOrEmpty(Scope)) {
                            Context.Variables[variableName] = value;
                        } else {
                            PipelineService.PutVariable(Scope, variableName, value);
                        }
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }

        private string GetExistingVariable() {
            Log.LogMessage(MessageImportance.Low, "Looking up variable: " + VariableName);

            if (string.IsNullOrEmpty(Scope)) {
                string value;
                if (Context.Variables.TryGetValue(VariableName, out value)) {
                    return value;
                }
            } else {
                return PipelineService.GetVariable(Scope, VariableName);
            }

            return null;
        }
    }

}