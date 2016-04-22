using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Common;
using Microsoft.TeamFoundation.Client;

namespace Aderant.Build.Tasks {
    public class PublishSymbols : SymStore {
        private bool hasAddedSymbolInformation;
        private int retryAttempts;

        static PublishSymbols() {
            VisualStudioEnvironmentContext.SetupContext();
        }

        /// <summary>
        /// Gets or sets the team project collection URI.
        /// </summary>
        /// <value>
        /// The team project collection URI.
        /// </value>
        public string TeamProjectCollectionUri { get; set; }

        /// <summary>
        /// Gets or sets the build URI.
        /// </summary>
        /// <value>
        /// The build URI.
        /// </value>
        public string BuildUri { get; set; }

        /// <summary>
        /// Gets or sets the lock file. This is a semaphore used to serialize writes to the symbol store.
        /// </summary>
        /// <value>
        /// The lock file.
        /// </value>
        [Required]
        public string LockFile { get; set; }

        [Required]
        public string WindowsSdkPath { get; set; }

        protected override string GenerateFullPathToTool() {
            // The base implementation is very dense
            return Path.Combine(WindowsSdkPath, base.GenerateFullPathToTool());
        }

        public override bool Execute() {
            try {
                // We need to serialize access to the symbol store or corruption will occur as its just a file system based thing.
                // Two locks here, one computer level as its much cheaper and more reliable to acquire and then the file lock on the drop itself.

                var mutexName = @"Global\Publish_Symbols_" + Guid.Parse("4B0684B6-0523-4504-BC50-399F4ECF8B18");

                using (var mutex = new Mutex(false, mutexName)) {
                    var owner = false;

                    try {
                        try {
                            Log.LogMessage(MessageImportance.High, "Acquiring machine symbol store lock");
                            owner = mutex.WaitOne(TimeSpan.FromSeconds(30));
                        } catch (AbandonedMutexException) {
                            // Abandoning a mutex is an indication something wrong is going on
                            owner = true; // now mine
                        }

                        if (!owner) {
                            SleepRandom();
                            try {
                                owner = mutex.WaitOne(TimeSpan.FromSeconds(30));
                            }  catch (AbandonedMutexException) {
                                // Abandoning a mutex is an indication something wrong is going on
                                owner = true; // now mine
                            }
                        }

                        if (owner) {
                            Log.LogMessage(MessageImportance.High, "Acquired machine symbol store lock");
                            var result = Publish();

                            if (!result) {
                                throw new SynchronizationLockException("Could not obtain symbol store lock: " + LockFile);
                            }
                        } else {
                            return LogNoPublishAndExit();
                        }
                    } finally {
                        if (owner) {
                            Log.LogMessage(MessageImportance.High, "Releasing machine symbol store lock");
                            mutex.ReleaseMutex();
                        }
                    }
                }
            } finally {
                VisualStudioEnvironmentContext.Shutdown();
            }

            if (ExitCode == 32) {
                // Sigh after all the locks and retries it still exploded. Treat this as non-fatal.
                return LogNoPublishAndExit();
            }

            // Exit code of -1 or 0 could mean success/
            // -1 typically happens after a failure then a successful add which I think means "transaction overwritten"
            return ExitCode <= 0;
        }

        private bool LogNoPublishAndExit() {
            Log.LogWarning("Failed to acquire machine symbol publishing lock in a reasonable time frame and symbols were not published. Debugging experience will be compromised.");
            return true;
        }

