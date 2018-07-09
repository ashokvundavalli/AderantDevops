using System;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Extracts key properties from the context and returns them to MSBuild
    /// </summary>
    public sealed class DefaultContextProperties : ContextTaskBase {

        [Output]
        public bool IsDesktopBuild { get; set; }

        [Required]
        public override string ContextFileName {
            get; set;
        }

        protected override bool ExecuteTask(Context context) {
            Environment.SetEnvironmentVariable(WellKnownProperties.ContextFileName, ContextFileName, EnvironmentVariableTarget.Process);

            IsDesktopBuild = context.IsDesktopBuild;

            return !Log.HasLoggedErrors;
        }
    }
}
