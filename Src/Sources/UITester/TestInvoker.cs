using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using UITester.Annotations;

namespace UITester {
    internal class TestInvoker : INotifyPropertyChanged {
        internal const string SqlUser = "cmsdbo";
        internal const string SqlPass = "cmsdbo";

        [NotNull]
        private readonly ParameterController parameters;
        private readonly SegregatedLogger logger;
        public TestInvoker(SegregatedLogger segregatedLogger) {
            parameters = ParameterController.Singleton;
            logger = segregatedLogger;
        }

        public static string GuessEnvironmentManifestPath(string oldValue, bool useRemoteMachineTarget, string remoteMachineName = null) {
            if (useRemoteMachineTarget) {
                return string.IsNullOrWhiteSpace(remoteMachineName) ? oldValue : string.Concat("\\\\", remoteMachineName, "\\ExpertSource\\environment.xml");
            } else { //local target
                return @"C:\ExpertShare\environment.xml";
            }
        }

        private CancellationTokenSource taskCanceler;
        private bool isTaskRunning = false;
        [NotNull]
        private readonly object isTaskRunningSyncLock = new object();
        public bool IsTaskRunning {
            get {
                lock (isTaskRunningSyncLock) {
                    return isTaskRunning;
                }
            }
            set {
                lock (isTaskRunningSyncLock) {
                    isTaskRunning = value;
                }
                OnPropertyChanged("IsTaskRunning");
            }
        }
        private CancellationToken NewCancellationTokenSource() {
            if (taskCanceler != null) {
                CancelAllTasks();
            }
            taskCanceler = new CancellationTokenSource();
            return taskCanceler.Token;
        }
        
