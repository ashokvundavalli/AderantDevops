using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Hosting;
using Aderant.Build.Utilities;
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

        private static readonly TaskItem processId;

        static HashItems() {
            processId = new TaskItem(NativeMethods.GetCurrentProcessId().ToString(CultureInfo.InvariantCulture));
        }

        [Output]
        public bool IncludeProcessId { get; set; }

        /// <summary>
        /// Execute the task.
        /// </summary>
        public override bool Execute() {
            if (IncludeProcessId && ItemsToHash != null && ItemsToHash.Length > 0) {
                var newItems = new ITaskItem[ItemsToHash.Length + 1];

                Array.Copy(ItemsToHash, newItems, ItemsToHash.Length);
                newItems[newItems.Length-1] = processId;

                ItemsToHash = newItems;
            }

            return base.Execute();
        }
    }
}