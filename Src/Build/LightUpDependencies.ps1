<# 
.Synopsis 
    Co-ordinates logic to pull down all dependencies for this module from the drop server
.Example         
    .\LightUpDependencies.ps1 -$modulesRootPath \\dfs.aderant.com\ExpertSuite\Dev\<Branch>     
.Parameter $modulesRootPath is the root of the module directory
.Parameter $moduleName is the name of the module for which the dependencies are being processed
#> 
param(
	[Parameter(Mandatory=$true)][string]$modulesRootPath,
	[Parameter(Mandatory=$false)][string]$moduleName = $null,
	[Parameter(Mandatory=$true)][string]$dropPath,
	[Parameter(Mandatory=$true)][string]$manifestFile
)

begin {
    $ErrorActionPreference = 'Stop'

    Write-Debug "modulesRootPath = $modulesRootPath"
    Write-Debug "moduleName = $moduleName"
    Write-Debug "dropPath = $dropPath"    

    $buildScriptsDirectory = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    Write-Debug "Using $buildScriptsDirectory as build script directory"    

    [string]$buildLibraries = "$buildScriptsDirectory\Build-Libraries.ps1"
    & $buildLibraries
    LoadLibraryAssembly $buildScriptsDirectory
}
    
process {
	Write-Host "Module name is $moduleName in LoadDependencies."
    if ([string]::IsNullOrEmpty($moduleName)) {
        # attempt to discover the module name
        $moduleName = ([System.IO.DirectoryInfo]$modulesRootPath).Name
		Write-Host "Module name set to $moduleName in LoadDependencies."
    }
        
    if ([string]::IsNullOrEmpty($moduleName)) {
        throw [string]"The name of the module could not be determined from the current path"
    }

    [string]$moduleDependenciesDirectory = (Join-Path -Path $modulesRootPath -ChildPath "Dependencies")
    [string]$moduleDependenciesDirectory = [System.IO.Path]::GetFullPath($moduleDependenciesDirectory)
    
    if (Test-ReparsePoint $moduleDependenciesDirectory) {
        [System.IO.Directory]::Delete($moduleDependenciesDirectory)

        # To see a files hardlinks
        #fsutil.exe hardlink list ...
    }

	[string]$paketLockFile = Join-Path -Path $modulesRootPath -ChildPath "paket.lock"
	[string]$backupPaketLockFile = "$($paketLockFile).backup"

	if (Test-Path $paketLockFile) {
		Rename-Item -Path $paketLockFile -NewName $backupPaketLockFile -Force
	}

	[string]$paketDependenciesFile = Join-Path -Path $modulesRootPath -ChildPath "paket.dependencies"

	if (Test-Path $paketDependenciesFile) {
		Remove-Item -Path $paketDependenciesFile -Force
	}

    Get-ExpertDependenciesForModule -ModuleName $moduleName -ModulesRootPath $modulesRootPath -DependenciesDirectory $moduleDependenciesDirectory -DropPath $dropPath -BuildScriptsDirectory $buildScriptsDirectory -ProductManifestPath $global:ProductManifestPath -ManifestFile $manifestFile

	if (Test-Path $paketLockFile) {
		Remove-Item -Path $paketLockFile -Force
	}

	if (Test-Path $backupPaketLockFile) {
		Rename-Item -Path $backupPaketLockFile -NewName $paketLockFile -Force
	}
}