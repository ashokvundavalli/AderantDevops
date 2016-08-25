using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aderant.Build.Tasks {
    public class OrmMappingValidator : Microsoft.Build.Utilities.Task {
        private static System.Text.RegularExpressions.Regex match = new Regex("(?i)(OrmMappingDefinition)");

        public ITaskItem[] Content { get; set; }

        public ITaskItem[] Compile { get; set; }

        public ITaskItem[] None { get; set; }

        public ITaskItem[] EmbeddedResource { get; set; }

        public override bool Execute() {
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

            return !Log.HasLoggedErrors;
        }

        private bool ValidateTransformValid(ITaskItem[] compile) {
            foreach (var item in compile) {
                if (match.IsMatch(item.ItemSpec)) {
                    Log.LogError("You have an {0} which is not a valid ORM mapping file. The correct extension is .hbm.xml. Also ensure the.cs file is not checked in.", item.ItemSpec);
                    return false;
                }
            }

            return true;
        }

        private void ValidateEmbeddedResources(ITaskItem[] embeddedResources, List<ITaskItem> items) {
            if (embeddedResources != null) {
                foreach (var item in embeddedResources) {
                    if (match.IsMatch(item.ItemSpec)) {
                        return;
                    }
                }
            }

            string mappingFiles = string.Join(", ", items.Select(s => s.ItemSpec));
            Log.LogError("The following mapping templates are defined: {0} but there is not a matching file set to Embedded Resource in the project. This is almost certainly an error. Set the type of the file to Embedded Resource so it can be loaded by NHibernate.", mappingFiles);
        }

        private void GatherInputs(ITaskItem[] input, List<ITaskItem> collector) {
            if (input != null) {
                foreach (var item in input) {
                    if (match.IsMatch(item.ItemSpec)) {
                        collector.Add(item);
                    }
                }
            }
        }
    }
}