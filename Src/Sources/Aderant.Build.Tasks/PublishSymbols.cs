using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using Aderant.Build.Tasks.FileLock;
using Microsoft.Build.Framework;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Common;
using Microsoft.TeamFoundation.Client;
using MSBuild.Community.Tasks.SymbolServer;

namespace Aderant.Build.Tasks {

    public class PublishSymbols : SymStore {
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
            IBuildDetail currentBuild = null;
            if (!string.IsNullOrEmpty(BuildUri) && !string.IsNullOrEmpty(TeamProjectCollectionUri)) {
                TfsTeamProjectCollection tfs = new TfsTeamProjectCollection(new Uri(TeamProjectCollectionUri));
                currentBuild = tfs.GetService<IBuildServer>().GetBuild(new Uri(BuildUri));
            }
            
            var fileLock = SimpleFileLock.Create(LockFile, TimeSpan.FromMinutes(1));

            for (var i = 0; i < 3; i++) {
                Log.LogMessage("Acquiring symbol store lock: " + LockFile, null);

                if (fileLock.TryAcquireLock()) {
                    Log.LogMessage("Acquired symbol store lock: " + LockFile, null);

                    try {
                        base.Execute();

                        if (currentBuild != null) {
                            AddSymStoreTransactionToBuild(currentBuild);
                        }
                    } finally {
                        fileLock.ReleaseLock();
                        Log.LogMessage("Released symbol store lock: " + LockFile, null);
                    }

                    return !Log.HasLoggedErrors;
                } else {
                    Log.LogMessage("Unable to acquire lock {0} - retrying", fileLock.LockName);
                    Thread.Sleep(TimeSpan.FromSeconds(30));
                }
            }

            throw new SynchronizationLockException("Could not obtain symbol store lock: " + LockFile);

            //return !Log.HasLoggedErrors;
        }

        private void AddSymStoreTransactionToBuild(IBuildDetail build) {
            // For the deletion of symbols to work when a build is retired or a retention policy is activied by TFS we need to ensure that TFS
            // knows where the symbols are stored so the clean up agent can purge the data from the symbol store.
            // The DefaultTemplate workflow does this by recording the StorePath and TransactionId in a BuildInformation node against the build.
            string id = GetLastId(base.Store);

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

                IBuildInformationNode informationNode = build.Information.CreateNode();

                informationNode.Type = InformationTypes.SymStoreTransaction;

                // It is very important that the store path does not have a trailing slash or TFS won't be able to delete the symbols from the drop
                // when a build retention policy fires.
                informationNode.Fields.Add("StorePath", base.Store.TrimEnd(Path.DirectorySeparatorChar));
                informationNode.Fields.Add("TransactionId", id);

                build.Information.Save();
            }
        }

        private string GetLastId(string storePath) {
            string lastIdFile = Path.Combine(storePath, "000Admin", "lastid.txt");

            int retries = 3;
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