using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aderant.Build.Model {
    public class ResolvedDependency<TUnresolved, TResolved> where TUnresolved : class where TResolved : class, IDependable {

        protected ResolvedDependency(IArtifact artifact) {
            Artifact = artifact;
        }

        public IArtifact Artifact { get; }

        public TUnresolved ExistingUnresolvedItem { get; protected set; }

        public TResolved ResolvedReference { get; set; }
    }
}