        public void Setup(string environmentManifestPath, bool useLocalTestSource, IEnumerable<string> dllNames, bool backupDb, bool useRemoteMachineTarget, string remoteMachineName = null) {
            CancellationToken cancellationToken = NewCancellationTokenSource();
            IsTaskRunning = true;
            Task.Run(
                () => {
                    Action<string> appendTestRunLog = null;
                    if (logger != null) {
                        logger.ClearTestRunLog();
                        logger.ClearGetFrameworkLog();
                        logger.ClearGetTestsLog();
                        logger.ClearSqlLog();
                        appendTestRunLog = logger.AppendTestRunLog;
                    }
                    AppendLog("Preparing your environment for automation tests...\n{Backup DB, Get framework, Get tests, Provision DB}", appendTestRunLog);
                    bool hasRemoteMachineName = !string.IsNullOrWhiteSpace(remoteMachineName);
                    bool hasEnvironmentManifestPath = !string.IsNullOrWhiteSpace(environmentManifestPath);
                    if (!hasEnvironmentManifestPath) {
                        AppendLog("Test run FAILED: You need to provide a manifest for your environment.", appendTestRunLog);
                    }
                    if (useRemoteMachineTarget && !hasRemoteMachineName) {
                        AppendLog("You need to specify a remote machine.", appendTestRunLog);
                    }
                    if (hasEnvironmentManifestPath && (!useRemoteMachineTarget || hasRemoteMachineName)) {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!File.Exists(environmentManifestPath)) {
                            AppendLog("Test run aborted: Unable to access the environment manifest. " + environmentManifestPath, appendTestRunLog);
                            IsTaskRunning = false;
                            return;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        EnvironmentManifestFavourites environment = new EnvironmentManifestFavourites(environmentManifestPath);
                        string dbBackupPath = Path.Combine(GetTempDirectory(), "AutomationDbBackup.bak");
                        List<Task> prepForProvisionDb = new List<Task>();
                        if (backupDb) {
                            prepForProvisionDb.Add(Task.Run(() => BackupDb(environment, dbBackupPath, true, cancellationToken), cancellationToken));
                        }
                        Task<string> getSourcePath = WorkOutTestSourcePath(useLocalTestSource);
                        getSourcePath.Wait();
                        string testSourceDirectory = getSourcePath.Result;
                        string testTargetDirectory = null;
                        if (useRemoteMachineTarget) {
                            testTargetDirectory = string.Concat("\\\\", remoteMachineName.Trim(), "\\ExpertSource\\ExpertSource");
                        } else {
                            testTargetDirectory = useLocalTestSource ? testSourceDirectory : environment.SourcePath;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(testTargetDirectory) || !Directory.Exists(testTargetDirectory)) {
                            AppendLog("Cannot seem to access the test directory (does it exist?): " + testTargetDirectory ?? "NULL", appendTestRunLog);
                            IsTaskRunning = false;
                            return;
                        }
                        string sqlScriptPath = Path.Combine(testTargetDirectory, "Resources\\ProvisionDB.sql");
                        Task getTestTask = null;
                        List<Task> setupTasks = new List<Task> {
                            Task.Run(
                                () => GetFramework(parameters, testTargetDirectory, logger, cancellationToken),
                                cancellationToken)
                        };
                        if (useLocalTestSource) {
                            AppendLog("Please build the tests locally.", appendTestRunLog);
                            Task buildTestTask = Task.Run(() => { return; }); //Consider loading the aderant profile and building the tests.
                            if (useRemoteMachineTarget) {
                                getTestTask = buildTestTask.ContinueWith((x) => GetTestBinaries(testSourceDirectory, testTargetDirectory, logger, cancellationToken), cancellationToken);
                            } else {
                                getTestTask = buildTestTask; //Just wait for them to finish building.
                            }
                        } else {
                            //get test from drop
                            getTestTask = Task.Run(() => GetTestBinaries(testSourceDirectory, testTargetDirectory, logger, cancellationToken), cancellationToken);
                        }
                        //Import packages for workflow tests?
                        prepForProvisionDb.Add(getTestTask);
                        setupTasks.Add(Task.WhenAll(prepForProvisionDb).ContinueWith((x) => ProvisionDatabase(sqlScriptPath, environment, true, true, logger, cancellationToken), cancellationToken));
                        dllNames = dllNames.Where(i => i != null).Select(i => Path.Combine(testTargetDirectory, i));
                        Task.WhenAll(setupTasks).ContinueWith(
                            (x) => {
                                RunTests(testTargetDirectory, dllNames, logger, cancellationToken);
                            },
                            cancellationToken).ContinueWith(
                                (x) => {
                                    if (backupDb) {
                                        RestoreDb(environment, dbBackupPath, true, cancellationToken);
                                    }
                                    IsTaskRunning = false;
                                },
                                cancellationToken);
                    } else {
                        IsTaskRunning = false;
                    }
                });
        }

        public async Task<string> WorkOutTestSourcePath(bool useLocalTests) {
            Task<string> worker = new Task<string>(
                () => {
                    string sourcePath = null;
                    if (useLocalTests && !string.IsNullOrWhiteSpace(parameters.BranchModulesDirectory)) {
                        sourcePath = Path.Combine(parameters.BranchModulesDirectory, "Tests.UIAutomation\\bin\\Test");
                    } else {
                        string dropPathForModule = null;
                        string expertManifestPath = Path.Combine(parameters.PackageScriptsDirectory, "ExpertManifest.xml");
                        if (!string.IsNullOrWhiteSpace(expertManifestPath)) {
                            dropPathForModule = GetDropPathFromExpertManifest(XDocument.Load(expertManifestPath), "Tests.UIAutomation", parameters);
                        } else {
                            dropPathForModule = "\\\\na.aderant.com\\expertsuite\\releases\\803x\\Tests.UIAutomation\\1.8.0.0";
                            logger.AppendGetTestsLog(string.Concat("Unable to open expert manifest at [", expertManifestPath, "] so assuming the drop is: ", dropPathForModule));
                        }
                        sourcePath = GetPathToLatestSuccessfulBuild(null, dropPathForModule, logger.AppendGetTestsLog, true);
                    }
                    return sourcePath;
                });
            worker.Start();
            await worker;
            return worker.Result;
        }

