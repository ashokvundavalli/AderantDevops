using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Generates a hash of a given ItemGroup items. Metadata is not considered in the hash.
    /// <remarks>
    /// Currently uses SHA1. Not intended as a cryptographic security measure, only uniqueness between build executions.
    /// </remarks>
    /// </summary>
    public class HashItems : Microsoft.Build.Tasks.Hash {

        /// <summary>
        /// Adds the current directory of the worker node to the items to hash
        /// to produce a deterministic yet some what unique value to minimize but not eliminate generating a hash
        /// in used by another worker.
        /// </summary>
        public bool IncludeCurrentDirectory { get; set; }

        /// <summary>
        /// Execute the task.
        /// </summary>
        public override bool Execute() {
            if (IncludeCurrentDirectory && ItemsToHash != null && ItemsToHash.Length > 0) {
                var newItems = new ITaskItem[ItemsToHash.Length + 1];

                Array.Copy(ItemsToHash, newItems, ItemsToHash.Length);
                newItems[newItems.Length - 1] = new TaskItem(Directory.GetCurrentDirectory());

                ItemsToHash = newItems;
            }

            return base.Execute();
        }

    }
}