using System;
using System.Runtime.InteropServices;

namespace Aderant.Build {

    internal static class NativeUtilities {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);

        internal enum SymbolicLink : uint {
            SYMBOLIC_LINK_FLAG_FILE = 0,
            SYMBOLIC_LINK_FLAG_DIRECTORY = 1
        }
    }
}