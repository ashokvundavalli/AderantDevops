<# 
.Synopsis 
    Co-ordinates logic to pull down all dependencies for this module from the drop server
.Example         
    LoadDependencies -$modulesRootPath \\na.aderant.com\ExpertSuite\Dev\<Branch>     
.Parameter $modulesRootPath is the root of the module directory
.Parameter $moduleName is the name of the module for which the dependencies are being processed
#> 
param([string]$modulesRootPath, [string]$moduleName = $null, [string]$dropPath, [switch]$update, [switch]$showOutdated, [switch]$force)

begin {
    $ErrorActionPreference = 'Stop'

    Write-Debug "modulesRootPath = $modulesRootPath"
    Write-Debug "moduleName = $moduleName"
    Write-Debug "dropPath = $dropPath"    

    $buildScriptsDirectory = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
    Write-Debug "Using $buildScriptsDirectory as build script directory"    

    $buildLibraries = "$buildScriptsDirectory\Build-Libraries.ps1"
    & $buildLibraries

    LoadLibraryAssembly $buildScriptsDirectory
    
    $file = gci $modulesRootPath -Filter DependencyManifest.xml -Recurse | Select-Object -First 1
   
    if ($file) {
        [xml]$xml = Get-Content $file.FullName
		
		if ($xml.DependencyManifest.DefaultSource) {
			$dropPath = $xml.DependencyManifest.DefaultSource

            if ([System.Environment]::UserInteractive) {
                if ($dropPath.IndexOf($BranchServerDirectory, [System.StringComparison]::OrdinalIgnoreCase) -eq -1) {
                    Write-Warning "The local branch context does not match the 'DefaultSource' attribute of the dependency manifest."
                }
            }            
		}

        if ([string]::IsNullOrEmpty($dropPath)) {
            # Empty nodes are converted to string in PowerShell, so we use SelectSingleNode to get an XmlElement instead                
            if (-not ($xml.DependencyManifest.SelectSingleNode("ReferencedModules").IsEmpty)) {
                throw "No default branch was specified for dependencies. Provide a default value in the 'DefaultSource' attribute of the dependency manifest."
            } else {
                Write-Host "No dependencies specified. Nothing to do."
                $abort = $true
            }
        }
    }
}
    
process {
	if ($abort) {
		return
	}

    if ([string]::IsNullOrEmpty($moduleName)) {
        # attempt to discover the module name
        $moduleName = ([System.IO.DirectoryInfo]$modulesRootPath).Name
    }
        
    if ([string]::IsNullOrEmpty($moduleName)) {
        throw [string]"The name of the module could not be determined from the current path"
    }  
        
    [string]$moduleBuildDirectory = (Join-Path $modulesRootPath  \Build)      
    [string]$moduleDependenciesDirectory = (Join-Path $modulesRootPath  \Dependencies)
    [string]$moduleDependenciesDirectory = [System.IO.Path]::GetFullPath($moduleDependenciesDirectory)
    
    Write-Host "Writing Resharper settings file."
    Copy-Item -Path "$buildScriptsDirectory\..\Profile\sln.DotSettings" -Destination  "$modulesRootPath\$moduleName.sln.DotSettings" -Force
    sp $modulesRootPath\$moduleName.sln.DotSettings IsReadOnly $false

	$absolutePath = (Resolve-Path "$buildScriptsDirectory\..\..\..")
	$currentLocation = Get-Location
	Set-Location $modulesRootPath
	$relativePath = "..\" + (Resolve-Path -relative $absolutePath)
	Set-Location $currentLocation

    (Get-Content "$modulesRootPath\$moduleName.sln.DotSettings") | Foreach-Object { ($_ -replace '\[ABSOLUTEPATH\]', $absolutePath) -replace '\[RELATIVEPATH\]', $relativePath }  | Out-File "$modulesRootPath\$moduleName.sln.DotSettings"
       
    Copy-Item "$PSScriptRoot\dir.proj" -Destination "$modulesRootPath\dir.proj" -Force
    Copy-Item "$PSScriptRoot\Aderant.wpp.content.proj" -Destination "$modulesRootPath\Aderant.wpp.content.proj" -Force

    if (Test-ReparsePoint $moduleDependenciesDirectory) {
        [System.IO.Directory]::Delete($moduleDependenciesDirectory)

        # To see a files hardlinks
        #fsutil.exe hardlink list ...
    }

    if ($global:BranchModulesDirectory) {
        Remove-Item -Path $global:BranchModulesDirectory\paket.dependencies -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $global:BranchModulesDirectory\paket.lock -Force -ErrorAction SilentlyContinue
    }
            
    Get-ExpertDependenciesForModule -ModuleName $moduleName -ModulesRootPath $modulesRootPath -DropPath $dropPath -BuildScriptsDirectory $buildScriptsDirectory -Update:$update -ShowOutdated:$showOutdated -Force:$force -ProductManifestPath $global:ProductManifestPath
}
