using System;

namespace Aderant.WebHooks.Model {
    [Flags]
    internal enum SourceControlType {
        Tfvc = 0,
        Git = 2
    }
}