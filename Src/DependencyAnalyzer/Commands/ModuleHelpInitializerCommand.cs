using System;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using Lapointe.PowerShell.MamlGenerator;

namespace DependencyAnalyzer.Commands {
    [Cmdlet(VerbsData.Initialize, "AderantModuleHelp")]
    public sealed class ModuleHelpInitializerCommand : PSCmdlet {

        [Parameter(HelpMessage = "Forces help generation to run.")]
        public SwitchParameter Force { get; set; }


        protected override void ProcessRecord() {
            base.ProcessRecord();

            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string psHome = Path.Combine(system32, "WindowsPowerShell", "v1.0");
            string destinationHelpFile = Path.Combine(psHome,
                                                      "dynamic_code_module_DependencyAnalyzer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null-Help.xml");
            
            FileInfo targetFile = new FileInfo(destinationHelpFile);

            if (Force) {
                GenerateHelp(psHome, targetFile);
                return;
            }

            if (targetFile.Exists) {
                if (targetFile.LastWriteTimeUtc.Date <= DateTime.UtcNow.AddDays(-1)) {
                    GenerateHelp(psHome, targetFile);
                }
                else {
                    WriteDebug("Help is less than a day old. Not regenerating");
                    return;
                }
            }

            GenerateHelp(psHome, targetFile);
        }

        private void GenerateHelp(string psHome, FileInfo target) {
            if (Directory.Exists(psHome)) {
                Host.UI.WriteLine("Generating module help.");

                CmdletHelpGenerator.GenerateHelp(psHome, true);

                Assembly asm = Assembly.GetExecutingAssembly();
                string helpFile = string.Format("{0}.dll-help.xml", asm.GetName().Name);
                FileInfo helpFileInfo = new FileInfo(Path.Combine(psHome, helpFile));

                if (helpFileInfo.Exists) {
                    if (target.Exists) {
                        target.Delete();
                    }
                    helpFileInfo.MoveTo(target.FullName);
                }
            }
        }
    }
}
