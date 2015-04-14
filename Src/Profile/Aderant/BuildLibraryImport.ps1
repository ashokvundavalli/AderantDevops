function GetSymbolicLinkTarget($path) {
    Add-Type -MemberDefinition @"
private const int CREATION_DISPOSITION_OPEN_EXISTING = 3;
private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

[DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
 public static extern int GetFinalPathNameByHandle(IntPtr handle, [In, Out] StringBuilder path, int bufLen, int flags);

[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
 public static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode,
 IntPtr SecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

 public static string GetSymbolicLinkTarget(System.IO.DirectoryInfo symlink)
 {
     SafeFileHandle directoryHandle = CreateFile(symlink.FullName, 0, 2, System.IntPtr.Zero, CREATION_DISPOSITION_OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, System.IntPtr.Zero);
     if(directoryHandle.IsInvalid)
     throw new Win32Exception(Marshal.GetLastWin32Error());

     StringBuilder path = new StringBuilder(512);
     int size = GetFinalPathNameByHandle(directoryHandle.DangerousGetHandle(), path, path.Capacity, 0);
     if (size<0)
     throw new Win32Exception(Marshal.GetLastWin32Error());
     // The remarks section of GetFinalPathNameByHandle mentions the return being prefixed with "\\?\"
     // More information about "\\?\" here -> http://msdn.microsoft.com/en-us/library/aa365247(v=VS.85).aspx
     if (path[0] == '\\' && path[1] == '\\' && path[2] == '?' && path[3] == '\\')
     return path.ToString().Substring(4);
     else
     return path.ToString();
 }
"@ -Name Win32 -NameSpace System -UsingNamespace System.Text,Microsoft.Win32.SafeHandles,System.ComponentModel

    $resolvedPath = [System.Win32]::GetSymbolicLinkTarget($path)
    Write-Host "Resolved profile path to: $resolvedPath"
    return $resolvedPath
}

function BuildProject([string]$actualPath, [bool]$rebuild) {
    $frameworkPath = "C:\Program Files (x86)\MSBuild\12.0\Bin"
    if (-not (Test-Path $frameworkPath)) {			
        $frameworkPath = $([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory())
    }

    $buildTool = [System.IO.Path]::Combine($frameworkPath, "MSBuild.exe")

    $projectPath = [System.IO.Path]::Combine($actualPath, "..\..\..\Build.Infrastructure.sln")
    $projectPath = [System.IO.Path]::GetFullPath($projectPath)

    if (-not [System.IO.File]::Exists($projectPath)) {
        throw "Cannot compile Aderant.Build assembly as the project file does not exist at path $projectPath"
    }
        
    $loggerSwitch = "/noconsolelogger"
    if ($DebugPreference -eq "Continue") {
        $loggerSwitch = ""
    }

    $rebuildSwitch = ""
    if ($rebuild) {
        $rebuildSwitch = "/target:Rebuild"
    }

    $command = "cmd /c `"$buildTool`" /nr:false $loggerSwitch /nologo /m `"$projectPath`" /verbosity:m " + $rebuildSwitch

    Invoke-Expression $command
}

function LoadAssembly([string]$targetAssembly) {
    if ([System.IO.File]::Exists($targetAssembly)) {
        Write-Host "Aderant.Build.dll found at $targetAssembly. Loading..."

        #Imports the specified modules without locking it on disk
        $assemblyBytes = [System.IO.File]::ReadAllBytes($targetAssembly)
        [System.Reflection.Assembly]$assembly = [System.Reflection.Assembly]::Load($assemblyBytes)
        Import-Module $assembly -DisableNameChecking
    }
}

function UpdateOrBuildAssembly([string]$actualPath) {	
    $targetAssembly = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($actualPath, "..\..\Build.Tools\Aderant.Build.dll"))

  #  if ([System.Diagnostics.Debugger]::IsAttached -eq $false) {
        if (-not [System.IO.File]::Exists($targetAssembly)) {
            Write-Host "No Aderant.Build.dll found at $targetAssembly. Creating..."
            BuildProject $actualPath $false
        }

        # Test if the file is older than a day
        $fileInfo = Get-ChildItem $targetAssembly
        if ($fileInfo.LastWriteTimeUtc.Date -le [System.DateTime]::UtcNow.AddDays(-1)) {
            Write-Host "Aderant.Build.dll is out of date. Updating..."
            BuildProject $actualPath $true
        }	
  #  }

    # Now actually load the assembly
    LoadAssembly $targetAssembly
}

$actualPath = GetSymbolicLinkTarget (Split-Path -Parent $MyInvocation.MyCommand.Definition)

UpdateOrBuildAssembly $actualPath
Initialize-AderantModuleHelp