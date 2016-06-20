<# 
.Synopsis 
    Co-ordinates logic to pull down all dependencies for this module from the drop server
.Example         
    LoadDependancies -$modulesRootPath \\na.aderant.com\ExpertSuite\Dev\<Branch>     
.Parameter $modulesRootPath is the path to the root where the modules are found
.Parameter $moduleName is the name of the module for which the dependencies are being processed
.Parameter $dropPath the dependency location
#> 
param([string]$modulesRootPath, [string]$moduleName = $null, [string]$dropPath)

begin {
    $ErrorActionPreference = 'Stop'

    $modulesRootPath = $modulesRootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar)

    Write-Debug "modulesRootPath = $modulesRootPath"
    Write-Debug "moduleName = $moduleName"
    Write-Debug "dropPath = $dropPath"    

    $buildScriptsDirectory = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    Write-Debug "Using $buildScriptsDirectory as build script directory"    

    $buildLibraries = "$buildScriptsDirectory\Build-Libraries.ps1"
    & $buildLibraries

    LoadLibraryAssembly $buildScriptsDirectory    
}
    
process {
    # Canonicalize the path - fix doubled up slashes etc
    $dropPath = [System.IO.Path]::GetFullPath($dropPath)    

    if ([string]::IsNullOrEmpty($moduleName)) {
        # attempt to discover the module name        
        $moduleName = [System.IO.Path]::GetFileName([System.IO.Path]::GetFullPath($modulesRootPath))
    }
        
    if ([string]::IsNullOrEmpty($moduleName)) {
        throw [string]"The name of the module could not be determined from the current path"
    }  
        
    [string]$moduleBuildDirectory = (Join-Path $modulesRootPath  \Build)      
    [string]$moduleDependenciesDirectory = (Join-Path $modulesRootPath  \Dependencies)
    [string]$moduleDependenciesDirectory = [System.IO.Path]::GetFullPath($moduleDependenciesDirectory)
    
    #Write-Host "Writing Resharper settings file."
    #Copy-Item -Path "$buildScriptsDirectory\..\Profile\sln.DotSettings" -Destination  "$modulesRootPath\$moduleName.sln.DotSettings" -Force
    #sp $modulesRootPath\$moduleName.sln.DotSettings IsReadOnly $false
    #(Get-Content "$modulesRootPath\$moduleName.sln.DotSettings") | Foreach-Object {$_ -replace '\[PATH\]', $env:ExpertDevBranchFolder}  | Out-File "$modulesRootPath\$moduleName.sln.DotSettings"
   
    
    if (Test-ReparsePoint $moduleDependenciesDirectory) {        
        #Remove-Item $moduleDependenciesDirectory -Force -Recurse -ErrorAction Stop
        [System.IO.Directory]::Delete($moduleDependenciesDirectory)

        #Remove-Item $moduleDependenciesDirectory\* -Recurse -Force -ErrorAction Stop
        # To see a files hardlinks
        #fsutil.exe hardlink list ...
    }

    Write-Host "Retrieving third party modules."
    $useThirdPartyFromDrop = $false

    if (-not $global:IsTeamBuild) {
        $thirdPartyVersionLocalFilePath = Join-Path -Path $modulesRootPath -ChildPath "ThirdPartyBuild.txt"
        $thirdPartyVersionServerFilePath = Join-Path -Path $dropPath -ChildPath "ThirdPartyBuild.txt"
        if (-not (Test-Path $thirdPartyVersionLocalFilePath)) {
            $useThirdPartyFromDrop = $true
            Write-Host "Using drop folder for third party dependencies" -ForegroundColor Cyan
            if (Test-Path $thirdPartyVersionServerFilePath) {
                Write-Host "Updating third party build version file" -ForegroundColor Cyan
                Copy-Item -Path $thirdPartyVersionServerFilePath -Destination $thirdPartyVersionLocalFilePath
            }
            else {
                Write-Host "Third party build version file does not exist in drop location" -ForegroundColor Cyan
            }
        } elseif (Test-Path $thirdPartyVersionServerFilePath) {
            $localVersion = Get-Content $thirdPartyVersionLocalFilePath
            $serverVersion = Get-Content $thirdPartyVersionServerFilePath
            Write-Host "Local build version of third party modules: " $localVersion -ForegroundColor Cyan
            Write-Host "Server build version of third party modules:" $serverVersion -ForegroundColor Cyan
            if ($localVersion -ne $serverVersion) {
                $useThirdPartyFromDrop = $true
                Write-Host "Using drop folder for third party dependencies" -ForegroundColor Cyan
            
                # check file hashes to see if the build output is newer
                Write-Host "Detecting changes to third party modules." -ForegroundColor Cyan
              
                [xml]$expertmanifest = LoadExpertManifest $buildScriptsDirectory
                [xml]$manifest = LoadManifest $moduleBuildDirectory    
                $hasAnyThirdPartyModuleChanged = $false
                
                if(-not [string]::IsNullOrEmpty($manifest.DependencyManifest.ReferencedModules)) {
                    foreach($module in $manifest.DependencyManifest.ReferencedModules.SelectNodes("ReferencedModule")) {                                
                        $moduleName = $module.Name;
                        $expertmanifestModuleDef = $expertmanifest.ProductManifest.Modules.SelectSingleNode("Module[@Name = '$moduleName']")
                        if($expertmanifestModuleDef) {
                            Write-Debug "Using Expert Manifest dependency definition for $moduleName"
                            $module = $expertmanifestModuleDef
                        }                            
                        if((IsThirdparty $module) -or (IsHelp $module)) {
                            $localModulePath = Join-Path (Join-Path -Path $modulesRootPath -ChildPath "..\ThirdParty") $module.Name
                            $serverModulePath = Join-Path -Path $dropPath -ChildPath $module.Name
                            Write-Debug "$localModulePath"
                            Write-Debug "$serverModulePath"
                            Write-Debug ""
                            $moduleFoundLocally = Test-Path -Path $localModulePath            
                            Write-Debug "$moduleFoundLocally"

                            if($moduleFoundLocally){
                                $hasServerVersionChanged = Compare-Checksums (Join-Path $localModulePath bin) (Join-Path $serverModulePath bin)
                                if ($hasServerVersionChanged) {
                                    $hasAnyThirdPartyModuleChanged = $true
                                    Write-Host "Change detected in module" $module.Name " - using drop folder for third party dependencies" -ForegroundColor Cyan
                                    Write-Host "You may want to get the latest third party files from TFS" -ForegroundColor Cyan
                                    break
                                }
                            }
                        }
                    }
                } 

                if (-not $hasAnyThirdPartyModuleChanged) {
                    Write-Host "Updating third party build version file" -ForegroundColor Cyan
                    Copy-Item -Path $thirdPartyVersionServerFilePath -Destination $thirdPartyVersionLocalFilePath
                }
            }
        }
        else {
            Write-Host "Third party build version file does not exist in drop location" $dropPath -ForegroundColor Cyan
        }   
    }
    
    #if ($useThirdPartyFromDrop) {
    #    Get-ExpertDependenciesForModule -ModuleName $moduleName -ModulesRootPath $modulesRootPath -DropPath $dropPath -BuildScriptsDirectory $buildScriptsDirectory -UseThirdPartyFromDrop
    #} else {
    #    Get-ExpertDependenciesForModule -ModuleName $moduleName -ModulesRootPath $modulesRootPath -DropPath $dropPath -BuildScriptsDirectory $buildScriptsDirectory
    #}

    Get-DependenciesForRepository -RepositoryDirectory $modulesRootPath -DropPath $dropPath -BuildScriptsDirectory $buildScriptsDirectory -UseThirdPartyFromDrop


}
