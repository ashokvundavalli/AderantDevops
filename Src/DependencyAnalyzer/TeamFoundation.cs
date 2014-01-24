using System;
using Microsoft.TeamFoundation.Client;

namespace DependencyAnalyzer {
    internal static class TeamFoundation {

        /// <summary>
        /// Gets the Expert Suite team project.
        /// </summary>
        /// <returns></returns>
        public static TfsTeamProjectCollection GetTeamProject() {
            var collection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri("http://tfs:8080/tfs/ADERANT"));
            collection.EnsureAuthenticated();

            return collection;
        }
    }
}