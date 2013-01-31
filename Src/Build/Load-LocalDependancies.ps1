<# 
.Synopsis 
    Co-ordinates logic to get all dependancies for this module locally
.Example     
    LoadDependancies -$moduleRootPath Libaries.Test -localModulesRootPath C:\tfs\project\branch\dev\modules -serverRootPath \\na.aderant.com\expertsuite\dev\branch      
.Parameter $moduleRootPath is the path to the root where the module we are getting dependencies for 
.Parameter $localModulesRootPath is the path to the root of your branch 
.Parameter $serverRootPath the build drop location for thirdparty modules that are not in your branch

#> 
param([string]$moduleName, [string]$localModulesRootPath, [string]$serverRootPath)

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
    
    ###
    # Get the ThirdParty binaries path
    ###
    Function GetThirdPartyModulePath([System.Xml.XmlNode]$module, [string]$serverRootPath, [string]$localModulesRootPath){                  
                    
        $moduleFoundLocally = Test-Path -Path (Join-Path -Path $localModulesRootPath -ChildPath $module.Name)            
        
        if($moduleFoundLocally){
            $path = GetLocalPathToBinaries $module $localModulesRootPath     
        }else{
            $path = GetPathToBinaries $module $serverRootPath
        }                            
        return $path          
    }
    
    ###
    # Get the binaries path
    ###
    Function GetModulePath([System.Xml.XmlNode]$module, [string]$localModulesRootPath){                                                  
            return (GetLocalPathToBinaries $module $localModulesRootPath)
    }    
}

process{
    
    write "Execute LoadModuleDependencies"       
    
    $modulePath = Join-Path -Path $localModulesRootPath -ChildPath $moduleName
    
    if(Test-Path $modulePath){
        
        [string]$moduleBuildDirectory = (Join-Path $modulePath  \Build)
        [string]$moduleCommonBuildDirectory = (Join-Path $modulePath  \CommonBuild)
        [string]$moduleDependenciesDirectory = (Join-Path $modulePath  \Dependencies) 
        
        if(!(Test-Path $moduleCommonBuildDirectory)){ 
            New-Item -Path $moduleCommonBuildDirectory -ItemType directory 
        }                                 
                        
        $buildScriptsDirectory = Join-Path -Path $localModulesRootPath -ChildPath "\Build.Infrastructure\Src\Build"
        LoadLibraries $buildScriptsDirectory $moduleCommonBuildDirectory    
                                             
        DeleteContentsFrom $moduleDependenciesDirectory                                            
        
		[xml]$expertmanifest = LoadExpertManifest $buildScriptsDirectory
        [xml]$manifest = LoadManifest $moduleBuildDirectory    
        
        if([string]::IsNullOrEmpty($manifest.DependencyManifest.ReferencedModules)){
            write "There are no referenced modules"
        }else{
            foreach($module in $manifest.DependencyManifest.ReferencedModules.SelectNodes("ReferencedModule")){                                
				$moduleName = $module.Name;
				$expertmanifestModuleDef = $expertmanifest.ProductManifest.Modules.SelectSingleNode("Module[@Name = '$moduleName']")
				if($expertmanifestModuleDef){
					Write-Debug "Using Expert Manifest dependency definition for $moduleName"
					$module = $expertmanifestModuleDef
				}                            
                if((IsThirdparty $module) -or (IsHelp $module)){
                   $moduleBinariesPath = GetThirdPartyModulePath $module $serverRootPath $localModulesRootPath
                }else{
                   $moduleBinariesPath = GetModulePath $module $localModulesRootPath
                }                        
                CopyContents $moduleBinariesPath $moduleDependenciesDirectory
            }        
        }
    }else{    
        throw "$modulePath does not exist"
    }                
}
