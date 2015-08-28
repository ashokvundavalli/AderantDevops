using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class ConsolePackageImporter : Microsoft.Build.Utilities.Task {
        [Required]
        public string EnvironmentManifestPath { get; set; }

        [Required]
        public string ConfigurationPath { get; set; }

        public override bool Execute() {
            ConsolePackageImporterExecutor importer = new ConsolePackageImporterExecutor(new BuildTaskLogger(this));
            return importer.Execute(EnvironmentManifestPath, ConfigurationPath);
        }
       
    }
}
