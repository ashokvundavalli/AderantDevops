using System;
using System.Text;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class WriteLinesToFile : Microsoft.Build.Tasks.WriteLinesToFile {
        private readonly IFileSystem2 fileSystem;

        public WriteLinesToFile()
            : this(new PhysicalFileSystem(Environment.CurrentDirectory)) {
        }

        private WriteLinesToFile(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        /// <summary>
        /// If true, the target file specified, if it exists, will be read first to compare against
        /// what the task would have written. If identical, the file is not written to disk and the
        /// timestamp will be preserved.
        /// </summary>
        public bool WriteOnlyWhenDifferent { get; set; }

        public override bool Execute() {
            if (WriteOnlyWhenDifferent) {

                if (fileSystem.FileExists(File.ItemSpec)) {
                    var existingContents = fileSystem.ReadAllText(File.ItemSpec);

                    StringBuilder buffer = new StringBuilder();
                    if (Lines != null) {
                        foreach (ITaskItem line in Lines) {
                            buffer.AppendLine(line.ItemSpec);
                        }
                    }

                    if (existingContents.Length == buffer.Length) {
                        var contentsAsString = buffer.ToString();

                        if (existingContents.Equals(contentsAsString)) {
                            Log.LogMessage(MessageImportance.Low, "Skipping unchanged file", File.ItemSpec);
                            return true;
                        }
                    }
                }
            }

            return base.Execute();
        }
    }
}