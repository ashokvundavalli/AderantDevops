<# 
.Synopsis 
    Co-ordinates logic to pull down all dependancies for this module from the drop server
.Example         
    LoadDependancies -$modulesRootPath \\na.aderant.com\ExpertSuite\Dev\<Branch>     
.Parameter $modulesRootPath is the root of the module directory
.Parameter $moduleName is the name of the module for which the dependencies are being processed
.Parameter $modulesRootPath is the path to the root where the modules are found
.Parameter $buildScriptsDirectory is an optional parameter is used by the build servers
.Parameter $onlyUpdated is an optional parameter to only get the  
#> 
param([string]$modulesRootPath, [string]$moduleName = $null, [string]$dropPath, [string]$buildScriptsDirectory, [switch]$onlyUpdated)

begin {
    Write-Debug "modulesRootPath = $modulesRootPath"
    Write-Debug "moduleName = $moduleName"
    Write-Debug "dropPath = $dropPath"
    Write-Debug "buildScriptsDirectory = $buildScriptsDirectory"    
    
    ###
    # Get the common Build-Libraries
    ###
    Function LoadLibraries([string]$fromBuildScriptsDirectory, [string]$to) {
        pushd $fromBuildScriptsDirectory
        Invoke-Expression ". .\Build-Libraries.ps1"
        popd		
                
        LoadLibraryAssembly $fromBuildScriptsDirectory

        $fromBuildScriptsDirectory = (Join-Path $fromBuildScriptsDirectory  \Build-Libraries.ps1)                    
        Copy-Item -Path $fromBuildScriptsDirectory -Destination $to -Force            
    }   

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
    [string]$moduleCommonBuildDirectory = (Join-Path $modulesRootPath  \CommonBuild)
    
    if (!(Test-Path $moduleCommonBuildDirectory)){ 
        New-Item -Path $moduleCommonBuildDirectory -ItemType directory | Out-Null
    }
    
    [string]$moduleDependenciesDirectory = (Join-Path $modulesRootPath  \Dependencies)
    $moduleDependenciesDirectory = [System.IO.Path]::GetFullPath($moduleDependenciesDirectory)
        
    if ([string]::IsNullOrEmpty($buildScriptsDirectory)){                  
        $buildScriptsDirectory = Join-Path -Path $dropPath -ChildPath "\Build.Infrastructure\Src\Build"                                      
        LoadLibraries $buildScriptsDirectory $moduleCommonBuildDirectory
    } else {
        LoadLibraries $buildScriptsDirectory $moduleCommonBuildDirectory 
    }
    
    $buildScriptsDirectory = [System.IO.Path]::GetFullPath($buildScriptsDirectory)

    Write-Host "Writing Resharper settings file."
    Copy-Item -Path $buildScriptsDirectory\..\Profile\sln.DotSettings -Destination  $modulesRootPath\$moduleName.sln.DotSettings -Force
    sp $modulesRootPath\$moduleName.sln.DotSettings IsReadOnly $false
    (Get-Content $modulesRootPath\$moduleName.sln.DotSettings) | Foreach-Object {$_ -replace '\[PATH\]', $env:ExpertDevBranchFolder}  | Out-File $modulesRootPath\$moduleName.sln.DotSettings
    Write-Debug "Using $buildScriptsDirectory as build script directory"

    if (Test-ReparsePoint $moduleDependenciesDirectory) {        
        #Remove-Item $moduleDependenciesDirectory -Force -Recurse -ErrorAction Stop
        [System.IO.Directory]::Delete($moduleDependenciesDirectory)

        #Remove-Item $moduleDependenciesDirectory\* -Recurse -Force -ErrorAction Stop
        # To see a files hardlinks
        #fsutil.exe hardlink list ...
    }

    Get-ExpertDependenciesForModule -ModuleName $moduleName -ModulesRootPath $modulesRootPath -DropPath $dropPath -BuildScriptsDirectory $buildScriptsDirectory
}
