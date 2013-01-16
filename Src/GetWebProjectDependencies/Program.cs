using System;

namespace GetWebProjectDependencies {
    class Program {
        static void Main(string[] args) {
            string sourceFolder = @"\\na.aderant.com\expertsuite\Dev\OnTheGoV2\Web.Foundation\1.8.0.0\1.8.4692.38190\Bin\Module";
            string destinationFolder = @"D:\TFS\releases\8003\Modules\Web.Workflow\Dependencies\";
            if (args.Length >= 2) {
                sourceFolder = args[0];
                destinationFolder = args[1];
            } else {
                Console.WriteLine("Usage: GetWebProjectDependencies SourceFolder DestinationFolder");
            }

            WebPackageExtract wpe = new WebPackageExtract();
            wpe.ExtractWebPackage(sourceFolder, destinationFolder);
        }
    }
}
