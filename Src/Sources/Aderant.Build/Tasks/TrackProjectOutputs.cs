using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class TrackProjectOutputs : BuildOperationContextTask {

        [Required]
        public string ProjectFile { get; set; }

        [Required]
        public string IntermediateDirectory { get; set; }

        [Required]
        public string OutputPath { get; set; }

        public string[] ProjectOutputs { get; set; }

        protected override bool UpdateContextOnCompletion { get; set; } = true;

        public override bool ExecuteTask() {
            Context.RecordProjectOutputs(ProjectFile, ProjectOutputs, OutputPath, IntermediateDirectory);
            return !Log.HasLoggedErrors;
        }
    }

    public class ProjectOutputFileReader {
        private readonly IFileSystem fileSystem;

        public ProjectOutputFileReader()
            : this(new PhysicalFileSystem()) {
        }

        private ProjectOutputFileReader(IFileSystem fileSystem) {
            this.fileSystem = fileSystem;

        }

        public async void ReadOutputFiles(IEnumerable<string> files) {
            foreach (var file in files) {
                Stream stream = fileSystem.OpenFile(file);

                StreamReader reader = new StreamReader(stream);
                var readLineAsync = await reader.ReadLineAsync();

            }
        }
    }
}