        private bool Publish() {
            TfsTeamProjectCollection tfs = null;
            IBuildDetail currentBuild = null;

            if (!string.IsNullOrEmpty(BuildUri) && !string.IsNullOrEmpty(TeamProjectCollectionUri)) {
                tfs = new TfsTeamProjectCollection(new Uri(TeamProjectCollectionUri));
                currentBuild = tfs.GetService<IBuildServer>().GetBuild(new Uri(BuildUri));
            }

            try {
                for (var i = 0; i < 3; i++) {
                    Log.LogMessage("Acquiring symbol store lock: " + LockFile, null);

                    using (var fileLock = FileLock.TryAcquire(LockFile, TimeSpan.FromMinutes(1))) {
                        if (fileLock.HasLock) {
                            Log.LogMessage("Acquired symbol store lock: " + LockFile, null);

                            try {
                                base.Execute();

                                if (currentBuild != null) {
                                    AddSymStoreTransactionToBuild(currentBuild);
                                }

                                return true;
                            } finally {
                                Log.LogMessage(MessageImportance.High, "Tool exited with code: " + ExitCode);

                                // Error 32: The process cannot access the file because it is being used by another process.
                                if (ExitCode == 32) {
                                    // We have both the system mutex AND the file lock however
                                    // there are other branches which don't use the file lock or the mutex to serialize
                                    // access as well as the TFS reaper for build clean up AND joy of joys DFS so it still possible
                                    // to end up with file locks.
                                    SleepRandom();
                                    retryAttempts++;

                                    // Prevent quirks with lock recursion 
                                    fileLock.Dispose();

                                    if (retryAttempts < 10) {
                                        Publish();
                                    }
                                }

                                Log.LogMessage("Released symbol store lock: " + LockFile, null);
                            }
                        }

                        Log.LogMessage("Unable to acquire lock {0} - retrying", LockFile);
                        SleepRandom();
                    }
                }
            } finally {
                if (currentBuild != null) {
                    currentBuild.Disconnect();
                }
                if (tfs != null) {
                    tfs.Disconnect();
                    tfs.Dispose();
                }
            }

            return false;
        }

        private static void SleepRandom() {
            Thread.Sleep(new Random().Next(1000, 10000));
        }

        private void AddSymStoreTransactionToBuild(IBuildDetail build) {
            if (hasAddedSymbolInformation) {
                return;
            }

            // For the deletion of symbols to work when a build is retired or a retention policy is activied by TFS we need to ensure that TFS
            // knows where the symbols are stored so the clean up agent can purge the data from the symbol store.
            // The DefaultTemplate workflow does this by recording the StorePath and TransactionId in a BuildInformation node against the build.
            var id = GetLastId(Store);

            if (!string.IsNullOrWhiteSpace(id)) {
/*
                Microsoft.TeamFoundation.Build.Client.BuildInformationNode
                +		Children	{Microsoft.TeamFoundation.Build.Client.BuildInformation}	Microsoft.TeamFoundation.Build.Client.IBuildInformation {Microsoft.TeamFoundation.Build.Client.BuildInformation}
                -		Fields	Count = 2	System.Collections.Generic.Dictionary<string,string>
                -		[0]	{[StorePath, \\na.aderant.com\ExpertSuite\Symbols]}	System.Collections.Generic.KeyValuePair<string,string>
                Key	"StorePath"	string
                key	"StorePath"	string
                Value	"\\\\na.aderant.com\\ExpertSuite\\Symbols"	string
                value	"\\\\na.aderant.com\\ExpertSuite\\Symbols"	string
                -		[1]	{[TransactionId, 0000000324]}	System.Collections.Generic.KeyValuePair<string,string>
                Key	"TransactionId"	string             
                Value	"0000000324"	string           
                +		Raw View		
                Id	5	int
                LastModifiedBy	"ADERANT_AP\\service.tfsbuild.ap"	string
                +		LastModifiedDate	{21/04/2015 4:23:00 p.m.}	System.DateTime
                Parent	null	Microsoft.TeamFoundation.Build.Client.IBuildInformationNode
                Type	"SymStoreTransaction"	string
*/
                Log.LogMessage("Adding symbol store transaction to build history. Transaction Id:" + id, null);

                var informationNode = build.Information.CreateNode();

                informationNode.Type = InformationTypes.SymStoreTransaction;

                // It is very important that the store path does not have a trailing slash or TFS won't be able to delete the symbols from the drop
                // when a build retention policy fires.
                informationNode.Fields.Add("StorePath", Store.TrimEnd(Path.DirectorySeparatorChar));
                informationNode.Fields.Add("TransactionId", id);

                build.Information.Save();

                hasAddedSymbolInformation = true;
            }
        }

        private string GetLastId(string storePath) {
            var lastIdFile = Path.Combine(storePath, "000Admin", "lastid.txt");

            var retries = 3;
            while (true) {
                try {
                    Log.LogMessage("Reading last id file: " + lastIdFile, null);

                    return ReadLastIdFile(lastIdFile);
                } catch {
                    if (--retries == 0) {
                        throw;
                    }
                    Log.LogMessage("Possible transient error while reading Last id file. Waiting to retry.", null);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }

        private string ReadLastIdFile(string lastIdFile) {
            var lines = File.ReadAllLines(lastIdFile);

            if (lines.Length > 0) {
                return lines[0].TrimEnd(null);
            }

            return null;
        }
    }
}