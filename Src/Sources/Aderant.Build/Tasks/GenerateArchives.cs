using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Parallel = System.Threading.Tasks.Parallel;

namespace Aderant.Build.Tasks {
    public sealed class GenerateArchives : BuildOperationContextTask {

        private CompressionLevel compressionLevel;

        [Required]
        public ITaskItem[] DirectoriesToArchive { get; set; }

        [Required]
        public ITaskItem[] OutputArchives { get; set; }

        [Required]
        public string CompressionLevel {
            get { return compressionLevel.ToString(); }
            set { compressionLevel = (CompressionLevel)Enum.Parse(typeof(CompressionLevel), value); }
        }

        [Output]
        public ITaskItem[] ArchivedFiles { get; private set; }

        public override bool ExecuteTask() {
            if (DirectoriesToArchive == null || OutputArchives.Length == 0) {
                Log.LogError("Value cannot be null or empty.", nameof(DirectoriesToArchive));
                return !Log.HasLoggedErrors;
            }

            if (OutputArchives == null || OutputArchives.Length == 0) {
                Log.LogError("Value cannot be null or whitespace.", nameof(OutputArchives));
                return !Log.HasLoggedErrors;
            }

            if (DirectoriesToArchive.Length != OutputArchives.Length) {
                Log.LogError($"Item count does not match: '{nameof(DirectoriesToArchive)}' item count: {DirectoriesToArchive.Length}, '{nameof(OutputArchives)}' item count: {OutputArchives.Length}.");
                return !Log.HasLoggedErrors;
            }

            List<PathSpec> directoriesToArchive = ConstructPathSpecs(DirectoriesToArchive, OutputArchives);

            Log.LogMessage($"Archive compression level set to: '{CompressionLevel}'.");
            foreach (PathSpec pathSpec in directoriesToArchive) {
                Log.LogMessage($"Archiving directory: '{pathSpec.Location}'");
                Log.LogMessage($"To file: '{pathSpec.Destination}'");
            }

            ProcessDirectories(directoriesToArchive, compressionLevel);
            ArchivedFiles = directoriesToArchive.Select(x => (ITaskItem)new TaskItem(x.Destination, new Dictionary<string, string> { {"Name", Path.GetFileNameWithoutExtension(x.Destination) } })).ToArray();

            return !Log.HasLoggedErrors;
        }

        internal static List<PathSpec> ConstructPathSpecs(ITaskItem[] directoriesToArchive, ITaskItem[] outputArchives) {
            return directoriesToArchive.Select((t, i) => new PathSpec(t.ItemSpec, outputArchives[i].ItemSpec)).ToList();
        }

        internal static void ProcessDirectories(IList<PathSpec> directoriesToArchive, CompressionLevel compressionLevel) {
            Parallel.ForEach(
                directoriesToArchive,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount < 6 ? Environment.ProcessorCount : 6 },
                directory => {
                    string temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    
                    ZipFile.CreateFromDirectory(directory.Location, temp, compressionLevel, false);

                    File.Move(temp, directory.Destination);
                });
        }
    }
}
