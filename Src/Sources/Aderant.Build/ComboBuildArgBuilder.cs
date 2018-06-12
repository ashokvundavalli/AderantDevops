using System.Collections.Generic;
using System.ComponentModel.Composition;
using Aderant.Build.Services;

namespace Aderant.Build {

    [Export(typeof(IArgumentBuilder))]
    [ExportMetadata(CompositionProperties.ExportContext, WellKnownProperties.MsBuild)]
    internal class ComboBuildArgBuilder : IArgumentBuilder {
        private readonly Context context;

        [ImportingConstructor]
        public ComboBuildArgBuilder([Import] Context context) {
            this.context = context;
        }

        public string[] GetArguments(string commandLine) {
            List<string> argLst = new List<string>();

            if (commandLine != null) {
                argLst.Add(commandLine);
            }

            if (context.BuildMetadata != null) {
                if (context.BuildMetadata.DebugLoggingEnabled) {
                    argLst.Add("/v:diag");
                }

                if (context.BuildMetadata.IsPullRequest) {
                    argLst.Add("/v:diag");
                }
            }

            if (context.IsDesktopBuild) {
                argLst.Add("/p:IsDesktopBuild=true");
            } else {
                argLst.Add("/p:IsDesktopBuild=false");
                argLst.Add("/clp:PerformanceSummary");
            }

            argLst.Add("/p:ComboBuildType=All");

            return argLst.ToArray();
        }
    }
}