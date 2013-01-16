using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebDependencyCsprojSynchronize {
    class Program {
        static void Main(string[] args) {
            string destinationFolder = @"D:\TFS\Dev\OnTheGoV2\Modules\Web.Workflow\Dependencies";
            if (args.Length >= 1) {
                destinationFolder = args[0];
            } else {
                Console.WriteLine("Usage: WebDependencyCsprojSynchronize DependenciesFolder");
            }

            ProjectFileFolderSync sync = new ProjectFileFolderSync();
            sync.Synchronize(destinationFolder);
        }
    }
}
