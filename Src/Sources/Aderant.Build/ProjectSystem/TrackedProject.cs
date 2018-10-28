using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Aderant.Build.MSBuild;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    internal class TrackedProject {

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

        public static void SetPropertiesNeededForTracking(ItemGroupItem project, ConfiguredProject visualStudioProject) {
            // Stashes the data needed for TrackProject(...) in resume scenarios
            project[nameof(ConfiguredProject.ProjectGuid)] = visualStudioProject.ProjectGuid.ToString("D");
            project[nameof(ConfiguredProject.SolutionRoot)] = visualStudioProject.SolutionRoot;
            project[nameof(ConfiguredProject.OutputPath)] = visualStudioProject.OutputPath;
        }

        public static TrackedProject GetPropertiesNeededForTracking(IDictionary<string, string> item) {
            var guidValue = GetValueOrDefault(nameof(ConfiguredProject.ProjectGuid), item);
            var solutionRoot = GetValueOrDefault(nameof(ConfiguredProject.SolutionRoot), item);
            var outputPath = GetValueOrDefault(nameof(ConfiguredProject.OutputPath), item);
            
            if (string.IsNullOrEmpty(guidValue)) {
                throw new ArgumentNullException("Project Guid metadata item not present");
            }

            if (string.IsNullOrEmpty(solutionRoot)) {
                throw new ArgumentNullException("Project SolutionRoot metadata item not present");
            }

            if (string.IsNullOrEmpty(outputPath)) {
                throw new ArgumentNullException("Project OutputPath metadata item not present");
            }

            return new TrackedProject {
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
