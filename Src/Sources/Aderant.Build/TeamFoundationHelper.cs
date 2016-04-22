//using System;
//using Microsoft.TeamFoundation.Client;
//using Microsoft.TeamFoundation.VersionControl.Client;

//namespace Aderant.Build {
//    internal static class TeamFoundationHelper {
//        ///// <summary>
//        ///// Gets the team project name.
//        ///// </summary>
//        ///// <value>
//        ///// The team project.
//        ///// </value>
//        //public static string TeamProject {
//        //    get { return "ExpertSuite"; }
//        //}

//        ///// <summary>
//        ///// Gets the Team Foundation Server URI.
//        ///// </summary>
//        ///// <value>
//        ///// The team foundation server URI.
//        ///// </value>
//        //public static string TeamFoundationServerUri {
//        //    get { return "http://tfs:8080/tfs/ADERANT"; }
//        //}

//        ///// <summary>
//        ///// Gets the ExpertSuite team project server.
//        ///// </summary>
//        ///// <returns></returns>
//        //public static TfsTeamProjectCollection GetTeamProjectServer() {
//        //    var server = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(TeamFoundationServerUri));
//        //    server.EnsureAuthenticated();

//        //    return server;
//        //}

//        /// <summary>
//        /// Gets the TeamFoundationWorkspace for the given TFS item.
//        /// </summary>
//        /// <param name="localItem">The local item.</param>
//        /// <returns></returns>
//        /// <exception cref="System.InvalidOperationException">Could not determine current TFS TeamFoundationWorkspace</exception>
//        public static TeamFoundationWorkspace GetWorkspaceForItem(string localItem) {
//            var server = GetTeamProjectServer();

//            WorkspaceInfo[] workspaceInfo = Workstation.Current.GetAllLocalWorkspaceInfo();

//            for (int i = 0; i < workspaceInfo.Length; i++) {
//                WorkspaceInfo info = workspaceInfo[i];

//                TeamFoundationWorkspace TeamFoundationWorkspace;
//                try {
//                    TeamFoundationWorkspace = info.GetWorkspace(server);
//                } catch (InvalidOperationException) {
//                    continue;
//                }

//                string path = TeamFoundationWorkspace.TryGetServerItemForLocalItem(localItem);
//                if (path != null) {
//                    return TeamFoundationWorkspace;
//                }
//            }

//            throw new InvalidOperationException("Could not determine current TFS TeamFoundationWorkspace");
//        }
//    }
//}