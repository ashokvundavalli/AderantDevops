using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Framework.Server;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using Microsoft.TeamFoundation.VersionControl.Server;
using ItemSpec = Microsoft.TeamFoundation.VersionControl.Client.ItemSpec;
using ItemType = Microsoft.TeamFoundation.VersionControl.Client.ItemType;
using PendingChange = Microsoft.TeamFoundation.VersionControl.Client.PendingChange;

namespace Aderant.Server.CheckInPolicy {
    public class CommonCheckInPolicySubscriber : ISubscriber {
        private const string HelpUrl = "http://ttwiki.ap.aderant.com/wiki/index.php?title=Module_Builds_and_TFS#How_do_I_move_or_rename_a_file_without_losing_the_TFS_history.3F";

        public string Name {
            get { return "Aderant.Server.CheckInPolicy"; }
        }

        public SubscriberPriority Priority {
            get { return SubscriberPriority.Normal; }
        }

        public EventNotificationStatus ProcessEvent(TeamFoundationRequestContext requestContext, NotificationType notificationType, object notificationEventArgs, out int statusCode, out string statusMessage, out ExceptionPropertyCollection properties) {
            var sw = new Stopwatch();
            sw.Start();

            statusCode = 0;
            properties = null;
            statusMessage = string.Empty;

            try {
                if (notificationType == NotificationType.DecisionPoint && notificationEventArgs is CheckinNotification) {
                    var checkinNotification = notificationEventArgs as CheckinNotification;

                    // rule overwrite logic
                    if (checkinNotification.Comment.EndsWith("***IDDQD***")) {
                        return EventNotificationStatus.ActionPermitted;
                    }

                    // exception test
                    if (checkinNotification.Comment.EndsWith("***IDSPISPOPD***")) {
                        throw new Exception("Just a test :-)");
                    }

                    // retrieve current (server) workspace
                    TeamFoundationLocationService service = requestContext.GetService<TeamFoundationLocationService>();
                    var accessMapping = service.GetServerAccessMapping(requestContext);
                    var tfsServer = new TfsTeamProjectCollection(new Uri(accessMapping.AccessPoint));
                    var versionControlServer = (VersionControlServer) tfsServer.GetService(typeof (VersionControlServer));
                    var workspace = versionControlServer.GetWorkspace(checkinNotification.WorkspaceName, checkinNotification.WorkspaceOwner.DisplayName);

                    // get all pending changes
                    var pendingChanges = workspace.GetPendingChanges().Where(p => p.ItemType == ItemType.File);

                    // no pending changes -> no rule to apply
                    if (!pendingChanges.Any()) {
                        return EventNotificationStatus.ActionPermitted;
                    }

                    // only apply rule for team project ExpertSuite
                    var teamProject = workspace.GetTeamProjectForLocalPath(pendingChanges.First().LocalItem);
                    if (teamProject.Name != "ExpertSuite") {
                        return EventNotificationStatus.ActionPermitted;
                    }

                    var submittedItems = checkinNotification.GetSubmittedItems(requestContext);

                    List<PendingChange> filteredChanges = new List<PendingChange>();
                    foreach (var change in pendingChanges) {
                        foreach (var item in submittedItems) {
                            if (string.Equals(change.ServerItem, item)) {
                                filteredChanges.Add(change);
                            }
                        }
                    }

                    var pendingAdds = filteredChanges.Where(p => p.IsAdd).ToList();
                    var pendingDeletes = filteredChanges.Where(p => p.IsDelete).ToList();

                    // check if a file was deleted and added somewhere else
                    // OR
                    // a file was deleted and added under a new name anywhere
                    var addedAndDeletedFiles = new List<string>();

                    foreach (var pendingDelete in pendingDeletes) {
                        if (pendingAdds.Any(p => p.FileName == pendingDelete.FileName) ||
                            pendingAdds.Any(add => {                               
                                try {
                                    // add will not have a hash value as it isn't added yet, but we do have the hash the contents
                                    // sent to TFS so we can use this to compare against the file that was deleted (as it's checked in and so has a hash value)
                                    if (add.UploadHashValue != null && pendingDelete.HashValue != null) {
                                        return string.Equals(ToHex(add.UploadHashValue), ToHex(pendingDelete.HashValue));
                                    }
                                }
                                catch (Exception) {
                                    return false;
                                }
                                return false;

                            })) {
                            addedAndDeletedFiles.Add(pendingDelete.FileName);
                        }
                    }

                    // if nothing went wrong, allow check in
                    if (addedAndDeletedFiles.Count == 0) {
                        return EventNotificationStatus.ActionPermitted;
                    }

                    // build failure message
                    var failureMessages = new StringBuilder();
                    if (addedAndDeletedFiles.Count == 1) {
                        failureMessages.AppendFormat(CultureInfo.InvariantCulture, "{0} was deleted and added which will result in loss of TFS history! Please rename the file correctly:{1}{2}", addedAndDeletedFiles[0], Environment.NewLine, HelpUrl);
                    } else if (addedAndDeletedFiles.Count > 1) {
                        foreach (var file in addedAndDeletedFiles) {
                            failureMessages.AppendLine(string.Concat("* ", file));
                        }
                        failureMessages.AppendFormat(CultureInfo.InvariantCulture, "were deleted and added which will result in loss of TFS history! Please rename the files correctly:{0}{1}", Environment.NewLine, HelpUrl);
                    }

                    // disallow check in and return full failure message
                    statusMessage = failureMessages.ToString();

                    try {
                        // log offenders
                        var offendersFile = Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), "_offenders.txt");
                        var now = DateTime.Now;
                        var filesBuilder = new StringBuilder();
                        foreach (var file in addedAndDeletedFiles) {
                            filesBuilder.AppendLine(file);
                        }
                        var offenderEntry = string.Format(CultureInfo.InvariantCulture, "{0} at {1} {2}{3}{4}{5}", workspace.DisplayName, now.ToShortDateString(), now.ToShortTimeString(), Environment.NewLine, filesBuilder, Environment.NewLine);
                        File.AppendAllText(offendersFile, offenderEntry);
                    } catch {
                    }

                    return EventNotificationStatus.ActionDenied;
                }

