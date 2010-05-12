<# 
.Synopsis 
    Co-ordinates logic to pull down all dependancies for this module from the drop server
.Example         
    LoadDependancies -$modulesRootPath \\na.aderant.com\ExpertSuite\Dev\<Branch>     
.Parameter $modulesRootPath is the path to the root where the modules are found
.Parameter $buildScriptsDirectory is an optional parameter is used by the build servers
#> 
param([string]$modulesRootPath, [string]$dropPath, [string]$buildScriptsDirectory)

begin{
    ###
    # Get the common Build-Libraries
    ###
     Function LoadLibraries([string]$fromBuildScriptsDirectory, [string]$to){            
        $fromBuildScriptsDirectory = (Join-Path $fromBuildScriptsDirectory  \Build-Libraries.ps1)                    
        Copy-Item -Path $fromBuildScriptsDirectory -Destination $to -Force            
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
        
    if([string]::IsNullOrEmpty($buildScriptsDirectory)){                  
        $buildScriptsDirectory = Join-Path -Path $dropPath -ChildPath "\Build.Infrastructure\Src\Build"                                      
        LoadLibraries $buildScriptsDirectory $moduleCommonBuildDirectory
    }else{
        LoadLibraries $buildScriptsDirectory $moduleCommonBuildDirectory 
    }        
                                         
    DeleteContentsFrom $moduleDependenciesDirectory                                            
    
    [xml]$manifest = LoadManifest $moduleBuildDirectory
        
    if([string]::IsNullOrEmpty($manifest.DependencyManifest.ReferencedModules)){
        Write-Debug "There are no referenced modules"
    }else{
        foreach($module in $manifest.DependencyManifest.ReferencedModules.SelectNodes("ReferencedModule")){                                
            Write-Debug ""                        
            $moduleBinariesPath = GetPathToBinaries $module $dropPath                        
            CopyContents $moduleBinariesPath $moduleDependenciesDirectory
        }        
    }
                
}
