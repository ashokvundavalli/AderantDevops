using System;

namespace Aderant.WebHooks.Model {
    internal class Contributor : IEquatable<Contributor> {
        public Guid Id { get; }
        public string UniqueName { get; protected set; }

        public string DisplayName { get; protected set; }

        public Contributor(string id, string uniqueName, string displayName)
            : this(new Guid(id), uniqueName, displayName) {
        }

        public Contributor(Guid id, string uniqueName, string displayName) {
            Id = id;
            DisplayName = displayName;
            UniqueName = uniqueName;
        }

        public bool Equals(Contributor other) {
            return other != null && Id == other.Id;
        }

        public override int GetHashCode() {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj) {
            return Equals(obj as Contributor);
        }
    }
}