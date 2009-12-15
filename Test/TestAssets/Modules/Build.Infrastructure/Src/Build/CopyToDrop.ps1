##
# Copies the module build output from <ModuleName>\Bin\* to the drop location
##
param([string]$moduleName, [string]$moduleRootPath,  [string]$dropRootUNCPath, [string]$assemblyFileVersion)

begin{
    $moduleBinPath = [system.io.path]::GetFullPath("$moduleRootPath\Bin\")
    $moduleDropPath = [system.io.path]::GetFullPath($dropRootUNCPath+'\'+$assemblyFileVersion)
}

process{

    if(![System.IO.Directory]::Exists($moduleBinPath)){
        write "$moduleBinPath Not Found"
        throw (new-object IO.DirectoryNotFoundException)
    }    

    # Declare Variable for Bin Path checking.
    $moduleDropPathBin = [system.io.path]::GetFullPath($moduleDropPath +'\Bin')
    # Create Bin Folder if it doesn't Exist.
    if(![System.IO.Directory]::Exists($moduleDropPathBin)){
        write "Creating Bin folder as it doesn't exist"
        [System.IO.Directory]::CreateDirectory($moduleDropPathBin)
    }
    write "Copying $moduleBinPath to $moduleDropPath"

    Copy-Item $moduleBinPath -Destination $moduleDropPath -Recurse -Force
}

end{
    write "Managed binaries updated for $moduleName"
}