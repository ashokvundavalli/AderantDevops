using System.Collections.Generic;

namespace Aderant.Build.MSBuild {
    public class ItemGroupItem {
        private string includeValue;
        private IDictionary<string, string> metadata = new Dictionary<string, string>();

        public ItemGroupItem(string includeValue) {
            this.includeValue = includeValue;
        }

        public string this[string key] {
            get { return metadata[key]; }
            set { metadata[key] = value; }
        }

        public string Expression {
            get { return includeValue; }
            set { includeValue = value; }
        }

        public ICollection<string> MetadataKeys {
            get { return metadata.Keys; }
        }

        public bool ContainsKey(string key) {
            return metadata.ContainsKey(key);
        }

        public bool TryGetValue(string key, out string value) {
            return metadata.TryGetValue(key, out value);
        }
    }
}