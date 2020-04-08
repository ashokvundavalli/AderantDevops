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
	[Parameter(Mandatory=$false)][string]$moduleName,
	[Parameter(Mandatory=$true)][string]$dropPath,
	[Parameter(Mandatory=$true)][string]$manifestFile
)

begin {
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'

    Write-Output "Running '$($MyInvocation.MyCommand.Name.Replace(`".ps1`", `"`"))' with the following parameters:"

    foreach ($parameter in $MyInvocation.MyCommand.Parameters) {
        Write-Output (Get-Variable -Name $Parameter.Values.Name -ErrorAction SilentlyContinue | Out-String)
    }

    [string]$buildScriptsDirectory = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    Write-Debug "BuildScriptsDirectory: $buildScriptsDirectory"
    . "$buildScriptsDirectory\Build-Libraries.ps1"
}
    
process {
    if ([string]::IsNullOrWhiteSpace($moduleName)) {
        # Attempt to discover the module name.
        $moduleName = ([System.IO.DirectoryInfo]$modulesRootPath).Name
    }

    if ([string]::IsNullOrWhiteSpace($moduleName)) {
        Write-Error "The name of the module could not be determined from the current path"
        Exit 1
    }

    Write-Output "Module Name: $moduleName"

    [string]$moduleDependenciesDirectory = [System.IO.Path]::GetFullPath((Join-Path -Path $modulesRootPath -ChildPath "Dependencies"))
    
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

    Get-ExpertDependenciesForModule -ModuleName $moduleName -ModulesRootPath $modulesRootPath -DependenciesDirectory $moduleDependenciesDirectory -DropPath $dropPath -BuildScriptsDirectory $buildScriptsDirectory -ManifestFile $manifestFile

	if (Test-Path $paketLockFile) {
		Remove-Item -Path $paketLockFile -Force
	}

	if (Test-Path $backupPaketLockFile) {
		Rename-Item -Path $backupPaketLockFile -NewName $paketLockFile -Force
	}
}

end {
    exit 0
}