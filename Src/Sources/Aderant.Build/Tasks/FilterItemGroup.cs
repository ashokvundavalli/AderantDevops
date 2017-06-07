using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class FilterItemGroup : Task {
        private List<ITaskItem> outputItems = new List<ITaskItem>();

        [Required]
        public ITaskItem[] ItemGroup { get; set; }

        [Output]
        public ITaskItem[] Result { get; set; }

        [Required]
        public ITaskItem[] ExcludeFilterSpec { get; set; }

        [Required]
        public string FilterType { get; set; }

        public override bool Execute() {

            if (string.Equals("FileName", FilterType, StringComparison.OrdinalIgnoreCase)) {
                Filter filter = new ExcludeFileNameFilter(ExcludeFilterSpec);

                foreach (ITaskItem item in ItemGroup) {
                    if (filter.PassesFilter(item)) {
                        outputItems.Add(item);
                    } else {
                        Log.LogMessage($"Filter: Removing {item.ItemSpec}");
                    }
                }

                Result = outputItems.ToArray();
            } else {
                Result = ItemGroup;
            }

            return !Log.HasLoggedErrors;
        }

        internal class ExcludeFileNameFilter : Filter {
            private readonly ITaskItem[] spec;

            public ExcludeFileNameFilter(ITaskItem[] spec) {
                this.spec = spec;
            }

            public override bool PassesFilter(ITaskItem item) {
                foreach (ITaskItem filterSpec in spec) {
                    if (string.Equals(item.GetMetadata("FileName") + item.GetMetadata("Extension"), filterSpec.ItemSpec)) {
                        return false;
                    }
                }

                return true;
            }
        }

        internal abstract class Filter {
            public abstract bool PassesFilter(ITaskItem item);
        }
    }
}