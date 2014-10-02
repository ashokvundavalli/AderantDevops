using System;
using Microsoft.TeamFoundation.Client;

namespace Aderant.Build {

    internal static class TeamFoundationHelper {

        /// <summary>
        /// Gets the team project name.
        /// </summary>
        /// <value>
        /// The team project.
        /// </value>
        public static string TeamProject {
            get {
                return "ExpertSuite";   
            }
        }

        /// <summary>
        /// Gets the ExpertSuite team project server.
        /// </summary>
        /// <returns></returns>
        public static TfsTeamProjectCollection GetTeamProjectServer() {
            var server = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri("http://tfs:8080/tfs/ADERANT"));
            server.EnsureAuthenticated();

            return server;
        }
    }
}