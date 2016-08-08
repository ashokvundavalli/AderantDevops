using System;

namespace Aderant.Build.DependencyResolver {

    internal sealed class DependencyResolvedEventArgs : EventArgs {
        public string DependencyProvider { get; set; }

        public string Branch { get; set; }

        public bool ResolvedUsingHardlink { get; set; }

        public string FullPath { get; set; }
    }
}