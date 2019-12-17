using System.Diagnostics;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.Model {

    [DebuggerDisplay("String value is {" + nameof(DebuggerDisplay) + "}")]
    public class ResolvedDependency<TUnresolved, TResolved> : IResolvedDependency
        where TUnresolved : class, IUnresolvedDependency
        where TResolved : class, IDependable {

        public ResolvedDependency(IArtifact artifact, TResolved resolved, TUnresolved unresolved) {
            ExistingUnresolvedItem = unresolved;
            ResolvedReference = resolved;
            Artifact = artifact;
        }

        private string DebuggerDisplay {
            [DebuggerStepThrough]
            get {
                if (ExistingUnresolvedItem != null) {
                    return $"{ExistingUnresolvedItem.GetType().Name} {ExistingUnresolvedItem.Id} => {ResolvedReference.Id}";
                }

                return $"{ResolvedReference.Id}";
            }
        }

        public TUnresolved ExistingUnresolvedItem { get; protected set; }

        public TResolved ResolvedReference { get; set; }

        public IArtifact Artifact { get; }

        IUnresolvedDependency IResolvedDependency.ExistingUnresolvedItem {
            get { return ExistingUnresolvedItem; }
        }

        IDependable IResolvedDependency.ResolvedReference {
            get { return ResolvedReference; }
        }
    }

    public interface IResolvedDependency {

        IArtifact Artifact { get; }

        IUnresolvedDependency ExistingUnresolvedItem { get; }

        IDependable ResolvedReference { get; }
    }

    internal static class ResolvedDependency {
        public static ResolvedDependency<TUnresolved, TResolved> Create<TUnresolved, TResolved>(IArtifact artifact, TResolved target, TUnresolved unresolvedDependency)
            where TUnresolved : class, IUnresolvedDependency
            where TResolved : class, IDependable {

            return new ResolvedDependency<TUnresolved, TResolved>(artifact, target, unresolvedDependency);

        }

        public static ResolvedDependency<IUnresolvedDependency, TResolved> Create<TResolved>(IArtifact artifact, TResolved target)
            where TResolved : class, IDependable {

            return new ResolvedDependency<IUnresolvedDependency, TResolved>(artifact, target, null);
        }
    }
}