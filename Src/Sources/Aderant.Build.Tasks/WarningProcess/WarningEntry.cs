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
                    // Some file access warnings do not have the MSB3026 qualifier in them, so we also check for the message text.
                    return 
                        Message.IndexOf("MSB3026", StringComparison.Ordinal) >= 0 || 
                        (Message.IndexOf("warning : Got System.IO.IOException: The process cannot access the file", StringComparison.Ordinal) >= 0 &&
                        Message.IndexOf("because it is being used by another process.", StringComparison.Ordinal) > 0);
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

        public bool IsTestThreadingIssue {
            get {
                if (Message != null) {
                    return Message.IndexOf("Attempted to access an unloaded AppDomain. This can happen if the test(s) started a thread but did not stop it.", StringComparison.Ordinal) >= 0;
                }
                return false;
            }
        }

        public bool IsTestingRelated {
            get {
                if (Message != null) {
                    return Message.IndexOf("paket.dependencies doesn't contain package Aderant.Build.Analyzer in group Main", StringComparison.Ordinal) >= 0;
                }
                return false;
            }
        }

        public bool IsPackageImportRelated {
            get {
                if (Message != null) {
                    return Message.IndexOf("One or more rule references for extension point \"ExpertSystem.Menus.GlobalWebMenu\" are invalid and cannot be imported", StringComparison.Ordinal) >= 0;
                }
                return false;
            }
        }

        public bool AffectsProjectQuality {
            get { return !IsCopyWarning && !IsTestDeploymentIssue && !IsTestThreadingIssue && !IsTestingRelated && !IsPackageImportRelated; }
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