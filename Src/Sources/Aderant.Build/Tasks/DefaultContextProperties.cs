using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Extracts key properties from the context and returns them to MSBuild
    /// </summary>
    public sealed class DefaultContextProperties : ContextTaskBase {
     
        [Output]
        public string SolutionRoot { get; set; }

        [Output]
        public bool IsDesktopBuild { get; set; }

        protected override bool ExecuteTask(Context context) {            
            IsDesktopBuild = context.IsDesktopBuild;

            return !Log.HasLoggedErrors;
            
        }
    }
}
