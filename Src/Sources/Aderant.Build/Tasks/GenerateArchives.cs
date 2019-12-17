using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build.Packaging;
using Aderant.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class GenerateArchives : BuildOperationContextTask {
        static GenerateArchives() {
            DotNetQuriks.ZipFileUseForwardSlash();
        }

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

        public bool CreateManifest { get; set; }

        [Output]
        public ITaskItem[] ArchivedFiles { get; private set; }

        public string[] ExcludeFilter { get; set; }

        public override bool ExecuteTask() {
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
                List<PathSpec> directoriesToArchive = ConstructPathSpecs(DirectoriesToArchive, OutputArchives, ExcludeFilter, Log);

                Log.LogMessage($"Archive compression level set to: '{CompressionLevel}'.");

                foreach (PathSpec pathSpec in directoriesToArchive) {
                    Log.LogMessage($"Compressing directory: '{pathSpec.Location}' --> '{pathSpec.Destination}");
                }

                try {
                    if (CreateManifest) {
                        GenerateManifest(directoriesToArchive.First().Location);
                    }
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

        internal static List<PathSpec> ConstructPathSpecs(ITaskItem[] directoriesToArchive, ITaskItem[] outputArchives, string[] excludeFilter = null, TaskLoggingHelper log = null) {
            var outputs = new HashSet<PathSpec>();

            for (var i = 0; i < directoriesToArchive.Length; i++) {
                var item = directoriesToArchive[i];
                var destination = outputArchives[i];

                bool add = true;

                if (excludeFilter != null) {
                    foreach (var filter in excludeFilter) {
                        if (item.ItemSpec.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) {
                            if (log != null) {
                                log.LogMessage(MessageImportance.Low, $"Ignoring file {item.ItemSpec} as it contains filter {filter}", null);
                            }

                            add = false;
                            break;
                        }
                    }
                }

                if (add) {
                    outputs.Add(new PathSpec(item.ItemSpec, destination.ItemSpec));
                }
            }

            return outputs.ToList();
        }

        internal static void ProcessDirectories(IList<PathSpec> directoriesToArchive, CompressionLevel compressionLevel) {
            Parallel.ForEach(
                directoriesToArchive,
                new ParallelOptions { MaxDegreeOfParallelism = ParallelismHelper.MaxDegreeOfParallelism },
                directory => {
                    string temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                    ZipFile.CreateFromDirectory(directory.Location, temp, compressionLevel, false);

                    if (File.Exists(directory.Destination)) {
                        File.Delete(directory.Destination);
                    }

                    File.Move(temp, directory.Destination);
                });
        }

        internal void GenerateManifest(string folder) {
            var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

            var specificationElement = new XElement("specification");
            foreach (var file in files) {
                var directoryName = Path.GetDirectoryName(file).Split(Path.DirectorySeparatorChar).Last();
                if (directoryName == "packages" && !file.Contains("BinFiles")) {
                    specificationElement.Add(new XElement("package", new XElement("name", file)));
                } else {
                    var fileElement = new XElement("file", new XElement("name", file));

                    if (directoryName != "BinFiles") {
                        var relativePath = file.Replace(Path.Combine(folder, "BinFiles"), "", StringComparison.OrdinalIgnoreCase);
                        var relativeFolder = relativePath.Replace(Path.GetFileName(file), "", StringComparison.OrdinalIgnoreCase);
                        relativeFolder = PathUtility.TrimLeadingSlashes(relativeFolder.TrimTrailingSlashes());
                        fileElement.Add(new XElement("relativePath", relativeFolder));
                    }
                    specificationElement.Add(fileElement);
                }
            }

            var updateName = PipelineService.GetContext().BuildMetadata.ScmBranch;
            updateName = Path.GetFileName(updateName);

            XElement manifest = new XElement("package", new XAttribute("Version", "ManifestV5"),
                new XElement("id", Guid.NewGuid()),
                new XElement("name", updateName),
                new XElement("description", $"Update package for branch {updateName}"),
                new XElement("instructions", "If you have customizations, please ensure they are reapplied after importing this Update."),
                new XElement("version", "1.8.0"),
                new XElement("createDate", DateTime.Now.ToUniversalTime()),
                new XElement("isPatch", true),
                new XElement("owner",
                    new XElement("id", "00000000-0000-0000-0000-00000000000a"),
                    new XElement("name", "Aderant")
                    ),
                specificationElement
            );

            manifest.Save(Path.Combine(folder, "Manifest.xml"));
        }
    }
}
