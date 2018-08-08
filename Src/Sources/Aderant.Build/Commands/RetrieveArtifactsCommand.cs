using System.Management.Automation;

namespace Aderant.Build.Commands {

    [Cmdlet(VerbsCommon.Get, "BuildArtifacts")]
    public class RetrieveArtifactsCommand : Cmdlet {

        [Parameter(Mandatory = false)]
        public string DependencyManifestPath { get; set; }

        protected override void ProcessRecord() {

            //var service = new ArtifactService();

        }
    }

}
