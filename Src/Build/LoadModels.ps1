<# 
.Synopsis 
    Co-ordinates logic to pull down all models for the Query Service.
.Example     
    LoadModels -$modulesRootPath C:\Source\<Branch>\Modules,  Get local dependacies    
.Parameter $modulesRootPath is the path to the root where the modules are found
#> 
param([string]$productManifestPath, [string]$dropPath, [string]$target, [string]$buildLibrariesPath)

begin{
    ###
    # Get the common Build-Libraries
    ###
    Function LoadLibraries(){            
        if([String]::IsNullOrEmpty($buildLibrariesPath)){
            $shell = (Join-Path $dropPath \Build.Infrastructure\Src\Build\Build-Libraries.ps1 )
        }else{            
            $shell = (Join-Path $buildLibrariesPath \Build-Libraries.ps1 )
        }
        &($shell)
    }
}

process{
    
    write "Execute LoadModels"       
     
    LoadLibraries 
    
    GetModelsForProduct -productManifestPath $productManifestPath -dropPath $dropPath -target $target
    
    
}
