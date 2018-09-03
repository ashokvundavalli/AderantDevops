using System;
using System.Runtime.InteropServices;

namespace Aderant.Build {
    public static class Dfs {

        /// <summary>
        /// The NetApiBufferFree function frees the memory that the NetApiBufferAllocate function allocates
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        [DllImport("netapi32.dll", SetLastError = true)]
        private static extern int NetApiBufferFree(IntPtr buffer);

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int NetDfsGetInfo(
            [MarshalAs(UnmanagedType.LPWStr)] string dfsEntryPath, /* DFS entry path for the volume */
            [MarshalAs(UnmanagedType.LPWStr)] string serverName, /* This parameter is currently ignored and should be NULL */
            [MarshalAs(UnmanagedType.LPWStr)] string shareName, /* This parameter is currently ignored and should be NULL. */
            NetDfsInfoLevel level, /* Level of information requested */
            out IntPtr buffer /* API allocates and returns buffer with requested info */
        );

        private static T GetStruct<T>(IntPtr buffer, int offset = 0) where T : struct {
            T r = new T();
            r = (T)Marshal.PtrToStructure(buffer + offset * Marshal.SizeOf(r), typeof(T));
            return r;
        }

        public static string ResolveDfsPath(string share) {
            System.Diagnostics.Debugger.Launch();

            string path = null;

            if (!string.IsNullOrWhiteSpace(share)) {
                if (share.Length > 2) {
                    if (share[0] == '\\' && share[1] == '\\') {
                        path = GetShareFromPath(share);
                    }
                }
            }

            if (path == null) {
                return share;
            }

            IntPtr b = IntPtr.Zero;
            try {
                int result = NetDfsGetInfo(path, null, null, NetDfsInfoLevel.DfsInfo3, out b);

                if (result != 0) {
                    // return passed string if not DFS
                    return path;
                }

                DFS_INFO_3 sRes = GetStruct<DFS_INFO_3>(b);
                if (sRes.NumberOfStorages > 0) {
                    DFS_STORAGE_INFO sResInfo = GetStruct<DFS_STORAGE_INFO>(sRes.Storage);
                    path = string.Concat(@"\\", sResInfo.ServerName, @"\", sResInfo.ShareName, @"\");
                }
            } finally {
                NetApiBufferFree(b);
            }

            return path;
        }

        internal static string GetShareFromPath(string share) {
            var pathWithoutPrefix = share.TrimStart('\\');

            string[] parts = pathWithoutPrefix.Split(new[] { '\\' }, 3);
            if (parts.Length >= 2) {
                return "\\\\" + parts[0] + "\\" + parts[1];
            }

            return share;
        }

        private enum NetDfsInfoLevel {
            DfsInfo1 = 1,
            DfsInfo2 = 2,
            DfsInfo3 = 3,
            DfsInfo4 = 4,
            DfsInfo5 = 5,
            DfsInfo6 = 6,
            DfsInfo7 = 7,
            DfsInfo8 = 8,
            DfsInfo9 = 9,
            DfsInfo50 = 50,
            DfsInfo100 = 100,
            DfsInfo150 = 150,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DFS_INFO_3 {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string EntryPath;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string Comment;

            public int State;
            public int NumberOfStorages;
            public IntPtr Storage;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DFS_STORAGE_INFO {
            public int State;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string ServerName;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string ShareName;
        }
    }
}
