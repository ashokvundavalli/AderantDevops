using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsLifecycle.Start, "IntegrationTests")]
    public class StartIntegrationTests : PSCmdlet {
        [Parameter(Mandatory = false, Position = 0, HelpMessage = "The GLOB pattern to search for test assemblies. Defaults to *IntegrationTest*.dll")]
        public string SearchPattern { get; set; }

        [Parameter(Mandatory = false, Position = 1, HelpMessage = "The path to search for tests. Defaults to ExpertSource in your current branch.")]
        public string TestDirectory { get; set; }

        [Parameter(Mandatory = false, Position = 2, HelpMessage = "Expects x64 or x32. Defaults to x64")]
        public string Platform { get; set; }

        [Parameter(Mandatory = false, Position = 3, HelpMessage = "Defaults to trx.")]
        public string Logger { get; set; }

        private string vsTestDirectory;
        [Parameter(Mandatory = false, Position = 4, HelpMessage = "The location of your vstest.console.exe")]
        public string VsTestDirectory { get { return vsTestDirectory; } set { vsTestDirectory = value; } }


        private string FindHighestVisualStudio(int start, int end) {
            for (int i = end; i >= start; i--) {
                string touchDir = $"C:\\Program Files (x86)\\Microsoft Visual Studio {i}.0";
                if (Directory.Exists(touchDir)) {
                    touchDir = Path.Combine(touchDir, @"Common7\IDE\CommonExtensions\Microsoft\TestWindow");
                    if (Directory.Exists(touchDir)) {
                        return touchDir;
                    }
                }
            }
            WriteWarning("Could not find your visual studio installation directory. Please specify VsTestDirectory.");
            return null;
        }

        protected override void ProcessRecord() {
            base.ProcessRecord();
            if (string.IsNullOrWhiteSpace(VsTestDirectory)) {
                VsTestDirectory = FindHighestVisualStudio(4, 20); //Somthing like: @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow";   
            }
            string testToolPath = File.Exists(VsTestDirectory) ? VsTestDirectory : Path.Combine(VsTestDirectory, "vstest.console.exe");
            if (string.IsNullOrWhiteSpace(TestDirectory)) {
                TestDirectory = ParameterHelper.BranchExpertSourceDirectory(this.SessionState);  //eg: @"C:\TFS\ExpertSuite\Dev\vnext\Binaries\ExpertSource";
            }
            if (string.IsNullOrWhiteSpace(SearchPattern)) {
                SearchPattern = "*IntegrationTest*.dll";
            }
            Logger = string.IsNullOrWhiteSpace(Logger) ? "/Logger:trx" : string.Concat("/Logger:", Logger);
            Platform = string.IsNullOrWhiteSpace(Platform) ? "/Platform:x64" : string.Concat("/Platform:", Platform);
            string isolationSwitch = "/InIsolation";

            List<string> assemblies = Directory.EnumerateFiles(TestDirectory, SearchPattern, SearchOption.TopDirectoryOnly).ToList();
            StringBuilder arguments = new StringBuilder();
            WriteVerbose(Resources.FoundAssemblies);
            foreach (string assembly in assemblies) {
                WriteVerbose(Resources.DoubleSpace);
                WriteVerbose(assembly);
                arguments.Append("\"").Append(Path.Combine(TestDirectory, assembly)).Append("\" ");
            }
            arguments.Append(Logger).Append(" ").Append(Platform).Append(" ").Append(isolationSwitch);
            ProcessStartInfo processInfo = new ProcessStartInfo(testToolPath, arguments.ToString()) {
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                CreateNoWindow = false,
            };
            using (System.Diagnostics.Process process = new System.Diagnostics.Process { StartInfo = processInfo }) {
                try {
                    process.Start();
                    process.WaitForExit();
                } catch (Exception objException) {
                    WriteError(new ErrorRecord(objException, string.Empty, ErrorCategory.FromStdErr, process));
                    throw;
                }
            }
        }
    }

    

}
