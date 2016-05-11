using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aderant.Build.PowerShell {
    public static class AssemblyResolver {

        public static void Register() {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
          //      System.Diagnostics.Debugger.Launch();

                if (args.Name.IndexOf(".resources", StringComparison.Ordinal) >= 0) {
                    return null;
                }

                if (args.Name.StartsWith("System", StringComparison.Ordinal)) {
                    return null;
                }

                System.Diagnostics.Debugger.Launch();

                return null;
            };
        }
    }
}