                if (notificationType == NotificationType.Notification && notificationEventArgs is CheckinNotification) {
                    Task.Run(() => {
                        LogLocalWorkspaceUser(requestContext, notificationEventArgs);
                    });
                }
                return EventNotificationStatus.ActionPermitted;
            } catch (Exception ex) {
                // log the error but allow the check in
                statusMessage = string.Format(CultureInfo.InvariantCulture, "Error in plugin '{0}', error details: {1}{2}{3}Please contact the Framework team ASAP. Thank you!", Name, ex, Environment.NewLine, Environment.NewLine);

                try {
                    EventLog.WriteEntry("TFS Service", statusMessage, EventLogEntryType.Error);
                    var errorsFile = Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), "_errors.txt");
                    var now = DateTime.Now;

                    var checkinNotification = notificationEventArgs as CheckinNotification;

                    // retrieve current (server) workspace
                    TeamFoundationLocationService service = requestContext.GetService<TeamFoundationLocationService>();
                    var accessMapping = service.GetServerAccessMapping(requestContext);
                    var tfsServer = new TfsTeamProjectCollection(new Uri(accessMapping.AccessPoint));
                    var versionControlServer = (VersionControlServer) tfsServer.GetService(typeof (VersionControlServer));
                    var workspace = versionControlServer.GetWorkspace(checkinNotification.WorkspaceName, checkinNotification.WorkspaceOwner.DisplayName);

                    var errorEntry = string.Format(CultureInfo.InvariantCulture, "{0} at {1} {2}{3}{4}{5}{5}", workspace.DisplayName, now.ToShortDateString(), now.ToShortDateString(), Environment.NewLine, ex, Environment.NewLine);
                    
                    ThreadPool.QueueUserWorkItem((o) => {
                        Thread.Sleep(10000);
                        // if we don't wait (magic numbered 10 seconds) until the method returns EventNotificationStatus.ActionPermitted, 
                        // TFS raises a RequestCanceledException (probably due to File IO?)
                        File.AppendAllText(errorsFile, errorEntry);
                    });
                } catch {
                }
                return EventNotificationStatus.ActionPermitted;
            }
        }

        private static void LogLocalWorkspaceUser(TeamFoundationRequestContext requestContext, object notificationEventArgs) {
            try {
                var checkinNotification = notificationEventArgs as CheckinNotification;

                // retrieve current (server) workspace
                TeamFoundationLocationService service = requestContext.GetService<TeamFoundationLocationService>();
                var accessMapping = service.GetServerAccessMapping(requestContext);
                var tfsServer = new TfsTeamProjectCollection(new Uri(accessMapping.AccessPoint));
                var versionControlServer = (VersionControlServer) tfsServer.GetService(typeof (VersionControlServer));
                var workspace = versionControlServer.GetWorkspace(checkinNotification.WorkspaceName, checkinNotification.WorkspaceOwner.DisplayName);

                // get first item of change set to retrieve the team project
                var changeset = versionControlServer.GetChangeset(checkinNotification.Changeset);
                var serverItem = changeset.Changes[0].Item.ServerItem;

                // log all check-ins in team project ExpertSuite from local workspaces for informational purposes
                if (serverItem.StartsWith("$/ExpertSuite") && workspace.Location == WorkspaceLocation.Local) {
                    var localWorkspaceListFile = Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), "_local-workspaces.txt");
                    var now = DateTime.Now;
                    var localWorkSpaceEntry = string.Format("{0} at {1} {2}{3}", workspace.DisplayName, now.ToShortDateString(), now.ToShortTimeString(), Environment.NewLine);
                    File.AppendAllText(localWorkspaceListFile, localWorkSpaceEntry);
                }
            } catch {
            }
        }

        public Type[] SubscribedTypes() {
            return new[] {typeof (CheckinNotification)};
        }

        public static string ToHex(byte[] bytes) {
            StringBuilder result = new StringBuilder(bytes.Length*2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString("X2"));

            return result.ToString();
        }
    }
}