using System;
using Aderant.Framework.Build;

namespace WebDependencyCsprojSynchronize {
    class Program {
        static void Main(string[] args) {
            string destinationFolder = @"C:\TFS\ExpertSuite\Dev\CaseV1\Modules\Web.Test\Dependencies";
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
