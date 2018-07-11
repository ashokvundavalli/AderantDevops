using System;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Extracts key properties from the context and returns them to MSBuild
    /// </summary>
    public sealed class DefaultContextProperties : ContextTaskBase {

        [Output]
        public bool IsDesktopBuild { get; set; }

        [Output]
        public string BuildSystemDirectory { get; set; }

        [Output]
        public string BuildFlavor { get; set; }

        [Required]
        public override string ContextFileName {
            get; set;
        }

        public override bool Execute() {
            base.Execute();

            Environment.SetEnvironmentVariable(WellKnownProperties.ContextFileName, ContextFileName, EnvironmentVariableTarget.Process);

            IsDesktopBuild = Context.IsDesktopBuild;
            BuildSystemDirectory = Context.BuildSystemDirectory;

            SetFlavor();

            return !Log.HasLoggedErrors;
        }

        private void SetFlavor() {
            if (!string.IsNullOrEmpty(Context.BuildMetadata.Flavor)) {
                BuildFlavor = Context.BuildMetadata.Flavor;
            } else {
                if (Context.Switches.Release) {
                    BuildFlavor = "Release";
                } else {
                    BuildFlavor = "Debug";
                }

                Context.BuildMetadata.Flavor = BuildFlavor;
            }
        }
    }
}