        private static void GetTestBinaries(string testSourcePath, string targetPath, SegregatedLogger logger, CancellationToken cancellationToken) {
            Action<string> addToLog = null;
            if (logger != null) {
                addToLog = logger.AppendGetTestsLog;
            }
            CheckCancel(cancellationToken, addToLog);
            if (string.IsNullOrWhiteSpace(testSourcePath)) {
                logger.AppendGetTestsLog("Unable to get test binaries: Source Path is null.");
                return;
            }
            //Consider If it is not a network share path, but the drive letter of both source and target path are the same do a hardlink.
            AppendLog(string.Concat("Getting test binaries:\nfrom [", testSourcePath, "]\nto   [", targetPath, "]"), addToLog);
            CopyDirectory(testSourcePath, targetPath, addToLog, cancellationToken);
            AppendLog("Finished.", addToLog);
        }

        private static bool CopyDirectory(string sourceDirectory, string targetDirectory, Action<string> addToLog, CancellationToken cancellationToken) {
            CheckCancel(cancellationToken, addToLog);
            List<string> failedCopies = new List<string>();
            var sourceFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            //BUG: Need to create sub directories.
            foreach (string inFile in sourceFiles) {
                CheckCancel(cancellationToken, addToLog);
                if (!TryCopy(inFile, sourceDirectory, targetDirectory, addToLog, false)) {
                    failedCopies.Add(inFile);
                }
            }
            bool atLeastOneFailed = false;
            foreach (string inFile in failedCopies) { //this is the retry. We log if it fails a second time.
                CheckCancel(cancellationToken, addToLog);
                if (!TryCopy(inFile, sourceDirectory, targetDirectory, addToLog, true)) {
                    atLeastOneFailed = true;
                }
            }
            return atLeastOneFailed;
        }

        private static bool TryCopy(string sourceFilePath, string sourceDirectoryPath, string targetDirectoryPath, Action<string> addToLog, bool logFailures, bool onlyLogFailures = false) {
            //Could also shell out to robocopy
            string relativePath = StringSubtract(sourceFilePath, sourceDirectoryPath);
            string finalDestination = Path.Combine(targetDirectoryPath, relativePath);
            try {
                string directoryName = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrWhiteSpace(directoryName)) {
                    string createDirectory = Path.GetDirectoryName(finalDestination);
                    if (!string.IsNullOrWhiteSpace(createDirectory) && !Directory.Exists(createDirectory)) {
                        if (!string.IsNullOrWhiteSpace(createDirectory)) {
                            Directory.CreateDirectory(createDirectory);
                        } else {
                            AppendLog(string.Concat("Failed to copy [", sourceFilePath, "] to [", finalDestination, "] because I could not create: ", createDirectory), addToLog);
                        }
                    }
                }
                File.Copy(sourceFilePath, finalDestination, true);
                if (!onlyLogFailures) {
                    AppendLog("Copying: " + relativePath, addToLog);
                }
            } catch (Exception ex) {
                if (logFailures) {
                    AppendLog(string.Concat("Failed to copy [", relativePath, "] ", ex.Message), addToLog);
                }
                return false;
            }
            return true;
        }

        private static string GetPathToLatestSuccessfulBuild(string buildScriptsDirectory, string pathToModuleAssemblyVersion, Action<string> addToLog, bool getTestBinariesInstead=false) {
            try {
                if (string.IsNullOrWhiteSpace(buildScriptsDirectory)) {
                    buildScriptsDirectory = ParameterController.Singleton.BuildScriptsDirectory;
                }
                PowerShell psInstance = PowerShell.Create();
                psInstance.AddScript(Path.Combine(buildScriptsDirectory, "Build-Libraries.ps1"));
                psInstance.Invoke();
                psInstance.AddCommand("global:PathToLatestSuccessfulBuild");
                psInstance.AddParameter("pathToModuleAssemblyVersion", pathToModuleAssemblyVersion);
                var results = psInstance.Invoke();
                string lastSuccessfulBuild = results.FirstOrDefault().ToString();
                if (getTestBinariesInstead) {
                    if (lastSuccessfulBuild.EndsWith("Module")) {
                        lastSuccessfulBuild = Path.Combine(lastSuccessfulBuild.Substring(0, lastSuccessfulBuild.LastIndexOf('\\')), "Test");
                    }
                }
                return lastSuccessfulBuild;
            } catch (Exception ex) {
                AppendLog(string.Concat("Unable to get path to latest successful build: ", ex.ToString()), addToLog);
            }
            return null;
        }

