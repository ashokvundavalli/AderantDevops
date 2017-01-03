using System;
using System.Management.Automation;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace WarningRatchet {
    [Cmdlet(VerbsLifecycle.Invoke, "WarningRatchet")]
    public class WarningRatchetCommand : PSCmdlet {
        [Parameter(Mandatory = true)]
        public string TeamFoundationServer { get; set; }

        [Parameter(Mandatory = true)]
        public string TeamProject { get; set; }

        [Parameter(Mandatory = true)]
        public int BuildId { get; set; }

        protected override void BeginProcessing() {
            base.BeginProcessing();

            var connection = new VssConnection(new Uri(TeamFoundationServer), new VssCredentials());

            var ratchet = new Aderant.Build.Tasks.WarningRatchet(connection);
            var request = ratchet.CreateNewRequest(TeamProject, BuildId);

            var currentBuildCount = ratchet.GetBuildWarningCount(request);
            var lastGoodBuildCount = ratchet.GetLastGoodBuildWarningCount(request);

            Host.UI.WriteLine($"Current build warnings: {currentBuildCount}. Last good build warnings {lastGoodBuildCount?.ToString() ?? "null"}");

            WriteObject(
                new {
                    CurrentBuildCount = currentBuildCount,
                    LastGoodBuildCount = lastGoodBuildCount,
                    Request = request,
                    Ratchet = ratchet
                });
        }
    }
}