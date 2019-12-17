using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.MSBuild;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    internal class OnDiskProjectInfo {

        [DataMember]
        public Guid ProjectGuid { get; set; }

        /// <summary>
        /// The full local path to the project file
        /// </summary>
        [DataMember]
        public string FullPath { get; set; }

        [DataMember]
        public string SolutionRoot { get; set; }

        [DataMember]
        public string OutputPath { get; set; }

        [DataMember]
        public bool? IsWebProject { get; set; }

        /// <summary>
        /// Stashes the data needed for TrackProject(...) in resume scenarios
        /// </summary>
        /// <param name="project"></param>
        /// <param name="visualStudioProject"></param>
        public static void SetPropertiesNeededForTracking(ItemGroupItem project, ConfiguredProject visualStudioProject) {
            project[nameof(ConfiguredProject.ProjectGuid)] = visualStudioProject.ProjectGuid.ToString("D");
            project[nameof(ConfiguredProject.SolutionRoot)] = visualStudioProject.SolutionRoot;
            project[nameof(ConfiguredProject.OutputPath)] = visualStudioProject.OutputPath;
        }

        public static OnDiskProjectInfo CreateFromSerializedValues(IDictionary<string, string> item) {
            string guidValue = GetValueOrDefault(nameof(ConfiguredProject.ProjectGuid), item);
            string solutionRoot = GetValueOrDefault(nameof(ConfiguredProject.SolutionRoot), item);
            string outputPath = GetValueOrDefault(nameof(ConfiguredProject.OutputPath), item);

            if (string.IsNullOrEmpty(guidValue)) {
                throw new ArgumentNullException("Project Guid metadata item not present");
            }

            if (string.IsNullOrEmpty(solutionRoot)) {
                throw new ArgumentNullException("Project SolutionRoot metadata item not present");
            }

            if (string.IsNullOrEmpty(outputPath)) {
                throw new ArgumentNullException("Project OutputPath metadata item not present");
            }

            return new OnDiskProjectInfo {
                ProjectGuid = new Guid(guidValue),
                OutputPath = outputPath,
                SolutionRoot = solutionRoot,
            };
        }

        private static string GetValueOrDefault(string key, IDictionary<string, string> item) {
            string value;
            item.TryGetValue(key, out value);

            return value;
        }
    }
}