        public void BackupDb(EnvironmentManifestFavourites environment, string filePath, bool displayCounts, CancellationToken cancellationToken) {
            Action<string> appendDatabaseManagementLog = null;
            if (logger != null) {
                logger.ClearDatabaseManagementLog();
                appendDatabaseManagementLog = logger.AppendDatabaseManagementLog;
                AppendLog("Backing up your database to: " + filePath, appendDatabaseManagementLog);
            }
            string backupSql = string.Concat("BACKUP DATABASE [", environment.DatabaseName, "] TO DISK=N'", filePath, "'\nWITH COPY_ONLY, NOFORMAT, INIT, SKIP, NOREWIND, NOUNLOAD, STATS=10");
            ExecuteSql(backupSql, environment, displayCounts, true, appendDatabaseManagementLog, cancellationToken, null, null, true);
        }

        public void RestoreDb(EnvironmentManifestFavourites environment, string filePath, bool displayCounts, CancellationToken cancellationToken) {
            Action<string> appendDatabaseManagementLog = null;
            if (logger != null) {
                appendDatabaseManagementLog = logger.AppendDatabaseManagementLog;
                AppendLog("Restoring your database from: " + filePath, appendDatabaseManagementLog);
            }
            string[] sqlCommands = {
                "USE [master]\nGO\n",
                "IF EXISTS (select * from sys.databases where name = '", environment.DatabaseName, "') BEGIN\n",
                "PRINT 'Dropping existing DB'",
                "\tALTER DATABASE [", environment.DatabaseName, "] SET  SINGLE_USER WITH ROLLBACK IMMEDIATE\n\tDROP DATABASE [", environment.DatabaseName, "]\nEND\nGO\n",
                "PRINT 'Reading DB from disk'",
                "RESTORE DATABASE [", environment.DatabaseName, "] FROM DISK =N'", filePath, "' WITH FILE = 1, STATS = 10\nGO\n",
                "USE [", environment.DatabaseName, "]\nEXEC dbo.sp_changedbowner @loginame = N'cmsdbo', @map = false\nGO\n",
                "ALTER DATABASE [", environment.DatabaseName, "] SET new_broker WITH ROLLBACK IMMEDIATE\nGO\n",
                "ALTER DATABASE [", environment.DatabaseName, "] SET trustworthy on WITH ROLLBACK IMMEDIATE\nGO\n",
                "ALTER DATABASE [", environment.DatabaseName, "] SET RECOVERY SIMPLE;\nGO\n"
            };
            string restoreSql = string.Concat(sqlCommands);
            ExecuteSql(restoreSql, environment, displayCounts, true, appendDatabaseManagementLog, cancellationToken, null, null, true, true);
        }

        private static string StringSubtract(string fullPath, string beginningPath) {
            if (!string.IsNullOrWhiteSpace(beginningPath) && !string.IsNullOrWhiteSpace(fullPath) && fullPath.StartsWith(beginningPath)) {
                string returnItem = fullPath.Substring(beginningPath.Length + 1);
                return returnItem;
            }
            return fullPath;
        }

        private void ProvisionDatabase(string sqlScriptPath, EnvironmentManifestFavourites environment, bool displayCounts, bool displayPrintOutput, SegregatedLogger logger, CancellationToken cancellationToken) {
            if (environment == null) {
                throw new ArgumentNullException("environment");
            }
            Action<string> addToLog = null;
            if (logger != null) {
                addToLog = logger.AppendSqlLog;
            }
            CheckCancel(cancellationToken, addToLog);
            string sqlFileContents;
            try {
                sqlFileContents = File.ReadAllText(sqlScriptPath);
            } catch (Exception ex) {
                AppendLog(string.Concat("Unable to read given SQL File [", sqlScriptPath, "]:\n", ex.ToString()), addToLog);
                return;
            }
            ExecuteSql(sqlFileContents, environment, displayCounts, displayPrintOutput, addToLog, cancellationToken);
        }
        
