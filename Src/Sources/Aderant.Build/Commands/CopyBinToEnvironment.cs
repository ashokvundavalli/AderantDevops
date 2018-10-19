using Aderant.Build.DependencyAnalyzer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.Copy, "BinToEnvironment")]
    public class CopyBinToEnvironment : PSCmdlet, IDisposable {

        //TODO: WARNING this will probably screw up anything that keeps old versions of dlls. (unless its in workflow)
        //TODO: -Build flag (or make the build pipe out the names of the modules it built and this pipe in the names of the modules you want to copy)
        //TODO: pull out the smaller classes in here into their own .cs files.
        
        #region Input Parameters

        private List<string> moduleNames;
        /// <summary>
        /// The names of the modules you want copied.
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, HelpMessage = "The module you want copied. If none is given, the current module is assumed.")]
        public List<string> ModuleNames {
            get { return moduleNames ?? (moduleNames = new List<string>()); }
            set { moduleNames = value; }
        }

        /// <summary>
        /// The name of the branch your modules are in.
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, HelpMessage = "The branch modules directory you are working in. If none is given, the current branch is assumed.")]
        public string BranchPath { get; set; }

        private bool recursive;
        /// <summary>
        /// Whether to recursively search for source files.
        /// </summary>
        [Parameter(Mandatory = false, Position = 2, HelpMessage = "Whether to recursively search for source files.")]
        public SwitchParameter Recursive {
            get { return recursive; }
            set { recursive = value; }
        }

        private bool stopServices;
        /// <summary>
        /// Whether to stop both Aderant Expert and IIS services before the copy operation.
        /// </summary>
        [Parameter(Mandatory = false, Position = 3, HelpMessage = "Stop both Aderant Expert and IIS services before the copy operation. (slow)")]
        public SwitchParameter StopServices {
            get { return stopServices; }
            set { stopServices = value; }
        }

        private bool startServices;
        /// <summary>
        /// Whether to start both IIS and Aderant Expert servies after the copy operation has completed.
        /// </summary>
        [Parameter(Mandatory = false, Position = 4, HelpMessage = "Start both IIS and Aderant Expert servies after the copy operation has completed. (slow)")]
        public SwitchParameter StartServices {
            get { return startServices; }
            set { startServices = value; }
        }

        private bool restartServices;
        /// <summary>
        /// Whether to automatically stop and start both IIS and Aderant Expert Services around the copy operation. (This is SLOW!)
        /// </summary>
        [Parameter(Mandatory = false, Position = 5, HelpMessage = "Stop before the copy, then restart both IIS and Aderant Expert services after the copy operation has completed. (slow)")]
        public SwitchParameter RestartServices {
            get { return restartServices; }
            set { restartServices = value; }
        }

        private bool veryVerbose;
        /// <summary>
        /// Print out all the found file names and their destination paths.
        /// </summary>
        [Parameter(Mandatory = false, Position = 6, HelpMessage = "Print out all the found file names and their destination paths.")]
        public SwitchParameter VeryVerbose {
            get { return veryVerbose; }
            set { veryVerbose = value; }
        }

        private bool quiet;
        /// <summary>
        /// Do not print out status and progress updates.
        /// </summary>
        [Parameter(Mandatory = false, Position = 7, HelpMessage = "Do not print out status and progress updates.")]
        public SwitchParameter Quiet {
            get { return quiet; }
            set { quiet = value; }
        }


        //public int Verbosity { get; set; }
        private bool includePdbs;
        /// <summary>
        /// Copy over the .pdb files too. Please note, this is often not required for debugging to work.
        /// </summary>
        [Parameter(Mandatory = false, Position = 8, HelpMessage = "Copy over the .pdb files too. Please note, this is often not required for debugging to work.")]
        public SwitchParameter IncludePdbs {
            get { return includePdbs; }
            set { includePdbs = value; }
        }

        private bool onlyCopyFilesWithMatchingPdb;
        //TODO What about .xml files that should not be copied?
        /// <summary>
        /// Experimental. YMMV. This will not copy any .dll or .exe files that do not have a matching .pdb file in the source dir.
        /// </summary>
        [Parameter(Mandatory = false, Position = 9, HelpMessage = "Experimental. YMMV. This will not copy any .dll or .exe files that do not have a matching .pdb file in the source dir.")]
        public SwitchParameter OnlyCopyFilesWithMatchingPdb {
            get { return onlyCopyFilesWithMatchingPdb; }
            set { onlyCopyFilesWithMatchingPdb = value; }
        }

        private bool copyLocals;
        /// <summary>
        /// Experimental. YMMV. Only copy files that do not have a matching file in the dependencies directory.
        /// </summary>
        [Parameter(Mandatory = false, Position = 10, HelpMessage = "Experimental. YMMV. Only copy files that do not have a matching file in the dependencies directory.")]
        public SwitchParameter CopyLocals {
            get { return copyLocals; }
            set { copyLocals = value; }
        }
        /// <summary>
        /// The maximum number of threads spawned during the copy operation. 1 = Use the main thread.
        /// </summary>
        [Parameter(Mandatory = false, Position = 11, HelpMessage = "The maximum number of threads spawned during the copy operation.")]
        public int MaxThreadCount { get; set; }

        private List<string> destinationPaths;
        /// <summary>
        /// A list of directory paths which contain potential destination files. "The haystacks".
        /// </summary>
        [Parameter(Mandatory = false, Position = 12, HelpMessage = "A list of directory paths which contain potential destination files. \"The haystacks\".")]
        public List<string> DestinationPaths {
            get { return destinationPaths ?? (destinationPaths = new List<string> {"C:\\ExpertShare", "C:\\AderantExpert"}); } //TODO: need to get this from somewhere smart like environment.xml? Can hardcode fallback values.
            set { destinationPaths = value; }
        }

        //EXAMPLE: sourcePath = "C:\\TFS\\ExpertSuite\\Dev\\Expenses\\Modules\\Services.Applications.AccountsPayable\\Bin\\Module";
        private List<string> sourcePaths;
        /// <summary>
        /// The directory where your source files are stored. "The needles." You can leave this blank and provide BOTH -BranchPath and -ModuleNames
        /// </summary>
        [Parameter(Mandatory = false, Position = 13, HelpMessage = "The directory where your source files are stored. \"The needles.\" You can leave this blank and provide BOTH -BranchPath and -ModuleNames")]
        public List<string> SourcePaths {
            get { return sourcePaths ?? (sourcePaths = new List<string>()); }
            set { sourcePaths = value; }
        }

        #endregion Input Parameters

        #region Internal Properties

        private ICollection<FileToCopy> sourceFileNames;
        /// <summary>
        /// The file names we are trying to copy to our environment. (The needles).
        /// </summary>
        private ICollection<FileToCopy> SourceFileNames { //these are our needles.
            get { return sourceFileNames ?? (sourceFileNames = new List<FileToCopy>()); }
            set { sourceFileNames = value; }
        }

        private IDictionary<string, ICollection<string>> destinationHaystack;
        /// <summary>
        /// The locations that our environment exists in. (The haystacks)
        /// </summary>
        private IDictionary<string, ICollection<string>> DestinationHaystack {
            get { return destinationHaystack ?? (destinationHaystack = new Dictionary<string, ICollection<string>>()); }
            set { destinationHaystack = value; }
        }

        private IDictionary<string, ICollection<FileToCopy>> filesToCopy;
        /// <summary>
        /// A list of the files to copy.
        /// </summary>
        internal IDictionary<string, ICollection<FileToCopy>> FilesToCopy {
            get { return filesToCopy ?? (filesToCopy = new Dictionary<string, ICollection<FileToCopy>>()); }
            set { filesToCopy = value; }
        }
        /// <summary>
        /// An controller for giving the user feedback.
        /// </summary>
        private OutputController messageOutput;

        #endregion Internal Properties

        #region Main Methods

        #region Main Control Methods

        protected override void ProcessRecord() {
            base.ProcessRecord();
            messageOutput = new OutputController();
            messageOutput.NewMessage += PrintOutput;
            if (Setup()) {
                CopyToDeployment();
            }
        }

        public bool Setup() {
            if (RestartServices) {
                StopServices = true;
                StartServices = true;
            }
            bool dontHaveAnySourcePaths = sourcePaths == null || !SourcePaths.Any();
            bool dontHaveABranchPath = string.IsNullOrEmpty(BranchPath);
            bool dontHaveAnyModuleNames = moduleNames == null || !ModuleNames.Any();
            
            if (dontHaveABranchPath && dontHaveAnySourcePaths) {
                try {
                    BranchPath = ParameterHelper.GetBranchModulesDirectory(null, this.SessionState);
                    dontHaveABranchPath = false;
                } catch (ArgumentException) {
                    WriteWarning("You have not provided a -BranchPath and I cannot get one from your powershell.");
                } catch (NullReferenceException) {
                    WriteWarning("You have not provided a -BranchPath and one is not selected in your current powershell state.");
                }
            }
            if (dontHaveAnyModuleNames && dontHaveAnySourcePaths) {
                try {
                    ModuleNames.Add(ParameterHelper.GetCurrentModulePath(null, this.SessionState));
                    dontHaveAnyModuleNames = false;
                } catch (ArgumentException) {
                    WriteWarning("You have not provided at least one -ModuleNames and I cannot get one from your powershell.");
                } catch (NullReferenceException) {
                    WriteWarning("You have not provided at least one -ModuleNames and one is not selected in your current powershell state.");
                }
            }
            if (dontHaveAnySourcePaths && (dontHaveABranchPath || dontHaveAnyModuleNames)) {
                WriteWarning("You need to provide either -SourcePath OR both -BranchPath and -ModuleNames.\n\tPlease note: You can have a branch and module selected in powershell and they will be used.");
                return false;
            }

            if (MaxThreadCount == 0) { //Default MaxThreadCount.
                MaxThreadCount = 5; //This is HDD hungry, not processor intensive.
            } else if (MaxThreadCount < 0) {
                MaxThreadCount = 1;
            }
            return true;
        }

        public void CopyToDeployment() {
            Stopwatch findTimer = new Stopwatch();
            if (StopServices) {
                StopAllServices();
            }
            findTimer.Start();
            //Add BranchPath and ModuleNames to SourcePaths.
            if (!string.IsNullOrEmpty(BranchPath) && moduleNames != null && ModuleNames.Any()) {
                GenerateAllSourcePaths(BranchPath, ModuleNames, SourcePaths);
            }

            SourceFileNames.Clear();

            AddToCollection(GetSourceFileNames(SourcePaths, Recursive), SourceFileNames);

            Find();
            findTimer.Stop();

            messageOutput.AddMessage(string.Format("Find took {0}ms", findTimer.ElapsedMilliseconds), MessageType.Progress);

            AsyncCopyController asyncCopyController = new AsyncCopyController {
                Messaging = messageOutput,
            };

            WaitForProcess(stopIis);
            WaitForProcess(stopEs);
            
            asyncCopyController.CopyFiles(FilesToCopy, MaxThreadCount, VeryVerbose);

            PrintOutput(this, new EventArgs());

            if (StartServices) {
                StartAllServices();
            }

            WaitForProcess(startIis);
            WaitForProcess(startEs);
        }

        #endregion Main Control Methods

        #region Main Search Methods

        private void GenerateAllSourcePaths(string branchPath, IEnumerable<string> moduleNames, ICollection<string> targetCollection) {
            foreach (string name in moduleNames) {
                if (string.IsNullOrWhiteSpace(name)) {
                    continue;
                }
                if (name.Contains("\\")) {
                    string binModulePath = Path.Combine(name, @"bin\module");
                    if (Directory.Exists(binModulePath)) {
                        targetCollection.Add(binModulePath);
                    } else {
                        var exception = new DirectoryNotFoundException($"Could not find the path [{binModulePath}] Maybe you should use -SourcePaths instead?");
                        WriteError(new ErrorRecord(exception, null, ErrorCategory.ObjectNotFound, null));
                    }
                } else {
                    string targetPath = GenerateSourcePath(branchPath, name);
                    if (Directory.Exists(targetPath)) {
                        targetCollection.Add(targetPath);
                    } else {
                        var exception = new DirectoryNotFoundException($"Could not find the path [{targetPath}] have you got the module {name} locally? Is your branch path correct?");
                        WriteError(new ErrorRecord(exception, null, ErrorCategory.ObjectNotFound, null));
                    }
                }
            }
        }

        internal void Find() {
            if (DestinationPaths.All(string.IsNullOrEmpty)) { //if none of them have a value, then complain.
                var exception = new ArgumentException("Cannot continue: You need to specify destination paths to look in.");
                WriteError(new ErrorRecord(exception, string.Empty, ErrorCategory.InvalidArgument, DestinationPaths));
                return;
            }

            PopulateHaystack();

            //Find all matching filenames.
            foreach (FileToCopy searchFile in SourceFileNames) { //foreach needle.
                string key = Path.GetFileName(searchFile.AbsoluteSourceFilePath).ToLower();
                if (key != null && DestinationHaystack.ContainsKey(key)) {
                    #region Fold pdb

                    string pdbSearchFile = IncludePdbs ? PdbFileName(searchFile.AbsoluteSourceFilePath) : null;
                    const StringComparison ignoreCase = StringComparison.InvariantCultureIgnoreCase;
                    bool weHaveSourcePdb = IncludePdbs
                                           && (searchFile.AbsoluteSourceFilePath.EndsWith(".dll", ignoreCase) || searchFile.AbsoluteSourceFilePath.EndsWith(".exe", ignoreCase)) //should we expect a pdb?
                                           && DoesExist(SourceFileNames, pdbSearchFile); //does it actually exist in source?

                    #endregion Fold pdb

                    bool isFirstDestination = true;
                    foreach (string destination in DestinationHaystack[key]) {
                        #region Fold pdb

                        if (weHaveSourcePdb) {
                            string pdbDestination = PdbFileName(destination); //create pdb for this item.
                            if (!DestinationHaystack[key].Contains(pdbDestination)) { //don't add it if it already exists.
                                FileToCopy pdbFileToCopy = new FileToCopy(searchFile.SourceLocation, pdbSearchFile) {
                                    DestinationFilePath = pdbDestination,
                                    IsAddedPdb = true
                                };
                                AddToFilesToCopy(key, pdbFileToCopy);
                            }
                        }

                        #endregion Fold pdb

                        bool copyLocalAllow = CopyLocals || !IsFileInDependencies(searchFile.AbsoluteSourceFilePath);
                        bool pdbMatchAllow = !OnlyCopyFilesWithMatchingPdb || HasMatchingPdb(searchFile.SourceFileName);

                        if (copyLocalAllow && pdbMatchAllow) {
                            if (isFirstDestination) {
                                searchFile.DestinationFilePath = destination;
                                AddToFilesToCopy(key, searchFile);
                                isFirstDestination = false;
                            } else {
                                AddToFilesToCopy(key, searchFile, destination); //This clones the items and adds the new clone, so we don't overwrite the first one.
                            }
                        }
                    }
                }
            }
            RemoveLinksToSharedBin(FilesToCopy);
        }

        #endregion Main Search Methods

        #endregion Main Methods

        #region Breakdown methods

        private void PrintOutput(object sender, EventArgs args) {
            //cannot use Write* when not in the powershell thread.
            if (!string.IsNullOrWhiteSpace(Thread.CurrentThread.Name) && Thread.CurrentThread.Name.Equals("Pipeline Execution Thread")) {
                try {
                    UserMessage message;
                    while ((message = messageOutput.Dequeue()) != null) {
                        if (message.PsErrorRecord != null) {
                            WriteError(message.PsErrorRecord);
                        }
                        switch (message.Type) {
                            case MessageType.Debug:
                                WriteDebug(message.Message);
                                break;
                            case MessageType.Warning:
                                WriteWarning(message.Message);
                                break;
                            case MessageType.Verbose:
                                WriteVerbose(message.Message);
                                break;
                            case MessageType.Error:
                                WriteError(new ErrorRecord(message.Exception, string.Empty, ErrorCategory.WriteError, message.Message));
                                break;
                            case MessageType.Info:
                                if (!Quiet) {
                                    WriteVerbose(message.Message);
                                }
                                break;
                            case MessageType.Progress:
                                //WriteProgress(new ProgressRecord(1, "a", message.Message));
                                if (!Quiet) {
                                    WriteVerbose(message.Message);
                                }
                                break;
                        }
                    }
                } catch {
                    //Cannot write message.
                }
            }
        }

        private void AddToCollection<T>(IEnumerable<T> source, ICollection<T> destination) {
            foreach (T item in source) {
                destination.Add(item);
            }
        }

        private IDictionary<string, IDictionary<string, ICollection<string>>> dependencyDirectoryCache;
        
        private bool IsFileInDependencies(string absoluteFilePath) {
            string searchName = Path.GetFileName(absoluteFilePath).ToLower();
            string dependenciesDir = FindDependenciesDirectoryFromBinFile(absoluteFilePath)?.ToLower();
            if (string.IsNullOrEmpty(dependenciesDir)) { //could not find a dependency dir for this file path.
                return false;
            }
            if (dependencyDirectoryCache == null) {
                dependencyDirectoryCache = new Dictionary<string, IDictionary<string, ICollection<string>>>();
            }
            CheckOrAddDependencyCache(dependenciesDir);
            if (dependencyDirectoryCache[dependenciesDir].ContainsKey(searchName)) {
                return true;
            }
            return false;
        }

        private void CheckOrAddDependencyCache(string dependenciesDir) {
            if (!dependencyDirectoryCache.ContainsKey(dependenciesDir)) {
                IDictionary<string, ICollection<string>> cache = new Dictionary<string, ICollection<string>>();
                dependencyDirectoryCache.Add(dependenciesDir, cache);
                string[] dependencyContents = Directory.GetFiles(dependenciesDir, "*", SearchOption.AllDirectories);
                foreach (string filePath in dependencyContents) {
                    AddFileToCache(cache, filePath);
                }
            }
        }

        private void AddFileToCache(IDictionary<string, ICollection<string>> cache, string filePath) {
            filePath = filePath.ToLower();
            string fileName = Path.GetFileName(filePath);
            if (cache.ContainsKey(fileName) && !cache[fileName].Contains(filePath)) {
                cache[fileName].Add(filePath);
            } else {
                cache[fileName] = new List<string> { filePath };
            }
        }

        private string FindDependenciesDirectoryFromBinFile(string nameOfBinOutputFile) { //TODO: should be looking inside packages too
            int index = nameOfBinOutputFile.LastIndexOf("\\Bin\\Module", StringComparison.InvariantCultureIgnoreCase);
            if (index < 0) {
                return null;
            }
            string dependenciesDir = Path.Combine(nameOfBinOutputFile.Substring(0, index), "Dependencies");
            return Directory.Exists(dependenciesDir) ? dependenciesDir : null;
        }

        private static bool DoesExist(IEnumerable<FileToCopy> collection, string path) {
            return collection.AsParallel().Any(file => file.AbsoluteSourceFilePath.Contains(path));
        }

        /// <summary>
        /// Adds a CLONE of the given <see cref="FileToCopy"/> to the FilesToCopy dictionary with the given key and destination file path.
        /// </summary>
        private void AddToFilesToCopy(string key, FileToCopy value, string destinationFilePath) {
            FileToCopy clonedValue = value.Clone();
            clonedValue.DestinationFilePath = destinationFilePath;
            AddToFilesToCopy(key, clonedValue);
        }

        /// <summary>
        /// Adds the given <see cref="FileToCopy"/> to the FilesToCopy dictionary with the given key.
        /// </summary>
        private void AddToFilesToCopy(string key, FileToCopy value) {
            if (FilesToCopy.ContainsKey(key)) {
                FilesToCopy[key].Add(value);
            } else {
                FilesToCopy.Add(key, new List<FileToCopy> { value });
            }
        }

        internal void PopulateHaystack(bool ignoreIfItHasAnyPopulation = true, bool emptyItFirst = false) { //if both of these is true, it will only ignore.
            if (ignoreIfItHasAnyPopulation && DestinationHaystack.Any()) {
                return;
            }
            if (emptyItFirst) {
                DestinationHaystack.Clear();
            }

            //Get the list of ALL files in the destination directories.
            //TODO: Can I do this parallel?
            foreach (string location in DestinationPaths.Where(i => !string.IsNullOrEmpty(i))) { //Look once in each location
                string[] allFilesInThisLocation = null;
                try {
                    allFilesInThisLocation = Directory.GetFiles(location, "*", SearchOption.AllDirectories);
                } catch (IOException ex) {
                    WriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.WriteError, location));
                    continue;
                }
                foreach (string file in allFilesInThisLocation) {
                    string key = Path.GetFileName(file).ToLower();
                    if (!string.IsNullOrEmpty(key)) {
                        if (DestinationHaystack.ContainsKey(key)) {
                            DestinationHaystack[key].Add(file.ToLower());
                        } else {
                            DestinationHaystack.Add(key, new List<string> { file.ToLower() });
                        }
                    }
                }
            }
        }

        private bool HasMatchingPdb(string fileName, bool partialMatch = true) {
            //partialMatch is to cater for directories inside sourcefile path.
            const StringComparison ignoreCase = StringComparison.InvariantCultureIgnoreCase;
            if (fileName.EndsWith(".dll", ignoreCase) || fileName.EndsWith(".exe", ignoreCase)) {
                if (partialMatch) {
                    string pdbFileName = PdbFileName(fileName);
                    return SourceFileNames.Any(i => i.AbsoluteSourceFilePath.Contains(pdbFileName));
                } else {
                    return SourceFileNames.Any(i => i.AbsoluteSourceFilePath == (PdbFileName(fileName)));
                }
            }
            return true; //It is not expected to have a matching PDB, these include things like .xml files.
        }

        private static string StringSubtract(string fullPath, string beginningPath) {
            if (fullPath.StartsWith(beginningPath)) {
                int substringIndex = beginningPath.Length > 0 && beginningPath[beginningPath.Length - 1] != '\\' ? beginningPath.Length + 1 : beginningPath.Length;
                string returnItem = fullPath.Substring(substringIndex);
                return returnItem;
            }
            return fullPath;
        }

        internal IEnumerable<FileToCopy> GetSourceFileNames(IEnumerable<string> sourcePaths, bool recursive = false) {
            List<FileToCopy> returnPaths = new List<FileToCopy>();
            foreach (var sourcePath in sourcePaths) {
                try {
                    string[] fullPathNames = Directory.GetFiles(sourcePath, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                    returnPaths.AddRange(fullPathNames.Select(fullPath => new FileToCopy(sourcePath, StringSubtract(fullPath, sourcePath))));
                } catch (IOException ex) {
                    WriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.WriteError, sourcePath));
                }
            }
            return returnPaths;
        }
        //EXAMPLE: C:\\AderantExpert\\Local\\Services\\Workflows\\Aderant.Workflow.ExpenseApproval\\1\\SharedBin\\Aderant.Firm.Library.dll
        private readonly Dictionary<string, ICollection<FileToCopy>> workflowBinaries = new Dictionary<string, ICollection<FileToCopy>>();

        private void AddToWorkflowBinaries(string key, FileToCopy value) {
            if (workflowBinaries.ContainsKey(key)) {
                workflowBinaries[key].Add(value);
            } else {
                workflowBinaries.Add(key, new List<FileToCopy> {value});
            }
        }

        private readonly Dictionary<string, bool> reparseCache = new Dictionary<string, bool>(); 

        private bool IsReparsePoint(string path) {
            //If it doesn't exist then it can't be a reparse point.
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }
            if (reparseCache.ContainsKey(path)) {
                return reparseCache[path];
            }
            bool isReparsePoint = false;
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (directoryInfo.Exists) {
                isReparsePoint = directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            } else {
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Exists) {
                    isReparsePoint = fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
                }
            }
            return reparseCache[path] = isReparsePoint;
        }

        internal void RemoveLinksToSharedBin(IDictionary<string, ICollection<FileToCopy>> ourFilesToCopy) {
            foreach (var list in ourFilesToCopy) {
                foreach (FileToCopy item in list.Value.ToList()) {
                    string[] splitDirectories = item.DestinationFilePath.Split('\\');
                    for (int i = splitDirectories.Length; i > 1; i--) {
                        if (IsReparsePoint(string.Join("\\", splitDirectories, 0, i))) {
                            list.Value.Remove(item);
                        }
                    }
                    //If we update one of these binaries then we cannot re-hydrate the workflow. Re-import the workflow to get updated binaries.
                    if (item.DestinationFilePath.ToLowerInvariant().Contains("aderantexpert\\local\\services\\workflows")) {
                        list.Value.Remove(item); 
                        AddToWorkflowBinaries(WorkflowName(item.DestinationFilePath), item);
                    }
                    //I've wrecked deployment manager on a number of occassions, so I'm skipping these too.
                    if (item.DestinationFilePath.ToLowerInvariant().Contains("aderantexpert\\install")) {
                        list.Value.Remove(item);
                    }
                    //Deployment manager injects info into exe xml config files. If we overwrite them, then our apps crash on startup.
                    if (item.SourceFileName.EndsWith(".config", StringComparison.InvariantCultureIgnoreCase)) {
                        list.Value.Remove(item);
                    }
                }
            }
            if (workflowBinaries.Count > 0) {
                messageOutput.AddMessage("I refuse to copy to workflow binaries.", MessageType.Warning);
            }
            foreach (var kvp in workflowBinaries) {
                messageOutput.AddMessage(string.Format("You will need to reimport the workflow named: {0}  if you want it's binaries updated.", kvp.Key), MessageType.Warning);
            }
        }

        private string WorkflowName(string path) {
            int start = path.IndexOf("Workflows\\", StringComparison.InvariantCultureIgnoreCase) + 10;
            int end = path.IndexOf("\\", start, StringComparison.InvariantCultureIgnoreCase);
            return path.Substring(start, end - start);
        }

        private string PdbFileName(string fileName) {
            StringComparison ignoreCase = StringComparison.InvariantCultureIgnoreCase;
            if (!(fileName.EndsWith(".dll", ignoreCase) || fileName.EndsWith(".exe", ignoreCase))) {
                messageOutput.AddMessage(string.Format("Creating a PDB filename for {0} which is not even a .dll or .exe file.", fileName), MessageType.Warning);
            }
#if DEBUG
            string path = Path.GetDirectoryName(fileName);
            String file = Path.GetFileNameWithoutExtension(fileName) + ".pdb";
            return Path.Combine(path ?? string.Empty, file);
#else
            return Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, Path.GetFileNameWithoutExtension(fileName) + ".pdb");
