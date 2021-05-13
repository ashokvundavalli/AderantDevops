using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.Tasks {
    public class OrmMappingValidator : Microsoft.Build.Utilities.Task {
        private static Guid unitTestProjectType = Guid.Parse("3AC096D0-A1C2-E12C-1390-A8335801FDAB");

        public ITaskItem[] Content { get; set; }

        public ITaskItem[] Compile { get; set; }

        public ITaskItem[] None { get; set; }

        public ITaskItem[] EmbeddedResource { get; set; }

        public ITaskItem[] ProjectTypeGuids { get; set; }

        internal bool IsTestProject { get; set; }

        public override bool Execute() {
            SetProjectType();

            if (!IsTestProject) {
                if (!ExecuteCore()) {
                    return false;
                }
            }

            return !Log.HasLoggedErrors;
        }

        private void SetProjectType() {
            if (ProjectTypeGuids != null) {
                foreach (var taskItem in ProjectTypeGuids) {
                    Guid id;
                    if (Guid.TryParse(taskItem.ItemSpec, out id)) {
                        if (id == unitTestProjectType) {
                            IsTestProject = true;
                        }
                    }
                }
            }
        }

        private bool ExecuteCore() {
            if (Compile != null) {
                if (!ValidateTransformValid(Compile)) {
                    return false;
                }
            }

            List<ITaskItem> items = new List<ITaskItem>();

            GatherInputs(None, items);
            GatherInputs(Content, items);

            if (items.Any()) {
                ValidateEmbeddedResources(EmbeddedResource, items);
            }
            return true;
        }

        private bool ValidateTransformValid(ITaskItem[] compile) {
            foreach (var item in compile) {
                var fullPath = item.GetMetadata("FullPath");

                if (fullPath != null) {
                    if (fullPath.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0) {
                        continue;
                    }

                    if (FileNameOfItemStartsWithOrmMapping(item)) {
                        Log.LogError("{0} is not a valid ORM mapping file. The correct extension is .hbm.xml. Also ensure the .cs file is not committed.", fullPath);
                        return false;
                    }
                }
            }

            return true;
        }

        private void ValidateEmbeddedResources(ITaskItem[] embeddedResources, List<ITaskItem> items) {
            if (embeddedResources != null) {
                foreach (var item in embeddedResources) {
                    if (FileNameOfItemStartsWithOrmMapping(item)) {
                        return;
                    }
                }
            }

            string mappingFiles = string.Join(", ", items.Select(s => s.ItemSpec));
            Log.LogError("The following mapping templates are defined: {0} but there is not a matching file set to Embedded Resource in the project. This is almost certainly an error. Set the type of the file to Embedded Resource so it can be loaded by NHibernate.", mappingFiles);
        }

        private static bool FileNameOfItemStartsWithOrmMapping(ITaskItem item) {
            return item.GetMetadata("FileName").StartsWith("OrmMappingDefinition", StringComparison.OrdinalIgnoreCase);
        }

        private static void GatherInputs(ITaskItem[] input, List<ITaskItem> collector) {
            if (input != null) {
                foreach (var item in input) {
                    if (FileNameOfItemStartsWithOrmMapping(item)) {
                        collector.Add(item);
                    }
                }
            }
        }
    }
}