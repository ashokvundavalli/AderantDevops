using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Aderant.Build.Services;

namespace Aderant.Build {

    [Export(typeof(IArgumentBuilder))]
    [ExportMetadata(CompositionProperties.AppliesTo, WellKnownProperties.MsBuild)]
    internal class ComboBuildArgBuilder : IArgumentBuilder {
        private readonly Context context;

        [ImportingConstructor]
        public ComboBuildArgBuilder([Import] Context context) {
            this.context = context;
        }

        public string[] GetArguments(string commandLine) {
            List<string> argList = new List<string>();

            if (commandLine != null) {
                argList.Add(commandLine);
            }

            if (context.BuildMetadata != null) {
                if (context.BuildMetadata.DebugLoggingEnabled) {
                    argList.Add("/v:diag");
                }

                if (context.BuildMetadata.IsPullRequest) {
                    argList.Add("/v:diag");
                }
            }

            // Don't show the logo and do not allow node reuse so all child nodes are shut down once the master node has completed build orchestration.
            argList.Add("/nologo");
            argList.Add("/nr:false");

            // Multi-core build
            argList.Add("/m");

            if (context.IsDesktopBuild) {
                argList.Add("/p:IsDesktopBuild=true");
            } else {
                argList.Add("/p:IsDesktopBuild=false");
                argList.Add("/clp:PerformanceSummary");
            }

            argList.Add(Path.Combine(context.BuildScriptsDirectory, "Aderant.ComboBuild.targets"));


            return argList.ToArray();
        }
    }
}