using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.CheckInPolicy {
    [Serializable]
    public class CustomPolicy : PolicyBase {

        public override bool Edit(IPolicyEditArgs policyEditArgs) {
            return true;
        }

        public override bool CanEdit {
            get { return false; }
        }

        public override string Type {
            get { return "Aderant Check-In Policy"; }
        }

        public override string TypeDescription {
            get { return "This policy prevents users from checking in adds/deletes to the same file rather than renaming them."; }
        }

        public override string Description {
            get { return "Common check-in policy for Aderant."; }
        }

        public override PolicyFailure[] Evaluate() {
            var pendingChanges = PendingCheckin.PendingChanges.AllPendingChanges;
            var pendingAdds = pendingChanges.Where(p => p.IsAdd).ToList();
            var pendingDeletes = pendingChanges.Where(p => p.IsDelete).ToList();

            var policyFailures = new List<PolicyFailure>();
            foreach (var pendingDelete in pendingDeletes) {
                if (pendingAdds.Any(p => p.FileName == pendingDelete.FileName) || 
                    pendingAdds.Any(p => {
                        var localAddedFileContent = File.ReadAllText(p.LocalItem);
                        var localDeletedFileContent = File.ReadAllText(pendingDelete.LocalItem);
                        return localAddedFileContent == localDeletedFileContent;
                    })) {
                    var message = $"{pendingDelete.FileName} was deleted and added which will result in loss of TFS history! Please rename move the file correctly to keep the history.";
                    policyFailures.Add(new PolicyFailure(message, this));
                }
            }

            return policyFailures.ToArray();
        }
    }
}