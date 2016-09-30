using System;

namespace Aderant.Build.Tasks.WarningProcess {
    internal class WarningEntry : IEquatable<WarningEntry> {
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

        public bool IsCopyWarning {
            get {
                if (Message != null) {
                    // C:\Program Files (x86)\MSBuild\14.0\bin\Microsoft.Common.CurrentVersion.targets(3963, 5) 
                    // Warning MSB3026 Could not copy "C\Program Files (x86)\MSBuild\14.0\bin\ru\Microsoft.Build.Engine.resources.dll" to "....\Bin\Test\ru\Microsoft.Build.Engine.resources.dll".Beginning retry 1 in 1000ms.The process cannot access the file '....\Bin\Test\ru\Microsoft.Build.Engine.resources.dll' because it is being used by another process.
                    return Message.IndexOf("MSB3026", StringComparison.Ordinal) >= 0;
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

        public bool IsTransient {
            get { return IsCopyWarning; }
        }

        public DateTime? Timestamp { get; set; }

        public string Message { get; }

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
    }
}