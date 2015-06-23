using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using MSBuild.Community.Tasks.SourceServer;

namespace Aderant.Build.Tasks {
    public sealed class SourceIndex : TfsSourceIndex {
        private readonly FileSystem fileSystem;
        private Workspace workspace;

        public SourceIndex() : this(new FileSystem()) {
        }

        internal SourceIndex(FileSystem fileSystem) {
            this.fileSystem = fileSystem;
        }

        public override bool Execute() {
            var buildEngineWrapper = new WrappedBuildEngine(BuildEngine);
            buildEngineWrapper.SuppressSrcToolExitCode = true;
            
            this.BuildEngine = buildEngineWrapper;
            
            return base.Execute();
        }

        protected override bool AddSourceProperties(SymbolFile symbolFile) {
            for (int i = symbolFile.SourceFiles.Count - 1; i >= 0; i--) {
                SourceFile sourceFile = symbolFile.SourceFiles[i];

                if (!sourceFile.File.Exists) {
                    // Checked in output from Text Templates contain metadata (# line) which tell the indexer where the .cs file is
                    // if the .cs file doesn't actually exist we can't index it.
                    symbolFile.SourceFiles.RemoveAt(i);
                    continue;
                }

                Log.LogMessage("Checking if file is generated: " + sourceFile.File.FullName, null);

                if (RemoveSourceFile(fileSystem, sourceFile)) {
                    Log.LogMessage("File {0} is generated. Removing from index set", sourceFile.File.Name);

                    symbolFile.SourceFiles.RemoveAt(i);
                }
            }

            if (symbolFile.SourceFiles.Count == 0) {
                return false;
            }

            if (workspace == null) {
                WorkspaceInfo workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(symbolFile.SourceFiles[0].File.FullName);
                workspace = workspaceInfo.GetWorkspace(new TfsTeamProjectCollection(new Uri(TeamProjectCollectionUri)));
            }

            AddPropertiesFromVersionControl(symbolFile);

            return true;
        }

        protected override bool WriteSourceIndex(SymbolFile symbolFile, string sourceIndexFile) {
            try {
                var sourceIndexStreamText = File.ReadAllText(sourceIndexFile);

                sourceIndexStreamText = SourceIndexStream.ModifySourceIndexStream(sourceIndexStreamText);

                File.WriteAllText(sourceIndexFile, sourceIndexStreamText);
            } catch (Exception ex) {
                Log.LogErrorFromException(ex, false, false, sourceIndexFile);
                throw;
            }

            return base.WriteSourceIndex(symbolFile, sourceIndexFile);
        }

        private void AddPropertiesFromVersionControl(SymbolFile symbolFile) {
            LocalVersion[][] localVersions = GetLocalVersions(symbolFile);

            foreach (SourceFile file in symbolFile.SourceFiles) {
                foreach (LocalVersion[] localVersion in localVersions) {
                    if (localVersion.Length > 0) {
                        LocalVersion itemInformation = localVersion[0];

                        string item = itemInformation.Item;

                        if (!string.IsNullOrEmpty(item)) {
                            if (string.Equals(file.File.FullName, item, StringComparison.OrdinalIgnoreCase)) {
                                string serverItem = workspace.TryGetServerItemForLocalItem(item);

                                // This information is taken and constructs the source index string
                                // C:\Builds\2\LocalTestProject\HelloWorld\src\HelloWorld\HelloWorld\Program.cs*VSTFSSERVER*/LocalTestProject/HelloWorld/HelloWorld/Program.cs*18*Program.cs
                                // This is the mapping between the server build path (the location of the source file as specified by the PDB) and the location of the source file in version control
                                file.Properties["FileName"] = file.File.Name;
                                file.Properties["Revision"] = itemInformation.Version.ToString(CultureInfo.InvariantCulture);
                                file.Properties["ItemPath"] = serverItem.Substring(1); // Trim $ 
                                file.IsResolved = true;

                                break;
                            }
                        }
                    }
                }
            }
        }

        private LocalVersion[][] GetLocalVersions(SymbolFile symbolFile) {
            return workspace.GetLocalVersions(symbolFile.SourceFiles.Select(s => new ItemSpec(s.File.FullName, RecursionType.None)).ToArray(), false);
        }

        internal static bool RemoveSourceFile(FileSystem fileSystem, SourceFile sourceFile) {
            // Generated source files don't exist in SCC so we need to remove them from the set of files to get version history for from TFS
            if (sourceFile.File.Name.IndexOf(".g.cs", StringComparison.OrdinalIgnoreCase) > 0) {
                return true;
            }

            if (sourceFile.File.Name.IndexOf(".gen.cs", StringComparison.OrdinalIgnoreCase) > 0) {
                return true;
            }

            /*  
             *  Weirdness here .. the SrcTool reports that the pdb contains .ttpre files for unexpected compile paths...
             *  For example if you run 
             *  
             *      srctool -r C:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Bin\Module\Aderant.PresentationFramework.Windows.pdb 
             * 
             * this is the output...
             * 
             * c:\tfs\ExpertSuite\Dev\integrationtests\modules\libraries.presentation\src\aderant.presentationframework.windows\grid\celltemplates\CheckBoxEditTemplate.ttpre
             * c:\expertsuite\dev\framework\modules\libraries.presentation\src\aderant.presentationframework.windows\grid\celltemplates\SearchBoxEditTemplate.ttpre
             * c:\tfs\dev\budgetingv1\modules\libraries.presentation\src\aderant.presentationframework.windows\grid\celltemplates\TextEditTemplate.ttpre         
             * c:\tfs\ExpertSuite\Dev\packaging\modules\libraries.presentation\src\aderant.presentationframework.windows\grid\celltemplates\TextEditTemplate.ttpre             
             * c:\tfs\ExpertSuite\Dev\integrationtests\modules\libraries.presentation\src\aderant.presentationframework.windows\grid\celltemplates\DateEditTemplate.ttpre
             * c:\tfs\ExpertSuite\Dev\integrationtests\modules\libraries.presentation\src\aderant.presentationframework.windows\grid\celltemplates\DurationEditTemplate.ttpre
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\CheckBoxEditTemplate.cs             
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\CustomCode\CheckBoxEditTemplate.cs
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\SearchBoxEditTemplate.cs
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\CustomCode\SearchBoxEditTemplate.cs             
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\TextEditTemplate.cs
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\CustomCode\TextEditTemplate.cs             
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\DateEditTemplate.cs
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\CustomCode\DateEditTemplate.cs             
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\DurationEditTemplate.cs
             * c:\tfs\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.PresentationFramework.Windows\Grid\CellTemplates\CustomCode\DurationEditTemplate.cs
             * */

            if (sourceFile.File.Name.EndsWith(".ttpre", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string directoryName = sourceFile.File.DirectoryName;
            string fileName = Path.GetFileNameWithoutExtension(sourceFile.File.Name);

            // Look for an obviously named text template file
            string[] content = fileSystem.Directory.GetFileSystemEntries(directoryName, fileName + "*.tt*");
            if (content.Length > 0) {
                return true;
            }

            return false;
        }
    }
}