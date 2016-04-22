<#
.Synopsis
    Copies the module build output from <ModuleName>\Bin\* to the drop location
.Parameter moduleName
    The module name
.Parameter moduleRootPath
    The local path to the module on the build server
.Parameter dropRootUNCPath
    The network drop path of the branch
.Parameter assemblyFileVersion
    The assembly file version of the module
.Parameter testBreak
    Controls if the contents of the unit test directory is copied to the drop
.Parameter buildBreak
    Indicates if the build passed or failed. Creates a build.succeeded file if the build passed.
.Parameter suppressUniqueCheck
    Suppresses the check when we compare the contents of the Dependencies directory to the Bin\Module directory when copying files to the drop location
#>
param([string]$moduleName, [string]$moduleRootPath, [string]$dropRootUNCPath, [string]$assemblyFileVersion, [switch]$testBreak = $false, [switch]$buildBreak = $false, [switch]$suppressUniqueCheck = $false)

begin {    
    $ErrorActionPreference = 'Stop'

    [string]$moduleDropPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($dropRootUNCPath, $assemblyFileVersion))   
    
    $buildScriptsDirectory = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    Write-Debug "Using $buildScriptsDirectory as build script directory"    

    $buildLibraries = "$buildScriptsDirectory\Build-Libraries.ps1"
    & $buildLibraries    
}

process {    
    Write-Host "Copying $moduleRootPath to $moduleDropPath"

    CopyBinFilesForDrop -modulePath $moduleRootPath -toModuleDropPath $moduleDropPath -testBreak:$testBreak -buildBreak:$buildBreak -suppressUniqueCheck:$suppressUniqueCheck  
}

end {
    write "Managed binaries updated for $moduleName"
}