#endif
        }

        private string GenerateSourcePath(string branchPath, string moduleName) {
            if (!branchPath.Split('\\').Last().Equals("Modules")) {
                branchPath = Path.Combine(branchPath, "Modules");
            }
            return Combine(branchPath, moduleName, "Bin\\Module");
        }

        public static string Combine(string one, string two, string three) {
            return Path.Combine(one ?? string.Empty, two ?? string.Empty, three ?? string.Empty);
        }

        public static string Combine(string one, string two, string three, string four) {
            return Path.Combine(one, two, three, four);
        }

        #region Services Control

        private void StopExpertServices() {
            messageOutput.AddMessage("Stopping Expert Services...", MessageType.Progress);
            try {
                //ShellInvocationHelper.InvokeCommand(this, "Start-DeploymentEngine", "stop");
                stopEs = DeploymentEngine802("stop");
            } catch (Exception ex) {
                messageOutput.AddMessage("Cannot stop Expert Services.", MessageType.Error, ex);
            }
        }

        private System.Diagnostics.Process stopIis;
        private System.Diagnostics.Process startIis;
        private System.Diagnostics.Process startEs;
        private System.Diagnostics.Process stopEs;
        
        private void StopIisServices() {
            messageOutput.AddMessage("Stopping IIS Services...", MessageType.Progress);
            try {
                stopIis = System.Diagnostics.Process.Start("C:\\Windows\\System32\\iisreset.exe", "/stop");
                if (stopIis != null) {
                    stopIis.Exited += ProcessOnExited;
                }
            } catch (Exception ex) {
                messageOutput.AddMessage("Cannot stop IIS.", MessageType.Error, ex);
            }
        }

        private System.Diagnostics.Process DeploymentEngine802(string action) {
            string deploymentEngine = Path.Combine(ParameterHelper.BranchExpertSourceDirectory(this.SessionState), "DeploymentEngine.exe");
            string manifest = Path.Combine(ParameterHelper.GetBranchBinariesDirectory(SessionState), "environment.xml");
            string parameters = action + " " + manifest;
            System.Diagnostics.Process proc = System.Diagnostics.Process.Start(deploymentEngine, parameters);
            if (proc != null) {
                proc.Exited += ProcessOnExited;
            }
            return proc;
        }

        private void StartExpertServices() {
        //I could not get Start-DeploymentEngine in Aderant.psm1 to fire, so this is written specifically for Expert Version 8.0.2;
        //I recommend we convert Start-DeploymentEninge to a C# Cmdlet and use that instead.

            messageOutput.AddMessage("Starting Expert Services...", MessageType.Progress);
            WaitForProcess(startEs);
            try {
                //ShellInvocationHelper.InvokeCommand(this, "Start-DeploymentEngine", "start");
                startEs = DeploymentEngine802("start");
            } catch (Exception ex) {
                messageOutput.AddMessage("Cannot start Expert Services.", MessageType.Error, ex);
            }
        }

        private void StartIisServices() {
            messageOutput.AddMessage("Starting IIS Services...", MessageType.Progress);
            try {
                startIis = System.Diagnostics.Process.Start("C:\\windows\\system32\\iisreset.exe", "/start");
                if (startIis != null) {
                    startIis.Exited += ProcessOnExited;
                }

            } catch (Exception ex) {
                messageOutput.AddMessage("Cannot start IIS.", MessageType.Error, ex);
            }
        }

        private void WaitForProcess(System.Diagnostics.Process process) {
            if (process != null) {
                while (!process.HasExited) {
                    Thread.Sleep(200);
                }
            }
        }

        private void ProcessOnExited(object sender, EventArgs eventArgs) {
            if (sender.Equals(startIis)) {
                messageOutput.AddMessage("Finished Starting IIS Services.", MessageType.Progress);
                Unsubscribe(ref startIis);
            } else if (sender.Equals(stopIis)) {
                messageOutput.AddMessage("Finished Stopping IIS Services.", MessageType.Progress);
                Unsubscribe(ref stopIis);
            } else if (sender.Equals(startEs)) {
                messageOutput.AddMessage("Finished Starting Expert Services", MessageType.Progress);
                Unsubscribe(ref startEs);
            } else if (sender.Equals(stopEs)) {
                messageOutput.AddMessage("Finished Stopping Expert Services.", MessageType.Progress);
                Unsubscribe(ref stopEs);
            }
        }

        private void Unsubscribe(ref System.Diagnostics.Process process) {
            if (process != null) {
                process.Exited -= ProcessOnExited;
                process = null;
            }
        }

        private void StopAllServices() {
            StopExpertServices();
            StopIisServices();
        }

        private void StartAllServices() {
            StartExpertServices();
            StartIisServices();
        }

        #endregion Services Control

        public void Dispose() {
            if (stopIis != null) {
                stopIis.Exited -= ProcessOnExited;
            }
            if (startIis != null) {
                startIis.Exited -= ProcessOnExited;
            }
            if (startEs != null) {
                startEs.Exited -= ProcessOnExited;
            }
            if (stopEs != null) {
                stopEs.Exited -= ProcessOnExited;
            }
        }

        #endregion Breakdown methods


    }

    #region internal classes

    internal class AsyncCopyController {
        private ConcurrentBag<FileToCopy> failedItems;
        private ConcurrentBag<FileToCopy> FailedItems {
            get { return failedItems ?? (failedItems = new ConcurrentBag<FileToCopy>()); }
            set { failedItems = value; }
        }
        
        internal OutputController Messaging { get; set; }

        internal bool Completed { get; set; }

        #region Main Copy Methods

        internal void CopyFiles(IDictionary<string, ICollection<FileToCopy>> filesToCopy, int numberOfThreads, bool verbose = false) {
            Stopwatch copyAllTimer = new Stopwatch();

            if (verbose) {
                Messaging.AddMessage("Copying the following: ", MessageType.Verbose);
                PrintGroupByFile(filesToCopy);
            }
            ConcurrentQueue<FileToCopy> copyQueue = new ConcurrentQueue<FileToCopy>();
            //Populate Queue
            foreach (KeyValuePair<string, ICollection<FileToCopy>> list in filesToCopy) {
                foreach (FileToCopy item in list.Value) {
                    copyQueue.Enqueue(item);
                }
            }
            int totalToCopy = copyQueue.Count;
            if (totalToCopy == 0) {
                Messaging.AddMessage("Could not find anything to copy. Check the paths you have provided are correct.", MessageType.Warning);
            }
            bool tryAgain = true;
            while (tryAgain) {
                tryAgain = false;
                copyAllTimer.Start();
                if (numberOfThreads > 1 && copyQueue.Count > 1) {
                    CopyFilesAsyncThreads(copyQueue, numberOfThreads); //now that DependencyAnalyzer is built for .NET v4.0 or greater, we should be able to use the TPL or something instead.
                } else {
                    CopyFilesSync(copyQueue);
                }
                copyAllTimer.Stop();
                string failedItemsString = FailedItems.Count > 0 ? FailedItems.Count + " copy operations have failed." : $"Copied {totalToCopy} items Successfully.";
                Messaging.AddMessage($"Copy has finished in {copyAllTimer.ElapsedMilliseconds}ms. {failedItemsString}", MessageType.Progress);
                if (FailedItems.Count > 0) {
                    Messaging.AddMessage($"{FailedItems.Count} items have failed. Would you like to retry? [y/N]", MessageType.Warning);
                    ConsoleKeyInfo key = Console.ReadKey(); //TODO: need to figure out how to do this using a PSCmdlet method.
                    Console.WriteLine();
                    if (key.KeyChar == 'y' || key.KeyChar == 'Y') {
                        tryAgain = true;
                        copyQueue = new ConcurrentQueue<FileToCopy>();
                        FileToCopy failedFile;
                        while (FailedItems.TryTake(out failedFile)) {
                            copyQueue.Enqueue(failedFile);
                        }
                    }
                }
            }
            Completed = true;
        }

        internal void CopyFilesSync(ConcurrentQueue<FileToCopy> copyQueue) {
            ThreadMethod(copyQueue);
        }

        internal void CopyFilesAsyncThreads(ConcurrentQueue<FileToCopy> copyQueue, int numberOfThreads) {
            ICollection<Thread> threads = new List<Thread>();
            int poolSize = Math.Min(numberOfThreads, copyQueue.Count);
            for (int i = 0; i < poolSize; i++) {
                Thread newThread = new Thread(ThreadMethod) {
                    Name = i.ToString(CultureInfo.InvariantCulture),
                    IsBackground = true,
                };
                threads.Add(newThread);
                newThread.Start(copyQueue);
            }
            foreach (Thread thread in threads) {
                try {
                    thread.Join(); //BUG: sometimes powershell crashes here, but it is not throwing an exception.
                } catch (ThreadStateException ex) {
                    Messaging.AddMessage("Thread state exception", MessageType.Error, ex);
                } catch (ThreadInterruptedException ex) {
                    Messaging.AddMessage("Thread Interrupted Exception", MessageType.Error, ex);
                } catch (Exception ex) {
                    Messaging.AddMessage("General Exception", MessageType.Error, ex);
                }
            }
        }

        private void ThreadMethod(object sourceAndDestinationQueue) {
            ConcurrentQueue<FileToCopy> queue = sourceAndDestinationQueue as ConcurrentQueue<FileToCopy>;
            if (queue != null) {
                try {
                    FileToCopy kvp;
                    while (queue.TryDequeue(out kvp)) {
                        Messaging.AddMessage($"{Thread.CurrentThread.Name} is copying to  {kvp.DestinationFilePath}", MessageType.Debug);
                        if (CopyFileOperation(kvp)) {
                            Messaging.AddMessage($"Copy completed {kvp.DestinationFilePath}", MessageType.Progress);
                        } else {
                            FailedItems.Add(kvp);
                        }
                        
                    }
                } catch (InvalidOperationException ex) {
                    //Queue is empty.
                    Messaging.AddMessage($"Thread {Thread.CurrentThread.Name} ended with exception, probably because the queue is empty.", MessageType.Error, ex);
                    return;
                }
            } else {
                string type = sourceAndDestinationQueue?.GetType().ToString() ?? "null";
                InvalidCastException exception = new InvalidCastException("Could not convert the input parameter sourceAndDestinationQueue of type " + type + " to a ConcurrentQueue<FileToCopy>.");
                exception.Data.Add("AdditionalInfo", "private void ThreadMethod in internal class AsyncCopyController in the namespace DependencyAnalyzer.Cmdlet");
                try { //toss the exception to get stack trace.
                    throw exception;
                } catch (InvalidCastException ex) {
                    Messaging.AddMessage("Unable to continue:", MessageType.Error, ex);
                }
            }
        }

        private bool CopyFileOperation(FileToCopy filePaths) {
            if (filePaths != null) {
                try {
                    File.Copy(filePaths.AbsoluteSourceFilePath, filePaths.DestinationFilePath, true);
                    return true;
                } catch (FileNotFoundException ex) {
                    Messaging.AddMessage($"Thread {Thread.CurrentThread.Name}: {ex.Message}", MessageType.Warning);
                } catch (IOException ex) {
                    if (ex.Message.Contains("because it is being used by another process")) { //supress these as warnings rather than errors.
                        Messaging.AddMessage($"Thread {Thread.CurrentThread.Name}: {ex.Message}", MessageType.Warning);
                    } else {
                        Messaging.AddMessage($"Thread {Thread.CurrentThread.Name}: {ex.Message}", MessageType.Error, ex);
                    }
                } catch (Exception ex) {
                    Messaging.AddMessage($"Thread {Thread.CurrentThread.Name}: {ex.Message}", MessageType.Error, ex);
                }
            }
            return false;
        }

        #endregion Main Copy Methods

        internal void PrintGroupByFile(IDictionary<string, ICollection<FileToCopy>> fileNames) {
            foreach (KeyValuePair<string, ICollection<FileToCopy>> list in fileNames) {
                Messaging.AddMessage(list.Key, MessageType.Verbose); //this ignores sub dir in source path.
                foreach (FileToCopy item in list.Value) {
                    Messaging.AddMessage("  " + item.DestinationFilePath, MessageType.Verbose);
                }
            }
        }
    }

    internal class FileToCopy {
        public FileToCopy() {}

        public FileToCopy(string sourceLcoation, string relativeSourcePath) {
            this.SourceLocation = sourceLcoation;
            this.RelativeSourceFilePath = relativeSourcePath;
        }

        public FileToCopy Clone() {
            FileToCopy returnObject = new FileToCopy {
                DestinationFileName = this.DestinationFileName,
                DestinationFilePath = this.DestinationFilePath,
                IsAddedPdb = this.IsAddedPdb,
                RelativeSourceFilePath = this.RelativeSourceFilePath,
                SourceFileName = this.SourceFileName,
                SourceLocation = this.SourceLocation,
            };
            return returnObject;
        }

        public string SourceLocation { get; set; }
        public string RelativeSourceFilePath { get; set; }

        public string AbsoluteSourceFilePath {
            get { return Path.Combine(SourceLocation, RelativeSourceFilePath); }
        }

        public string SourceFileName {
            get { return Path.GetFileName(AbsoluteSourceFilePath); }
            set {
                if (RelativeSourceFilePath != null) {
                    string path = Path.GetDirectoryName(RelativeSourceFilePath);
                    RelativeSourceFilePath = path != null ? Path.Combine(path, value) : value;
                } else {
                    RelativeSourceFilePath = value; //could also do Path.GetFileName(value);
                }
            }
        }

        public string DestinationFilePath { get; set; }

        public string DestinationFileName {
            get { return Path.GetFileName(DestinationFilePath); }
            set {
                if (DestinationFilePath != null) {
                    string path = Path.GetDirectoryName(DestinationFilePath);
                    DestinationFilePath = path != null ? Path.Combine(path, value) : value;
                } else {
                    DestinationFilePath = value;
                }
            }
        }

        public bool IsAddedPdb { get; set; }
    }

    internal enum MessageType {
        Debug,
        Verbose,
        Info,
        Progress,
        Error,
        Warning,
    }

    internal class UserMessage {
        internal string Message { get; set; }
        internal MessageType Type { get; set; }
        internal ConsoleColor? Colour { get; set; }
        internal Exception Exception { get; set; }
        internal ErrorRecord PsErrorRecord { get; set; }

        public override string ToString() {
            return this.Message;
        }
    }

    internal class OutputController {
        internal delegate void OnNewMessage(object sender, EventArgs args);

        internal event OnNewMessage NewMessage;

        private ConcurrentQueue<UserMessage> output;
        internal ConcurrentQueue<UserMessage> Output {
            get { return output ?? (output = new ConcurrentQueue<UserMessage>()); }
            set { output = value; }
        }

        internal void AddMessage(string message, MessageType type, ConsoleColor colour, Exception ex = null) {
            Output.Enqueue(new UserMessage {
                Message = message,
                Type = type,
                Colour = colour,
                Exception = ex,
            });    
            if (NewMessage != null) {
                NewMessage(this, new EventArgs());
            }
        }

        internal void AddMessage(string message, MessageType type, Exception ex = null) {
            Output.Enqueue(new UserMessage {
                Message = message,
                Type = type,
                Colour = null,
                Exception = ex,
            });
            if (NewMessage != null) {
                NewMessage(this, new EventArgs());
            }
        }

        internal void AddErrorRecord(ErrorRecord errorRecord, MessageType type, string message = null, Exception ex = null) {
            Output.Enqueue(new UserMessage {
                Message = message,
                Type = type,
                Colour = null,
                Exception = ex,
                PsErrorRecord = errorRecord,
            });
        }

        internal int Count { 
            get { return Output.Count; }
        }

        internal UserMessage Dequeue() {
            UserMessage returnValue;
            return Output.TryDequeue(out returnValue) ? returnValue : null;
        }
    }

    #endregion internal classes
}
