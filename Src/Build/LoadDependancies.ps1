<# 
.Synopsis 
    Co-ordinates logic to pull down all dependancies for this module 
.Example     
    LoadDependancies -$modulesRootPath C:\Source\<Branch>\Modules,  Get local dependacies    
    LoadDependancies -$modulesRootPath \\na.aderant.com\ExpertSuite\Dev\<Branch>, Get dependacies from drop
.Parameter $modulesRootPath is the path to the root where the modules are found
#> 
param([string]$modulesRootPath, [string]$dropPath, [bool]$localBuild = $false)

begin{
    ###
    # Get the common Build-Libraries
    ###
     Function LoadLibraries([string]$from, [string]$to){            
        $from = (Join-Path $from  \Build.Infrastructure\Src\Build\Build-Libraries.ps1)                    
        Copy-Item -Path $from -Destination $to -Force            
        $shell = (Join-Path $to  Build-Libraries.ps1)             
        &($shell)                                           
    }
}

process{
    
    write "Execute LoadModuleDependencies"       
        
    [string]$moduleBuildDirectory = (Join-Path $modulesRootPath  \Build)
    [string]$moduleCommonBuildDirectory = (Join-Path $modulesRootPath  \CommonBuild)
    
    if(!(Test-Path $moduleCommonBuildDirectory)){ 
        New-Item -Path $moduleCommonBuildDirectory -ItemType directory 
    }
    
    [string]$moduleDependenciesDirectory = (Join-Path $modulesRootPath  \Dependencies)                  
        
    LoadLibraries $dropPath $moduleCommonBuildDirectory 
                                         
    DeleteContentsFrom $moduleDependenciesDirectory
                                      
    CopyModuleBuildFiles $dropPath $moduleCommonBuildDirectory $localBuild        
    
    [xml]$manifest = LoadManifest $moduleBuildDirectory
    
    foreach($module in $manifest.DependencyManifest.ReferencedModules.SelectNodes("ReferencedModule")){        
    
        if($localBuild){
            $moduleBinariesPath = PathToLocalBinaries $module $dropPath 
        }else{
            $moduleBinariesPath = PathToBinaries $module $dropPath         
        }                
        CopyContents $moduleBinariesPath $moduleDependenciesDirectory
    }            
}
