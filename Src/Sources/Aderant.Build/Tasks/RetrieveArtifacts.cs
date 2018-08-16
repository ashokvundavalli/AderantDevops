using System.Text;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    public class WriteBuildStateFile : BuildOperationContextTask {

        protected override bool UpdateContextOnCompletion { get; set; } = true;

        public override bool ExecuteTask() {
            var writer = new BuildStateWriter();
            writer.WriteStateFiles(Context);
            return !Log.HasLoggedErrors;
        }
    }

    public sealed class RetrieveArtifacts : BuildOperationContextTask {

        [Required]
        public string SolutionRoot { get; set; }

        public string PublisherName { get; set; }

        public string WorkingDirectory { get; set; }

        protected override bool UpdateContextOnCompletion { get; set; } = false;

        public override bool ExecuteTask() {
            var service = new ArtifactService(Logger);
            service.Resolve(Context, PublisherName, SolutionRoot, WorkingDirectory);

            return !Log.HasLoggedErrors;
        }
    }

    public sealed class PrintBanner : Task {

        private static string header = "╔═════════════════════════════════════════════════════════════════════╗";
        private static string side = "║";
        private static string footer = "╚═════════════════════════════════════════════════════════════════════╝";

        public string Text { get; set; }

        public override bool Execute() {
            if (string.IsNullOrWhiteSpace(Text)) {
                return true;
            }

            var center = header.Length / 2 + Text.Length / 2;

            string text = Text.PadLeft(center).PadRight(header.Length - 2);

            StringBuilder sb = new StringBuilder(header);
            sb.AppendLine();
            sb.Append(side);
            sb.Append(text);
            sb.Append(side);
            sb.AppendLine();
            sb.Append(footer);

            Log.LogMessage(sb.ToString());

            return true;
        }
    }

}
