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
        if(Test-Path $from){                        
            $from = (Join-Path $from  \Build.Infrastructure\Src\Libraries\Build-Libraries.ps1)                    
            Copy-Item -Path $from -Destination $to -Force
            .\Build-Libraries.ps1
        }else{
            throw "Path [$from] is not valid"
        }                                                
    }
}

process{
    
    write "Execute LoadModuleDependencies"       
        
    [string]$moduleBuildDirectory = (Join-Path $modulesRootPath  \Build)
    [string]$moduleDependenciesDirectory = (Join-Path $modulesRootPath  \Dependencies)              
        
    LoadLibraries $dropPath $moduleBuildDirectory
                                         
    DeleteContentsFrom $moduleDependenciesDirectory                                   
    CopyModuleBuildFiles $modulesRootPath $moduleBuildDirectory $copyFromLocalBin        
    
    [xml]$manifest = LoadManifest $moduleBuildDirectory
    
    foreach($module in $manifest.DependencyManifest.ReferencedModules.SelectNodes("ReferencedModule")){        
        $moduleBinariesPath = PathToBinaries $module                                                                 
        CopyContents $moduleBinariesPath $moduleDependenciesDirectory
    }            
}
