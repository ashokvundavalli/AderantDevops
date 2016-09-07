using System;

namespace Aderant.Build.Tasks.WarningProcess {
    internal class WarningEntry : IEquatable<WarningEntry> {
        public bool Equals(WarningEntry other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }
            if (ReferenceEquals(this, other)) {
                return true;
            }
            return string.Equals(Message, other.Message, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() {
            return Message != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Message) : 0;
        }

        public static bool operator ==(WarningEntry left, WarningEntry right) {
            return Equals(left, right);
        }

        public static bool operator !=(WarningEntry left, WarningEntry right) {
            return !Equals(left, right);
        }

        public WarningEntry(string message) {
            Message = message;
        }
        public bool IsUnresolvedReference {
            get {
                if (Message != null) {
                    return Message.IndexOf("MSB3245", StringComparison.Ordinal) >= 0;
                }
                return false;
            }
        }

        public bool IsTestDeploymentIssue {
            get {
                if (Message != null) {
                    return Message.IndexOf("Test Run deployment issue", StringComparison.Ordinal) >= 0;
                }
                return false;
            }
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            if (obj.GetType() != this.GetType()) {
                return false;
            }
            return Equals((WarningEntry)obj);
        }

        public DateTime? Timestamp { get; set; }

        public string Message { get; }
    }
}