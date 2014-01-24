using System;
using System.ComponentModel;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DependencyAnalyzer {
	[Cmdlet(VerbsCommon.Get, "SymbolicLinkTarget")]
	public class GetSymbolicLinkTarget : PSCmdlet {
		[Parameter(Mandatory = false, Position = 0, HelpMessage = "Sets the module name or names which are the dependency providers.")]
		public System.IO.DirectoryInfo symlink {
			get;
			set;
		}

		private const int FILE_SHARE_READ = 1;
		private const int FILE_SHARE_WRITE = 2;

		private const int CREATION_DISPOSITION_OPEN_EXISTING = 3;
		private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

		[DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int GetFinalPathNameByHandle(IntPtr handle, [In, Out] StringBuilder path, int bufLen, int flags);

		[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode,
		IntPtr SecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);


		protected override void ProcessRecord()
		{
			SafeFileHandle directoryHandle = CreateFile(symlink.FullName, 0, 2, System.IntPtr.Zero, CREATION_DISPOSITION_OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, System.IntPtr.Zero);
			if (directoryHandle.IsInvalid)
				throw new Win32Exception(Marshal.GetLastWin32Error());

			StringBuilder path = new StringBuilder(512);
			int size = GetFinalPathNameByHandle(directoryHandle.DangerousGetHandle(), path, path.Capacity, 0);
			if (size < 0)
				throw new Win32Exception(Marshal.GetLastWin32Error());
			// The remarks section of GetFinalPathNameByHandle mentions the return being prefixed with "\\?\"
			// More information about "\\?\" here -> http://msdn.microsoft.com/en-us/library/aa365247(v=VS.85).aspx
			if (path[0] == '\\' && path[1] == '\\' && path[2] == '?' && path[3] == '\\')
				WriteObject(path.ToString().Substring(4));
			else
				WriteObject(path.ToString());

		}

	}
}
