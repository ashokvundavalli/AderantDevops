$DebugPreference = 'Continue'

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
    if (directoryHandle.IsInvalid) {
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    StringBuilder path = new StringBuilder(512);
    int size = GetFinalPathNameByHandle(directoryHandle.DangerousGetHandle(), path, path.Capacity, 0);
    if (size < 0) {
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    directoryHandle.Dispose();

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

function BuildProject($properties, [bool]$rebuild) {
    # Load the build libraries as this has our shared compile function. This function is shared by the desktop and server bootstrap of Build.Infrastructure
    $buildScripts = $properties.BuildScriptsDirectory

    if (-not (Test-Path $buildScripts)) {
        throw "Cannot find directory: $buildScripts"
        return
    }

    Write-Debug "Build scripts: $buildScripts"

    pushd $buildScripts
    Invoke-Expression ". .\Build-Libraries.ps1"
    popd	       

    CompileBuildLibraryAssembly $buildScripts $rebuild
}

function LoadAssembly($properties, [string]$targetAssembly) {
    if ([System.IO.File]::Exists($targetAssembly)) {
        Write-Host "Aderant.Build.dll found at $targetAssembly. Loading..."

        #Imports the specified modules without locking it on disk
        $assemblyBytes = [System.IO.File]::ReadAllBytes($targetAssembly)
        $pdb = [System.IO.Path]::ChangeExtension($targetAssembly, "pdb");        

        if (Test-Path $pdb) {
            Write-Debug "Importing assembly with symbols"
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes, [System.IO.File]::ReadAllBytes($pdb))
        } else {
            $assembly = [System.Reflection.Assembly]::Load($assemblyBytes)
        }

        $directory = Split-Path -Parent $targetAssembly

        [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($properties.PackagingTool)) | Out-Null
        
        Import-Module $assembly -DisableNameChecking -Global
    }
}

function UpdateOrBuildAssembly($properties) {
    Write-Debug "Profile home: $actualPath"
    $aderantBuildAssembly = [System.IO.Path]::Combine($properties.BuildToolsDirectory, "Aderant.Build.dll")	

	$needToBuild = $false
	
    if (-not [System.IO.File]::Exists($aderantBuildAssembly)) {
        Write-Host "No Aderant.Build.dll found at $aderantBuildAssembly. Creating..."
		$needToBuild = $true
    }

	if ($needToBuild -eq $true) {
		Write-Host "Building Build.Infrastructure..."
		BuildProject $properties $true
	}

    # Test if one of the files is older than a day
    $aderantBuildFileInfo = Get-ChildItem $aderantBuildAssembly	

	$outdatedAderantBuildFile = $false	
	
	$dt = $aderantBuildFileInfo.LastWriteTime.ToString("d", [System.Globalization.CultureInfo]::CurrentCulture)
	if ($aderantBuildFileInfo.LastWriteTime.Date -le [System.DateTime]::Now.AddDays(-1)) {
        Write-Host ("Aderant.Build.dll is out of date ({0}). Updating..." -f $dt)
		$outdatedAderantBuildFile = $true
    } else {
        Write-Host ("Aderant.Build.dll is not out of date. {0} is less than 1 day old" -f $dt)
    }

	if ($outdatedAderantBuildFile) {
		BuildProject $properties $true
	}

    # Now actually load Aderant.Build.dll
    LoadAssembly $properties $aderantBuildAssembly
}

# We can't use $PSScriptRoot here until we move to Git and get rid of symlinks
$actualPath = GetSymbolicLinkTarget (Split-Path -Parent $MyInvocation.MyCommand.Definition)   

$ShellContext = New-Object -TypeName PSObject
$ShellContext | Add-Member -MemberType ScriptProperty -Name BuildScriptsDirectory -Value { [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($actualPath, "..\..\Build")) }
$ShellContext | Add-Member -MemberType ScriptProperty -Name BuildToolsDirectory -Value { [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($actualPath, "..\..\Build.Tools")) }
$ShellContext | Add-Member -MemberType ScriptProperty -Name PackagingTool -Value { [System.IO.Path]::Combine($This.BuildScriptsDirectory, "paket.exe") }
$ShellContext | Add-Member -MemberType NoteProperty -Name IsGitRepository -Value $false
$ShellContext | Add-Member -MemberType NoteProperty -Name PoshGitAvailable -Value $false

Write-Debug $ShellContext

UpdateOrBuildAssembly $ShellContext