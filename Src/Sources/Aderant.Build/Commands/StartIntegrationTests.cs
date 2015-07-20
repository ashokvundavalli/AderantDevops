using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Aderant.Build.Providers;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Commands {
    /**
     * Author: Aylwin Agraviador
     * 
     *  Start-IntegrationTests is a powershell command that allows the developer to run integration tests with ease on their local deployed machine.
     *  pos 0: True to keep database from being dropped
     *  pos 1: modulename
     **/

    [Cmdlet("Start", "IntegrationTests")]
    public class StartIntegrationTestsCommand : PSCmdlet {
        private ManualResetEvent waitHandle = new ManualResetEvent(false);
        private int testRunExitCode;
        private bool debugDBonFail = false;

        [Parameter(Mandatory = false, Position = 0)]
        public SwitchParameter PreserveDB {
            get { return debugDBonFail; }
            set { debugDBonFail = true; }
        }

        [Parameter(Mandatory = false, Position = 1)]
        public string ModuleName { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public string BranchPath { get; set; }

        [Parameter(HelpMessage = "Configures the app.config in preparation for running integration tests")]
        public SwitchParameter ConfigureOnly { get; set; }

        protected override void ProcessRecord() {
            string branchPath = ParameterHelper.GetBranchPath(BranchPath, this.SessionState);
      
            if (string.IsNullOrEmpty(ModuleName)) {
                try {
                    ModuleName = ParameterHelper.GetCurrentModuleName(null, SessionState);
                } catch (Exception) {
                }

                if (string.IsNullOrEmpty(ModuleName)) {
                    Host.UI.WriteWarningLine("No module set.");
                    return;
                }
            }

            DependencyBuilder builder = new DependencyBuilder(branchPath);
            ExpertModule module = builder.GetAllModules().FirstOrDefault(x => string.Equals(x.Name, ModuleName, StringComparison.OrdinalIgnoreCase));
            if (module == null) {
                throw new PSArgumentOutOfRangeException("ModuleName");
            }

            /** 
             * App.Config Generation - This portion creates a dynamic app.config to be used with integration testing.
             * This allows for flexible and easier testing for developers and utilizes their deployed environmental manifest.
             **/
            
            // b. Locate Existing Appconfig of Module && Optional Backup 
            string modulePath = Path.Combine(ParameterHelper.GetBranchModulesDirectory(ModuleName, this.SessionState), ModuleName);
            if (!Directory.Exists(modulePath)) {
                throw new DirectoryNotFoundException("Could not find module directory: " + modulePath);
            }

            XDocument environment = GetEnvironmentManifest();

            IntegrationTestContext testContext = new IntegrationTestContext(environment) {
                ModuleUnderTest = ModuleName,
                ModulePath = modulePath,
            };

            IntegrationTestConfigBuilder configBuilder = new IntegrationTestConfigBuilder(environment);
            XDocument appConfig = configBuilder.BuildAppConfig(testContext, Resources.IntegrationTest);
            ConfigureTestAssemblyAppConfigs(testContext, appConfig);
            EditSolutionAppConfig(modulePath, appConfig);

            if (ConfigureOnly) {
                return;
            }

            RestartApplicationServer();

            /**  
             * Backup Integration Database - This portion backs up integration test database in order to prevent errors when transactions
             * have not been correctly rolled back. Fail safe rollback. 
             **/

            var tempPath = Environment.GetEnvironmentVariable("temp", EnvironmentVariableTarget.Machine);
            string serverName = configBuilder.GetServer();
            string db = configBuilder.GetDB();
            string testDb = configBuilder.GetTestDB();

            try {
                // Backup database
                var backup = new SqlBackupDatabase(serverName, db);
                backup.Backup(tempPath, db);
                
                Host.UI.WriteLine("Database backup successfully created in: " + tempPath);

                // c. Task 2 - Restore Backup Database as new DB
                var restore = new SqlRestoreDatabase(serverName, "master");
                List<SQLDataFileEntry> filesFromBackup = new List<SQLDataFileEntry>();
                foreach (var item in backup.FilesInBackup) {
                    filesFromBackup.Add(item);
                }

                Host.UI.WriteLine("Restoring database for tests...");

                restore.Restore(backup.BackupFile, tempPath, testDb, filesFromBackup);

                /**                
                 * Run Integration Tests - This portion runs integration test against the database in order to see if build is functioning as planned. 
                 **/

                //a. Initiate Process For Integration Testing
                Host.UI.WriteLine("\n Running tests... \n");

                PrepareTestEnvironment(testContext);

                RunVsTest(testContext);

                // Block until signaled
                waitHandle.WaitOne();
            } finally {
                TearDownTestEnvironment(testContext, configBuilder, serverName, testDb, tempPath, db);
            }
        }

        private XDocument GetEnvironmentManifest() {
            string environmentManifestPath = ParameterHelper.GetBranchBinariesDirectory(this.SessionState) + @"\environment.xml";

            if (!File.Exists(environmentManifestPath)) {
                throw new FileNotFoundException("Could not find the environment file for the current branch", environmentManifestPath);
            }

            XDocument environment = XDocument.Load(environmentManifestPath);
            return environment;
        }

        private void EditSolutionAppConfig(string modulePath, XDocument appConfig) {
            var fileSystemEntries = Directory.GetFileSystemEntries(Path.Combine(modulePath, "Test", "Config"), "app.config", SearchOption.TopDirectoryOnly);

            if (fileSystemEntries.Length == 0) {
                throw new FileNotFoundException("No app.config found under Test\\Config. An template app.config must exist in the solution.", "app.config");
            }

            if (fileSystemEntries.Length == 1) {
                string appConfigFile = fileSystemEntries[0];

                Workspace workspace = TeamFoundationHelper.GetWorkspaceForItem(fileSystemEntries[0]);
                workspace.PendEdit(appConfigFile);

                appConfig.Save(appConfigFile);
            }
        }

        private void RestartApplicationServer() {
            Host.UI.WriteLine("Restarting IIS...");

            System.Diagnostics.Process resetCommand = new System.Diagnostics.Process {
                StartInfo = new ProcessStartInfo("iisreset.exe") {
                    CreateNoWindow = true,
                    ErrorDialog = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            resetCommand.Start();
        }

        private void ConfigureTestAssemblyAppConfigs(IntegrationTestContext context, XDocument appConfig) {
            string testPath = Path.Combine(context.ModulePath, "Bin", "Test");

            string[] testAssemblies = Directory.GetFileSystemEntries(testPath, "IntegrationTest*.dll", SearchOption.TopDirectoryOnly);
            foreach (string testAssembly in testAssemblies) {
                string configPath = testAssembly + ".config";

                // c. Test to see if appropriate integration test config, if appropriate backup, then overwrite
                if (File.Exists(configPath)) {
                    XDocument bak = XDocument.Load(configPath);

                    if (File.ReadLines(configPath).Any(line => line.Contains("instanceMetadataConfigurationSection"))) {
                        appConfig.Save(configPath);

                        string configurationBackup = configPath + ".bak";
                        bak.Save(configurationBackup);
                        context.AddTemporaryItem(configurationBackup);

                        Host.UI.WriteLine("App config updated: " + configPath);
                    }
                } else {
                    appConfig.Save(configPath);
                }
            }

            context.TestAssemblies = testAssemblies;
            context.TestAssemblyDirectory = testPath;
        }

        private void RunVsTest(IntegrationTestContext context) {
            string runArguments = CreateTestRunArguments(context.TestAssemblies);

            System.Threading.Tasks.Task.Run(() => {
                var info = new ProcessStartInfo("vstest.console.exe");
                info.Arguments = runArguments;
                info.WorkingDirectory = context.TestAssemblyDirectory;
                info.RedirectStandardInput = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;

                info.UseShellExecute = false;
                info.CreateNoWindow = true;

                var testProcess = new System.Diagnostics.Process { StartInfo = info };
                testProcess.EnableRaisingEvents = true;

                testProcess.OutputDataReceived += (sender, args) => {
                    if (args != null && args.Data != null) {
                        Host.UI.WriteLine(args.Data);
                    }
                };

                testProcess.ErrorDataReceived += (sender, args) => {
                    if (args != null && args.Data != null) {
                        Host.UI.WriteLine(args.Data);
                    }
                };

                testProcess.Start();
                testProcess.BeginOutputReadLine();
                testProcess.BeginErrorReadLine();

                testProcess.WaitForExit();

                testRunExitCode = testProcess.ExitCode;

                // Alert PowerShell host we are done
                waitHandle.Set();
            });
        }

        private void PrepareTestEnvironment(IntegrationTestContext context) {
            var directories = new List<KeyValuePair<string, string>>();
            directories.Add(new KeyValuePair<string, string>(Path.Combine(context.TestAssemblyDirectory, "Dependencies"), Path.Combine(context.ModulePath, "Dependencies")));
            directories.Add(new KeyValuePair<string, string>(Path.Combine(context.TestAssemblyDirectory, "Bin"), Path.Combine(context.ModulePath, "Bin", "Module")));
            directories.Add(new KeyValuePair<string, string>(Path.Combine(context.TestAssemblyDirectory, "NetworkShare"), context.NetworkShare));

            foreach (var link in directories) {
                if (!Directory.Exists(link.Value)) {
                    throw new DirectoryNotFoundException(string.Format("Directory {0} does not exist", link.Value));
                }

                NativeUtilities.CreateSymbolicLink(link.Key, link.Value, (uint)NativeUtilities.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY);

                context.AddTemporaryItem(link.Key);
            }
        }

        private void TearDownTestEnvironment(IntegrationTestContext testContext, IntegrationTestConfigBuilder configBuilder, string serverName, string testDb, string tempPath, string db) {
            try {
                File.Copy(configBuilder.BackupInstanceFile, configBuilder.InstanceFile, true);
            } catch {
                Host.UI.WriteErrorLine("Unable to restore instance file: " + configBuilder.InstanceFile);
            }

            testContext.RemoveTemporaryItems();
            /**         
             * Restore Integration Database - This portion restores integration test database in order to prevent errors when transactions
             * have not been correctly rolled back. Fail safe rollback. Also cleans up backup folder.           
             **/

            // Restore database to original state.
            if (testRunExitCode == 0 && !debugDBonFail) { 
                SQLDropDatabase drop = new SQLDropDatabase(serverName, "master");
                drop.Drop(testDb);
                Host.UI.WriteLine("Backup database successfully dropped and/or zero test errors.");

                RemoveDatabaseFiles(tempPath, db);
            } else {
                Host.UI.WriteLine("Backup database not dropped. Greater than zero test errors.");
            }
        }

        private void RemoveDatabaseFiles(string tempPath, string db) {
            // Clean Up - delete backups, delete dir
            string[] filePaths = Directory.GetFiles(tempPath, db + ".bak");
            foreach (string filePath in filePaths) {
                try {
                    File.Delete(filePath);
                } catch (Exception e) {
                    Host.UI.WriteErrorLine(e.ToString());
                }
            }
            Host.UI.WriteLine("Cleanup Completed \nCMD executed successfully");
        }

        private static string CreateTestRunArguments(string[] testAssemblies) {
            StringBuilder sb = new StringBuilder(string.Join(" ", testAssemblies));
            sb.Append(" /Platform:x64");
            sb.Append(" /InIsolation");
            sb.Append(" /logger:Trx");

            return sb.ToString();
        }
    }

    internal class IntegrationTestContext {
        private IList<string> temporaryItems = new List<string>();

        public IntegrationTestContext(XDocument environment) {
            NetworkShare = environment.Root.Attribute("networkSharePath").Value;
        }

        public string NetworkShare { get; private set; }

        public string ModuleUnderTest { get; set; }

        public string ModulePath { get; set; }

        public string[] TestAssemblies { get; set; }

        public string TestAssemblyDirectory { get; set; }

        public void AddTemporaryItem(string item) {
            this.temporaryItems.Add(item);
        }

        public void RemoveTemporaryItems() {
            foreach (string item in temporaryItems) {
                FileAttributes attr = File.GetAttributes(item);

                if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
                    if (Directory.Exists(item)) {
                        Directory.Delete(item);
                    }
                } else {
                    if (File.Exists(item)) {
                        File.Delete(item);
                    }
                }
            }
        }
    }
}