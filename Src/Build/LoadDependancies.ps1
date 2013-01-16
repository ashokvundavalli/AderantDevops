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
            $moduleBinariesPath = GetPathToBinaries $module $dropPath  
			 if( ([string]$moduleBinariesPath).Contains("Web."))  { <# web files are packaged using webdeploy, and need to be unzipped and copied in a specific way #>
			 	Write-Output $moduleBinariesPath
				& $buildScriptsDirectory\..\build.tools\GetWebProjectDependencies.exe $moduleBinariesPath $moduleDependenciesDirectory
			 } else {
                CopyContents $moduleBinariesPath $moduleDependenciesDirectory
				$type = $moduleBinariesPath.GetType().Name
				if ($type -eq "String") {
					$modulePath = $moduleBinariesPath.SubString(0, $moduleBinariesPath.LastIndexOf("\"))
					$moduleName = $modulePath.SubString($modulePath.LastIndexOf("\")+1)  <# e.g. Web.Workflow #>
					$folders =  Get-ChildItem $moduleDependenciesDirectory\..\src\ | ?{ $_.PSIsContainer }
					foreach ($folder in $folders) { <# Iterate all projects in the src folder, copying in js and css dependencies if they exist #>
						$fn = $folder.Name
						$scriptsFolder = "$moduleDependenciesDirectory\..\src\$fn\Scripts"
						if (Test-Path $scriptsFolder\$moduleName) { <# delete Scripts\ThirdParty.XXX if there #>
							Remove-Item -Recurse -Force $scriptsFolder\$moduleName
						}
	                	RoboCopy $moduleBinariesPath\ $scriptsFolder\$moduleName *.js /S /NJH /NJS /NDL /NFL > $null <# copy ThirdParty.XXX into src\YYY\Scripts\ThirdPArty.XXX #>
						ls -recurse $scriptsFolder | where {!@(ls -force $_.fullname)} | rm <# delete empty folders #>
						
						$contentFolder = "$moduleDependenciesDirectory\..\src\$fn\Content\Includes"
						if (Test-Path $contentFolder\$moduleName) {
							Remove-Item -Recurse -Force $contentFolder\$moduleName
						}
	                	RoboCopy $moduleBinariesPath\ $contentFolder\$moduleName *.css /S /NJH /NJS /NDL /NFL > $null 
	                	RoboCopy $moduleBinariesPath\ $contentFolder\$moduleName *.less /S /NJH /NJS /NDL /NFL  > $null 
	                	RoboCopy $moduleBinariesPath\ $contentFolder\$moduleName *.png /S /NJH /NJS /NDL /NFL  > $null 
						ls -recurse $contentFolder | where {!@(ls -force $_.fullname)} | rm  > $null 
					}
				}
			}
        }        
		& $buildScriptsDirectory\..\build.tools\WebDependencyCsprojSynchronize.exe $moduleDependenciesDirectory
    }
}
