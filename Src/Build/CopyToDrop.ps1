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
.Parameter copyTestDirectory
    Controls if the contents of the unit test directory is copied to the drop
.Parameter suppressUniqueCheck
    Suppresses the check when we compare the contents of the Dependencies directory to the Bin\Module directory when copying files to the drop location
#>
param([string]$moduleName, [string]$moduleRootPath, [string]$dropRootUNCPath, [string]$assemblyFileVersion, [switch]$copyTestDirectory = $false, [switch]$suppressUniqueCheck = $false)

begin {    
    [string]$moduleDropPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($dropRootUNCPath, $assemblyFileVersion))   
    
    $commonBuildDir = Join-Path $moduleRootPath \CommonBuild    
    $shell = Join-Path $commonBuildDir Build-Libraries.ps1
    &($shell)

    Write-Host "Is Team Build: $IsTeamBuild"
}

process {    
    Write-Host "Copying $moduleRootPath to $moduleDropPath"

    CopyBinFilesForDrop -modulePath $moduleRootPath -toModuleDropPath $moduleDropPath -copyTestDirectory:$copyTestDirectory -suppressUniqueCheck:$suppressUniqueCheck  
}

end {
    write "Managed binaries updated for $moduleName"
}

