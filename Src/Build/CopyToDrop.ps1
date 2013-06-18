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
#>
param([string]$moduleName, [string]$moduleRootPath, [string]$dropRootUNCPath, [string]$assemblyFileVersion, [switch]$copyTestDirectory = $false)

begin{    
    [string]$moduleDropPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($dropRootUNCPath, $assemblyFileVersion))   
	
    $commonBuildDir = Join-Path $moduleRootPath \CommonBuild    
    $shell = Join-Path $commonBuildDir Build-Libraries.ps1
    &($shell)
}

process {        
    write "Copying $moduleRootPath to $moduleDropPath"
    CopyBinFilesForDrop -modulePath $moduleRootPath -toModuleDropPath $moduleDropPath
}

end{
    write "Managed binaries updated for $moduleName"
}