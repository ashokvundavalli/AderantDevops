<#
.Synopsis
    Copies the module build output from <ModuleName>\Bin\* to the drop location
.Parameter moduleName
    The module name
.Parameter moduleRootPath
    The local path to the module on the build server
.Parameter dropRootUNCPath
    The network drop path of the branch
.Parameter components
	The submodules within a particular module
.Parameter origin
	TFVC branch name or Git branch name
.Parameter version
	The TFS build id
#>
param(
	[Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$moduleName,
	[Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$moduleRootPath,
	[Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$dropRoot,
	[Parameter(Mandatory=$false)][string[]]$components,
	[Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$origin,
	[Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$version,
	[Parameter(Mandatory=$true)][boolean]$generateFile
)

process {
	Set-StrictMode -Version 2.0
	$ErrorActionPreference = "Stop"

	# Identify Build Libraries
    [string]$buildScriptsDirectory = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    Write-Debug "Using: $($buildScriptsDirectory) as build script directory"
    [string]$buildLibraries = [System.IO.Path]::Combine($buildScriptsDirectory, "Build-Libraries.ps1")

	if ($generateFile) {
		[string]$componentsVariable = ""

		if ($components) {
			$componentsVariable = "-components $($components)"
		}

		[string]$file = "`$ErrorActionPreference = `"Stop`"

# Load Build Libraries
& $($buildLibraries)

# Copy the module build output from <ModuleName>\Bin\* directory to the drop root
CopyFilesToDrop -moduleName $($moduleName) -moduleRootPath $($moduleRootPath) -dropRoot $($dropRoot) $componentsVariable -origin $($origin) -version $($version)"

		[string]$outputPath = Join-Path -Path $moduleRootPath -ChildPath "CopyToDrop.ps1"
		New-Item -ItemType File -Path $outputPath -Value $file -Force

        Write-Host "Generated $outputPath"
	} else {
		& $buildLibraries

		# Copy the module build output from <ModuleName>\Bin\* directory to the drop root
		CopyFilesToDrop -moduleName $moduleName -moduleRootPath $moduleRootPath -dropRoot $dropRoot -components $components -origin $origin -version $version
	}
}

end {
}