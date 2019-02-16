using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

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

            Log.LogMessage(MessageImportance.High, sb.ToString());

            return true;
        }
    }

    public sealed class SetProgress : BuildOperationContextTask {
        public string CurrentOperation { get; set; }
        public string Activity { get; set; }
        public string StatusDescription { get; set; }

        public override bool ExecuteTask() {
            System.Threading.Tasks.Task.Run(() => { PipelineService.SetProgress(CurrentOperation, Activity, StatusDescription); });
            return !Log.HasLoggedErrors;
        }
    }
}