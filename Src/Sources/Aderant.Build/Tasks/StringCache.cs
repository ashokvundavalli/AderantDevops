using System;
using System.Collections.Concurrent;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Used to cache expensive item groups.
    /// For example the file list from a directory
    /// </summary>
    public class StringCache : Microsoft.Build.Utilities.Task {
        private static ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool? hasValueForKey;

        [Required]
        public string Key {
            get; set;
        }

        [Output]
        public string Value {
            get {
                string value;
                if (cache.TryGetValue(Key, out value)) {
                    hasValueForKey = true;
                }

                return value;
            }
            set {
                cache[Key] = value;
            }
        }

        [Output]
        public bool HasValueForKey {
            get {
                if (hasValueForKey.HasValue) {
                    return hasValueForKey.Value;
                }

                return cache.ContainsKey(Key);
            }
        }

        public override bool Execute() {
            return !Log.HasLoggedErrors;
        }
    }
}
