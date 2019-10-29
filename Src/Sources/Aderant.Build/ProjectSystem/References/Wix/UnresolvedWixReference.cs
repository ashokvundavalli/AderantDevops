namespace Aderant.Build.ProjectSystem.References.Wix {
    internal class UnresolvedWixReference : UnresolvedAssemblyReference {
        public UnresolvedWixReference(WixReferenceService wixReferenceService, UnresolvedAssemblyReferenceMoniker moniker)
            : base(wixReferenceService, moniker) {
        }
    }
}