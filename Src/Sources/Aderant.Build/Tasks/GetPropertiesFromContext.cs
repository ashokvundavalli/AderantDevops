using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Extracts key properties from the context and returns them to MSBuild
    /// </summary>
    public sealed class GetPropertiesFromContext : BuildOperationContextTask {
        // TODO: This class also sets...what name to give it?
        public string ArtifactStagingDirectory { get; set; }

        [Output]
        public bool IsDesktopBuild { get; set; }

        [Output]
        public string BuildSystemDirectory { get; set; }

        [Output]
        public string BuildFlavor { get; set; }

        [Output]
        public ITaskItem[] PropertiesToCreate { get; set; }

        public override bool ExecuteTask() {
            base.Execute();

            var context = Context;

            IsDesktopBuild = context.IsDesktopBuild;
            BuildSystemDirectory = context.BuildSystemDirectory;

            SetFlavor(context);

            if (!string.IsNullOrWhiteSpace(ArtifactStagingDirectory)) {
                context.ArtifactStagingDirectory = ArtifactStagingDirectory;
            }

            CreatePropertyCollection();

            PipelineService.Publish(context);

            return !Log.HasLoggedErrors;
        }

        private void CreatePropertyCollection() {
            List<TaskItem> taskItems = new List<TaskItem>();
            CreateTaskItems(taskItems, Context);
            CreateTaskItems(taskItems, Context.Switches);

            if (Context.Variables != null) {
                foreach (var kvp in Context.Variables) {
                    var taskItem = new TaskItem(kvp.Key);
                    taskItem.SetMetadata("Value", kvp.Value);

                    taskItems.Add(taskItem);
                }
            }

            PropertiesToCreate = taskItems.ToArray();
        }

        private void CreateTaskItems(List<TaskItem> taskItems, object o) {
            var clrProperties = o.GetType().GetProperties();
            foreach (var clrProperty in clrProperties) {
                var attributes = clrProperty.GetCustomAttributes(typeof(CreatePropertyAttribute), false);
                if (attributes.Length > 0) {
                    var value = clrProperty.GetValue(o);

                    if (value != null) {
                        var taskItem = new TaskItem(clrProperty.DeclaringType.Name + "_" + clrProperty.Name);
                        taskItem.SetMetadata("Value", value.ToString());

                        taskItems.Add(taskItem);
                    }
                }
            }
        }

        private void SetFlavor(BuildOperationContext context) {
            if (context.BuildMetadata != null) {
                if (!string.IsNullOrEmpty(context.BuildMetadata.Flavor)) {
                    BuildFlavor = context.BuildMetadata.Flavor;
                } else {
                    if (Context.Switches.Release) {
                        BuildFlavor = "Release";
                    } else {
                        BuildFlavor = "Debug";
                    }

                    context.BuildMetadata.Flavor = BuildFlavor;
                }
            }
        }
    }
}
