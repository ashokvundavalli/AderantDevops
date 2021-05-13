using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class GetCurrentProcessId : Task {

        [Output]
        public string ProcessId {
            get {
                return NativeMethods.GetCurrentProcessId().ToString(CultureInfo.InvariantCulture);
            }
        }
        public override bool Execute() {
            return true;
        }
    }
}