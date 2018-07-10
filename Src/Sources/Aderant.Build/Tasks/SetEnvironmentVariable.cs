using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class SetEnvironmentVariable : Task {

        [Required]
        public ITaskItem[] Variables { get; set; }

        public override bool Execute() {
            Dictionary<string, string> newEnvironment = Variables.ToDictionary(s => s.ItemSpec, s => s.GetMetadata("Value"));

            SetEnvironment(newEnvironment);

            return !Log.HasLoggedErrors;
        }

        internal void SetEnvironment(IDictionary<string, string> newEnvironment) {
            if (newEnvironment != null) {
                foreach (KeyValuePair<string, string> kvp in newEnvironment) {
                    Log.LogMessage(MessageImportance.Low, $"Setting environment variable {kvp.Key} to {kvp.Value}");
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value, EnvironmentVariableTarget.Process);
                }
            }
        }
    }

}
