using System;
using Aderant.Framework.Build;

namespace GetWebProjectDependencies {
    class Program {
        static void Main(string[] args) {
			string sourceFolder = @"\\na.aderant.com\expertsuite\Dev\SMFinancialsV1\Web.Presentation\1.8.0.0\1.8.4985.3264\Bin\Module";
			string destinationFolder = @"C:\TFS\ExpertSuite\dev\SMFinancialsV1\Modules\Web.Case\Dependencies";
            if (args.Length >= 2) {
                sourceFolder = args[0];
                destinationFolder = args[1];
            } else {
                Console.WriteLine("Usage: GetWebProjectDependencies SourceFolder DestinationFolder");
            }

			var wpe = new WebPackageExtract();
            wpe.ExtractWebPackage(sourceFolder, destinationFolder);
        }
    }
}