        [NotNull]
        private string GetTempDirectory() {
            try {
                if (!Directory.Exists(@"C:\Temp")) {
                    Directory.CreateDirectory(@"C:\Temp");
                }
                return @"C:\Temp";
            } catch (IOException) {
                return Path.GetTempPath();
            }
        }

        private void ExecuteSql(string sqlStatements, EnvironmentManifestFavourites environment, bool displayCounts, bool displayPrintOutput, Action<string> addToLog, CancellationToken cancellationToken, string user = null, string pass = null, bool useIntegratedSecurity = false, bool useMaster = false) {
            CheckCancel(cancellationToken, addToLog);
            string connectionString = "server=tcp:" + environment.DatabaseServerInstance +
                                   "; database=" + (useMaster ? "master" : environment.DatabaseName) +
                                   "; connection timeout=" + 3600;
            if (useIntegratedSecurity) {
                connectionString += "; Trusted_Connection=True";
            } else {
                connectionString += "; user id=" + (string.IsNullOrWhiteSpace(user) ? SqlUser : user) +
                                    "; password=" + (string.IsNullOrWhiteSpace(pass) ? SqlPass : pass);
            }
            SqlInfoMessageEventHandler infoMessageHandler = (o, args) => {
                if (addToLog != null && args != null) {
                    addToLog(args.Message);
                }
            };
            StatementCompletedEventHandler statementCompletedHandler = (o, args) => {
                if (addToLog != null && args != null) {
                    addToLog(string.Concat(args.RecordCount, " records effected"));
                }
            };
            using (SqlConnection connection = new SqlConnection(connectionString)) {
                using (SqlCommand command = connection.CreateCommand()) {
                    CancelEventHandler commandCancelHandler = (sender, args) => {
                        try {
                            command.Cancel();
                        } catch (Exception ex) {
                            AppendLog(string.Concat("Failed to cancel the SQL command:\n", ex.ToString()), addToLog);
                        }
                    };
                    TasksCancelled += commandCancelHandler;
                    try {
                        if (displayCounts) {
                            command.StatementCompleted += statementCompletedHandler;
                        }
                        if (displayPrintOutput) {
                            connection.InfoMessage += infoMessageHandler;
                        }
                        connection.Open();
                        CheckCancel(cancellationToken, addToLog);
                        IEnumerable<string> splitOnGo = Regex.Split(sqlStatements, "\\s[Gg][Oo]\\s", RegexOptions.Multiline);
                        splitOnGo = splitOnGo.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim(null));
                        foreach (string statement in splitOnGo) {
                            if (statement.Contains("RESTORE") || statement.Contains("BACKUP")) {
                                command.CommandTimeout = 600;
                            }
                            CheckCancel(cancellationToken, addToLog);
                            command.CommandText = statement;
                            command.ExecuteNonQuery();
                        }
                    } catch (Exception ex) {
                        if (addToLog != null) {
                            if (ex.Message.Contains("Operation cancelled by user.")) {
                                AppendLog("Operation cancelled by user.", addToLog);
                            } else {
                                AppendLog(string.Concat("SQL Failure:\n", ExceptionToString(ex)), addToLog);
                            }
                        }
                    } finally {
                        if (displayCounts) {
                            command.StatementCompleted -= statementCompletedHandler;
                        }
                        if (displayPrintOutput) {
                            connection.InfoMessage -= infoMessageHandler;
                        }
                        TasksCancelled -= commandCancelHandler;
                    }
                }
            }
            AppendLog("Finished.", addToLog);
        }

        private static void AppendLog(string lines, Action<string> logAction) {
            if (logAction != null) {
                logAction(lines);
            }
        }

        private static string GetDropPathFromExpertManifest(XDocument expertManifest, string moduleName, ParameterController parameters) {
            if (parameters == null) {
                parameters = ParameterController.Singleton;
            }
            if (expertManifest == null || moduleName == null || parameters == null) {
                return null;
            }
            if (string.IsNullOrWhiteSpace(parameters.BranchServerDirectory)) {
                throw new Exception("You need to provide the branch server directory");
            }
            string dropPath = parameters.BranchServerDirectory;
            XElement moduleElement = expertManifest.XPathSelectElement("ProductManifest/Modules/Module[@Name=\"" + moduleName + "\"]");
            if (moduleElement == null) {
                throw new Exception(string.Concat("Could not find ", moduleName, "in the expert manifest"));
            }
            XAttribute pathAttribute = moduleElement.Attribute("Path");
            if (pathAttribute != null) {
                dropPath = pathAttribute.Value;
            }
            XAttribute assemblyVersionAttribute = moduleElement.Attribute("AssemblyVersion");
            if (assemblyVersionAttribute == null) {
                throw new Exception(string.Concat("Could not find AssemblyVersion for the module ", moduleName, " in the expert manifest."));
            }
            string frameworkVersion = assemblyVersionAttribute.Value;
            return Path.Combine(dropPath, moduleName, frameworkVersion);
        }

        private static void GetFramework(ParameterController parameters, string testTargetDirectory, SegregatedLogger logger, CancellationToken cancellationToken) {
            //Consider seeing if framework already exists before copying it in again.
            //consider option of getting framework binaries from local disk vs drop
            Action<string> addToLog = null;
            try {
                if (logger != null) {
                    addToLog = logger.AppendGetFrameworkLog;
                }
                CheckCancel(cancellationToken, addToLog);
                if (string.IsNullOrWhiteSpace(testTargetDirectory) || !Directory.Exists(testTargetDirectory)) {
                    if (logger != null) {
                        logger.AppendGetFrameworkLog("The target directory for the tests does not seem to exist: "+testTargetDirectory);
                    }
                    return;
                }
                string expertManifestPath = Path.Combine(parameters.PackageScriptsDirectory, "ExpertManifest.xml");
                string uiFrameworkDropLocation;
                CheckCancel(cancellationToken, addToLog);
                if (File.Exists(expertManifestPath)) {
                    uiFrameworkDropLocation = GetDropPathFromExpertManifest(XDocument.Load(expertManifestPath), "UIAutomation.Framework", parameters);
                } else {
                    uiFrameworkDropLocation = "\\\\na.aderant.com\\packages\\Infrastructure\\Automation\\UIAutomation\\UIAutomation.Framework\\5.3.1.0";
                    AppendLog(string.Concat("We cannot find your ExpertManifest at [", expertManifestPath, "], so we are assuming the drop location is: ", uiFrameworkDropLocation), addToLog);
                }
                CheckCancel(cancellationToken, addToLog);
                string latestSuccessfulBuild = GetPathToLatestSuccessfulBuild(parameters.BuildScriptsDirectory, uiFrameworkDropLocation, addToLog);
                AppendLog(string.Concat("Getting UI Automation testing framework:\nfrom [", latestSuccessfulBuild, "]\nto [", testTargetDirectory, "]"), addToLog);
                CheckCancel(cancellationToken, addToLog);
                CopyDirectory(latestSuccessfulBuild, testTargetDirectory, addToLog, cancellationToken);
                AppendLog("Finished.", addToLog);
            } catch (Exception ex) {
                AppendLog(ExceptionToString(ex), addToLog);
            }
        }
        
        private static void CheckCancel(CancellationToken cancellationToken, Action<string> addToLog) {
            if (cancellationToken != null) {
                if (cancellationToken.IsCancellationRequested) {
                    AppendLog("Canceled.", addToLog);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void RunTests(string testBinaryPath, IEnumerable<string> dllNames, SegregatedLogger logger, CancellationToken cancellationToken, string testSettings = null) {
            Action<string> addToLog = null;
            if (logger != null) {
                addToLog = logger.AppendTestRunLog;
            }
            string testTool = null;
            CheckCancel(cancellationToken, addToLog);
            if (File.Exists(@"C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe")) {
                testTool = @"C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";
            } else if (File.Exists(@"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe")) {
                testTool = @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";
            } else {
                AppendLog("ERROR: unable to run tests as vstest.console.exe cannot be found", addToLog);
                return;
            }
            ProcessStartInfo startInfo = new ProcessStartInfo {
                Arguments = string.Concat("/logger:trx /InIsolation ", string.Join(" ", dllNames)),
                CreateNoWindow = true,
                FileName = testTool,
                LoadUserProfile = false,
                UseShellExecute = false,
                WorkingDirectory = testBinaryPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            Process testRunner = new Process { StartInfo = startInfo };
            CancelEventHandler processKiller = (sender, args) => {
                testRunner.Kill(); //BUG: does not kill all spawned processes (like expert ones)
                AppendLog("Killing the vstest.console.exe process", addToLog);
            };
            DataReceivedEventHandler outputDataReceivedHandler = (sender, args) => {
                if (args != null) {
                    AppendLog(args.Data, addToLog);
                }
            };
            try {
                if (addToLog != null) {
                    testRunner.OutputDataReceived += outputDataReceivedHandler;
                    testRunner.ErrorDataReceived += outputDataReceivedHandler;
                }
                CheckCancel(cancellationToken, addToLog);
                TasksCancelled += processKiller;
                testRunner.Start();
                testRunner.BeginErrorReadLine();
                testRunner.BeginOutputReadLine();
                testRunner.WaitForExit();
            } finally {
                if (addToLog != null) {
                    testRunner.OutputDataReceived -= outputDataReceivedHandler;
                    testRunner.ErrorDataReceived -= outputDataReceivedHandler;
                }
                TasksCancelled -= processKiller;
            }
        }

        public static string ExceptionToString(Exception ex) {
            try {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(ex.ToString());
                sb.AppendLine("HResult: " + ex.HResult + " HelpLink: " + ex.HelpLink);
                sb.AppendLine("DataDictionary:");
                foreach (object key in ex.Data.Keys) {
                    if (key != null) {
                        sb.AppendLine(key + ": " + ex.Data[key]);
                    }
                }
                return sb.ToString();
            } catch (Exception newEx) {
                return string.Concat("Unable to convert exception into string: ", newEx.ToString());
            }
        }

        /*private string CreateTestSettings(IDictionary<string, string> selectionCriteria) {
            string originalDoc = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestSettings name=""RemoteTarget"" id=""d2cb272c-d187-49b7-82c4-ab24590de6d5"" xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <Description>This is autogenerated from UI Test Runner.</Description>
  <Deployment>
    <DeploymentItem filename=""..\TestBinaries\ExpertSource\log4net.config"" />
    <DeploymentItem filename=""..\TestBinaries\ExpertSource\Resources\"" outputDirectory=""Resources""/>
  </Deployment>
  <RemoteController name=""vmexpdevb306"" />
  <Execution location=""Remote"">
    <Timeouts runTimeout=""7200000"" testTimeout=""600000"" />
    <TestTypeSpecific>
      <UnitTestRunConfig testTypeId=""13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b"">
        <AssemblyResolution>
          <TestDirectory useLoadContext=""true"" />
        </AssemblyResolution>
      </UnitTestRunConfig>
    </TestTypeSpecific>
    <AgentRule name=""Main"">
      <SelectionCriteria>
      </SelectionCriteria>
    </AgentRule>
  </Execution>
  <Properties />
</TestSettings>";
            XDocument document = XDocument.Load(originalDoc);
            XElement selectionNode = document.Elements("SelectionCriteria").FirstOrDefault();
            foreach (string key in selectionCriteria.Keys) {
                selectionNode.Add(new XElement{});
            }
        }
*/
        private event CancelEventHandler TasksCancelled;
        // This fires the TasksCancelled event and cancels the CancellationToken
        public void CancelAllTasks() {
            CancelEventHandler handler = TasksCancelled;
            if (handler != null) {
                handler(this, new CancelEventArgs(true));
            }
            if (taskCanceler != null) {
                taskCanceler.Cancel();
            }
            IsTaskRunning = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var handler = PropertyChanged;
            if (handler != null) {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
