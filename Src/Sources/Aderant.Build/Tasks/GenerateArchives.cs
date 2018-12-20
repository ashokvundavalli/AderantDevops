using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.Packaging;
using Aderant.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace Aderant.Build.Tasks {
    public sealed class GenerateArchives : Task {

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

        public string[] ExcludeFilter { get; set; }

        public override bool Execute() {
            if (DirectoriesToArchive == null || OutputArchives.Length == 0) {
                Log.LogError("Value {0} cannot be null or empty.", nameof(DirectoriesToArchive));
                return !Log.HasLoggedErrors;
            }

            if (OutputArchives == null || OutputArchives.Length == 0) {
                Log.LogError("Value {0} cannot be null or whitespace.", nameof(OutputArchives));
                return !Log.HasLoggedErrors;
            }

            if (DirectoriesToArchive.Length != OutputArchives.Length) {
                Log.LogError($"Item count does not match: '{nameof(DirectoriesToArchive)}' item count: {DirectoriesToArchive.Length}, '{nameof(OutputArchives)}' item count: {OutputArchives.Length}.");
                return !Log.HasLoggedErrors;
            }

            try {
                List<PathSpec> directoriesToArchive = ConstructPathSpecs(DirectoriesToArchive, OutputArchives, ExcludeFilter);

                Log.LogMessage($"Archive compression level set to: '{CompressionLevel}'.");

                foreach (PathSpec pathSpec in directoriesToArchive) {
                    Log.LogMessage($"Compressing directory: '{pathSpec.Location}' --> '{pathSpec.Destination}");
                }

                try {
                    ProcessDirectories(directoriesToArchive, compressionLevel);
                } catch (AggregateException exception) {
                    Log.LogError(exception.Flatten().ToString());
                    return !Log.HasLoggedErrors;
                }

                ArchivedFiles = directoriesToArchive.Select(x => (ITaskItem)new TaskItem(x.Destination, new Dictionary<string, string> { { "Name", Path.GetFileNameWithoutExtension(x.Destination) } })).ToArray();
            } catch (Exception exception) {
                Log.LogErrorFromException(exception);
                return !Log.HasLoggedErrors;
            }

            return !Log.HasLoggedErrors;
        }

        internal List<PathSpec> ConstructPathSpecs(ITaskItem[] directoriesToArchive, ITaskItem[] outputArchives, string[] excludeFilter = null) {
            List<PathSpec> outputs = new List<PathSpec>();

            for (var i = 0; i < directoriesToArchive.Length; i++) {
                var item = directoriesToArchive[i];
                var destination = outputArchives[i];

                bool add = true;

                if (excludeFilter != null) {
                    foreach (var filter in excludeFilter) {
                        if (item.ItemSpec.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) {
                            Log.LogMessage(MessageImportance.Low, $"Ignoring file {item.ItemSpec} as it contains filter {filter}");
                            add = false;
                            break;
                        }
                    }
                }

                if (add) {
                    outputs.Add(new PathSpec(item.ItemSpec, destination.ItemSpec));
                }
            }

            return outputs;
        }

        internal static void ProcessDirectories(IList<PathSpec> directoriesToArchive, CompressionLevel compressionLevel) {
            Parallel.ForEach(
                directoriesToArchive,
                new ParallelOptions { MaxDegreeOfParallelism = ParallelismHelper.MaxDegreeOfParallelism() },
                directory => {
                    string temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                    ZipFile.CreateFromDirectory(directory.Location, temp, compressionLevel, false);

                    if (File.Exists(directory.Destination)) {
                        File.Delete(directory.Destination);
                    }

                    File.Move(temp, directory.Destination);
                });
        }
    }

}