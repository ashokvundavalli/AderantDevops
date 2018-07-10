using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class InspectItemGroup : Task {

        [Required]
        public ITaskItem[] ItemGroup { get; set; }

        public override bool Execute() {
            return !Log.HasLoggedErrors;
        }
    }

    public sealed class ExtractPropertyFromPropertyList : Task {

        [Required]
        [Output]
        public ITaskItem[] Items { get; set; }

        public string PropertyName { get; set; }

        public override bool Execute() {
            foreach (ITaskItem item in Items) {
                string metadata = item.GetMetadata(PropertyName);

                if (string.IsNullOrEmpty(metadata)) {
                    string s = item.GetMetadata("Properties");

                    if (s != null) {
                        string[] items = s.Split(';');

                        foreach (string s1 in items) {
                            var propertyNameAndValue = s1.Split('=');

                            string propertyName = propertyNameAndValue[0].Trim();
                            string propertyValue = propertyNameAndValue[1].Trim();

                            if (string.Equals(PropertyName, propertyName)) {
                                item.SetMetadata(PropertyName, propertyValue);
                            }
                        }
                    }
                }
            }
        
            return !Log.HasLoggedErrors;
        }
    }
}
