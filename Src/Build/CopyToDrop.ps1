##
# Copies the module build output from <ModuleName>\Bin\* to the drop location
##
param([string]$moduleName, [string]$moduleRootPath,  [string]$dropRootUNCPath, [string]$assemblyFileVersion)

begin{    
    $moduleDropPath = [system.io.path]::GetFullPath($dropRootUNCPath+'\'+$assemblyFileVersion)
    $commonBuildDir = Join-Path $moduleRootPath \CommonBuild    
    $shell = Join-Path $commonBuildDir Build-Libraries.ps1
    &($shell)
}

process{        
    write "Copying $moduleBinPath to $moduleDropPath"    
    CopyBinFilesForDrop -modulePath $moduleRootPath -toModuleDropPath $moduleDropPath       
}

end{
    write "Managed binaries updated for $moduleName"
}