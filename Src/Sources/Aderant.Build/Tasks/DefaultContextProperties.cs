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

        public override bool Execute()  {
            Environment.SetEnvironmentVariable(WellKnownProperties.ContextFileName, ContextFileName, EnvironmentVariableTarget.Process);

            IsDesktopBuild = Context.IsDesktopBuild;

            return !Log.HasLoggedErrors;
        }
    }
}
