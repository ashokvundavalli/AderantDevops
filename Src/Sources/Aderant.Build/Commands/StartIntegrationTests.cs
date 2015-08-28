using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
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
        private bool debugDBonFail;
        private ILogger logger;

        [Parameter(Mandatory = false, Position = 0)]
        public SwitchParameter PreserveDb {
            get { return debugDBonFail; }
            set { debugDBonFail = true; }
        }

        [Parameter(Mandatory = false, Position = 1)]
        public string ModuleName { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public string BranchPath { get; set; }

        [Parameter(HelpMessage = "Specifies an arbitrary path that contains integration test assemblies. Cannot be used in conjunction with ModuleName", Mandatory = false, Position = 3)]
        public string TestAssemblyPath { get; set; }

        [Parameter(HelpMessage = "Configures the app.config in preparation for running integration tests", Position = 4)]
        public SwitchParameter ConfigureOnly { get; set; }

        protected override void ProcessRecord() {
            this.logger = new PowerShellLogger(Host);

            IntegrationTestContext testContext = CreateContextFromParameters();

            if (testContext == null) {
                return;
            }

            IEnumerable<IntegrationTestAssemblyConfig> assemblyConfigs = IntegrationTestAssemblyConfig.Create(testContext.TestAssemblyDirectory);
            
            if (testContext.UpdateSolutionAppConfig) {
                EditModuleSolutionAppConfig(testContext);
            }

            foreach (IntegrationTestAssemblyConfig config in assemblyConfigs) {
                config.IgnoreMissingConfigFile = true;

                try {
                    config.ConfigureAppConfig(testContext);
                } catch (Exception ex) {
                    logger.Error(string.Format("Failed to configure: {0}. Exception: {1}", config.AppConfigFile, ex.Message));
                    continue;
                }

                logger.Log("Configured " + config.AppConfigFile);

                config.SaveAppConfig();
            }

            if (ConfigureOnly) {
                return;
            }

            RestartApplicationServer();

            /**  
             * Backup Integration Database - This portion backs up integration test database in order to prevent errors when transactions
             * have not been correctly rolled back.
             **/

            IntegrationTestConfigBuilder configBuilder = new IntegrationTestConfigBuilder(FileSystem.Default, testContext.Environment);
            string tempPath = Environment.GetEnvironmentVariable("temp", EnvironmentVariableTarget.Machine);
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

        private IntegrationTestContext CreateContextFromParameters() {
            string branchPath = ParameterHelper.GetBranchPath(BranchPath, SessionState);

            if (string.IsNullOrEmpty(branchPath)) {
                throw new ArgumentException("Cannot determine current branch");
            }

            XDocument environmentManifest = ParameterHelper.GetEnvironmentManifest(Path.Combine(branchPath, "Binaries"));
            IntegrationTestContext context = new IntegrationTestContext(environmentManifest);
            
            if (!string.IsNullOrEmpty(TestAssemblyPath)) {
                context.TestAssemblyDirectory = TestAssemblyPath;

                string modulesDirectory = ParameterHelper.GetBranchModulesDirectory(null, SessionState);
                string template = Path.Combine(modulesDirectory, @"Build.Infrastructure\Src\Projects\TestsIntegration\IntegrationTest.config");

                if (File.Exists(template)) {
                    context.AppConfigTemplate = XDocument.Load(template);
                }

                return context;
            }

            if (string.IsNullOrEmpty(ModuleName)) {
                try {
                    ModuleName = ParameterHelper.GetCurrentModuleName(null, SessionState);
                } catch (Exception) {
                }

                if (string.IsNullOrEmpty(ModuleName)) {
                    Host.UI.WriteWarningLine("No module set.");
                    return null;
                }
            }

            DependencyBuilder builder = new DependencyBuilder(branchPath);
            ExpertModule module = builder.GetAllModules().FirstOrDefault(x => string.Equals(x.Name, ModuleName, StringComparison.OrdinalIgnoreCase));
            if (module == null) {
                throw new PSArgumentOutOfRangeException("ModuleName");
            }

            string modulePath = Path.Combine(ParameterHelper.GetBranchModulesDirectory(ModuleName, this.SessionState), ModuleName);
            if (!Directory.Exists(modulePath)) {
                throw new DirectoryNotFoundException("Could not find module directory: " + modulePath);
            }

            string testAssemblyDirectory = Path.Combine(modulePath, "Bin", "Test");
            context.TestAssemblyDirectory = testAssemblyDirectory;
            context.ModulePath = modulePath;

            return context;
        }

        private void EditModuleSolutionAppConfig(IntegrationTestContext context) {
            var fileSystemEntries = Directory.GetFileSystemEntries(Path.Combine(context.ModulePath, "Test", "Config"), "app.config", SearchOption.TopDirectoryOnly);

            if (fileSystemEntries.Length == 0) {
                throw new FileNotFoundException("No app.config found under Test\\Config. An template app.config must exist in the solution.", "app.config");
            }

            if (fileSystemEntries.Length == 1) {
                string appConfigFile = fileSystemEntries[0];

                Workspace workspace = TeamFoundationHelper.GetWorkspaceForItem(fileSystemEntries[0]);
                workspace.PendEdit(appConfigFile);

                IntegrationTestAssemblyConfig config = new IntegrationTestAssemblyConfig(appConfigFile, FileSystem.Default);
                config.ConfigureAppConfig(context);
                config.SaveAppConfig();

                context.AppConfigTemplate = XDocument.Parse(config.AppConfig.ToString());
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


        private void RunVsTest(IntegrationTestContext context) {
            string runArguments = CreateTestRunArguments(context.TestAssemblies);

            Task.Run(() => {
                var info = new ProcessStartInfo("vstest.console.exe");
                info.Arguments = runArguments;
                info.WorkingDirectory = context.TestAssemblyDirectory;
                info.RedirectStandardInput = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;

                info.UseShellExecute = false;
                info.CreateNoWindow = true;

                var testProcess = new System.Diagnostics.Process {StartInfo = info};
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
            if (context.ShouldConfigureAssemblyBinding) {
                var directories = new List<KeyValuePair<string, string>>();
                directories.Add(new KeyValuePair<string, string>(Path.Combine(context.TestAssemblyDirectory, "Dependencies"), Path.Combine(context.ModulePath, "Dependencies")));
                directories.Add(new KeyValuePair<string, string>(Path.Combine(context.TestAssemblyDirectory, "Bin"), Path.Combine(context.ModulePath, "Bin", "Module")));
                directories.Add(new KeyValuePair<string, string>(Path.Combine(context.TestAssemblyDirectory, "NetworkShare"), context.NetworkShare));

                foreach (var link in directories) {
                    if (!Directory.Exists(link.Value)) {
                        throw new DirectoryNotFoundException(string.Format("Directory {0} does not exist", link.Value));
                    }

                    NativeUtilities.CreateSymbolicLink(link.Key, link.Value, (uint) NativeUtilities.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY);

                    context.AddTemporaryItem(link.Key);
                }
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
}